using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using AssignmentSystem.Api.Controllers;
using AssignmentSystem.Application.Features.Assignments;
using AssignmentSystem.Application.Features.Submissions;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Infrastructure.Persistence.Seed;
using Xunit;

namespace AssignmentSystem.Api.Tests;

/// <summary>
/// Shared plumbing for the integration suite: authenticated clients, envelope reading,
/// and helpers that provision an isolated "world" (class, course, teacher, student)
/// through the Admin API so authorization tests can cross realistic boundaries.
/// </summary>
[Collection(ApiCollection.Name)]
public abstract class IntegrationTestBase
{
    protected const string AuthCookieName = "asm_refresh";
    protected const string TestPassword = "Password123!";

    protected static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    protected IntegrationTestBase(ApiFactory api)
    {
        Api = api;
        Client = api.CreateClient();
    }

    protected ApiFactory Api { get; }

    /// <summary>Default client for the test. <see cref="AuthenticateAsync"/> attaches a bearer token to it.</summary>
    protected HttpClient Client { get; }

    // ── Clients & auth ────────────────────────────────────────────────────────

    protected HttpClient CreateCookielessClient() => Api.CreateCookielessClient();

    protected async Task AuthenticateAsync(string email, string password) =>
        Client.DefaultRequestHeaders.Authorization = await BearerAsync(email, password);

    /// <summary>A fresh client already signed in as the given user — lets one test act as several people.</summary>
    protected async Task<HttpClient> SignInAsync(string email, string password = TestPassword)
    {
        var client = Api.CreateClient();
        client.DefaultRequestHeaders.Authorization = await BearerAsync(email, password);
        return client;
    }

    protected Task<HttpClient> SignInAsAdminAsync() => SignInAsync(DbSeeder.AdminEmail, DbSeeder.DefaultPassword);

    private async Task<AuthenticationHeaderValue> BearerAsync(string email, string password)
    {
        using var client = Api.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        response.EnsureSuccessStatusCode();

        var payload = await ReadAsync<AuthResponseBody>(response);
        return new AuthenticationHeaderValue("Bearer", payload.AccessToken);
    }

    // ── Envelope helpers ──────────────────────────────────────────────────────

    /// <summary>Reads the success envelope and asserts the payload is present.</summary>
    protected static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<T>>(JsonOptions);
        envelope.Should().NotBeNull();
        envelope!.Data.Should().NotBeNull();
        return envelope.Data!;
    }

    /// <summary>Reads a paged response including its pagination metadata.</summary>
    protected static async Task<(List<T> Items, PaginationMeta Pagination)> ReadPageAsync<T>(HttpResponseMessage response)
    {
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<List<T>>>(JsonOptions);
        envelope.Should().NotBeNull();
        envelope!.Data.Should().NotBeNull();
        envelope.Pagination.Should().NotBeNull();
        return (envelope.Data!, envelope.Pagination!);
    }

    protected sealed class ApiResponseEnvelope<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public PaginationMeta? Pagination { get; set; }
    }

    protected sealed class PaginationMeta
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int Total { get; set; }
        public int TotalPages { get; set; }
    }

    // ── Provisioning (via the Admin API, so the endpoints are exercised too) ──

    /// <summary>
    /// Creates a self-contained department + class + course + teacher + student, wired
    /// together by a teacher assignment. Two worlds give every "may user X touch resource
    /// Y?" test a genuine boundary to cross.
    /// </summary>
    protected async Task<TestWorld> ProvisionWorldAsync(string label)
    {
        // Short unique tag: the suite shares one database, so names and codes must not collide.
        var tag = $"{label}-{Guid.NewGuid():N}"[..(label.Length + 9)];
        using var admin = await SignInAsAdminAsync();

        var @class = await PostAsync<ClassRef>(admin, "/api/v1/classes",
            new CreateClassRequest($"Class {tag}", "X", tag));

        // Department codes are capped at 10 characters, so this one cannot be built from
        // the tag — "D" plus 9 hex digits is short enough and still collision-free.
        var departmentCode = $"D{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var department = await PostAsync<DepartmentRef>(admin, "/api/v1/departments",
            new CreateDepartmentRequest($"Department {tag}", departmentCode));

        var course = await PostAsync<CourseRef>(admin, "/api/v1/courses",
            new CreateCourseRequest($"Course {tag}", $"CRS-{tag}", department.Id));

        var teacherEmail = $"teacher-{tag}@test.local";
        var teacher = await PostAsync<UserRef>(admin, "/api/v1/users",
            new CreateUserRequest(teacherEmail, $"Teacher {tag}", TestPassword, Role.Teacher, null, department.Id));

        var studentEmail = $"student-{tag}@test.local";
        var student = await PostAsync<UserRef>(admin, "/api/v1/users",
            new CreateUserRequest(studentEmail, $"Student {tag}", TestPassword, Role.Student, @class.Id, null));

        var teacherAssignment = await PostAsync<TeacherAssignmentRef>(admin, "/api/v1/teacher-assignments",
            new CreateTeacherAssignmentRequest(teacher.Id, course.Id, @class.Id));

        return new TestWorld(
            @class.Id,
            course.Id,
            teacherAssignment.Id,
            teacher.Id,
            teacherEmail,
            student.Id,
            studentEmail);
    }

    /// <summary>Creates a draft assignment as the world's teacher.</summary>
    protected async Task<AssignmentDto> CreateAssignmentAsync(
        HttpClient teacherClient,
        Guid teacherAssignmentId,
        string title = "Test Assignment",
        decimal maxMarks = 100m,
        DateTime? deadlineUtc = null,
        bool allowResubmission = true)
    {
        var response = await teacherClient.PostAsJsonAsync("/api/v1/assignments", new CreateAssignmentRequest(
            teacherAssignmentId,
            title,
            "Answer every question.",
            deadlineUtc ?? DateTime.UtcNow.AddDays(7),
            maxMarks,
            allowResubmission));

        response.EnsureSuccessStatusCode();
        return await ReadAsync<AssignmentDto>(response);
    }

    /// <summary>Creates and publishes an assignment — the usual starting point for submission tests.</summary>
    protected async Task<AssignmentDto> CreatePublishedAssignmentAsync(
        HttpClient teacherClient,
        Guid teacherAssignmentId,
        string title = "Test Assignment",
        decimal maxMarks = 100m,
        DateTime? deadlineUtc = null,
        bool allowResubmission = true)
    {
        var assignment = await CreateAssignmentAsync(
            teacherClient, teacherAssignmentId, title, maxMarks, deadlineUtc, allowResubmission);

        var publish = await teacherClient.PostAsync($"/api/v1/assignments/{assignment.Id}/publish", null);
        publish.EnsureSuccessStatusCode();

        return await ReadAsync<AssignmentDto>(publish);
    }

    /// <summary>Submits a text answer as the given student.</summary>
    protected static async Task<SubmissionDto> SubmitAsync(HttpClient studentClient, Guid assignmentId, string content)
    {
        var response = await studentClient.PostAsJsonAsync(
            $"/api/v1/assignments/{assignmentId}/submissions",
            new SubmitAssignmentRequest(content));

        response.EnsureSuccessStatusCode();
        return await ReadAsync<SubmissionDto>(response);
    }

    /// <summary>Adds another student to an existing class — for "classmate cannot peek" tests.</summary>
    protected async Task<string> AddStudentToClassAsync(Guid classId, string label)
    {
        var tag = $"{label}-{Guid.NewGuid():N}"[..(label.Length + 9)];
        var email = $"{tag}@test.local";

        using var admin = await SignInAsAdminAsync();
        var response = await admin.PostAsJsonAsync("/api/v1/users",
            new CreateUserRequest(email, $"Student {tag}", TestPassword, Role.Student, classId, null));

        response.EnsureSuccessStatusCode();
        return email;
    }

    /// <summary>The signed-in caller's own id, read from the token rather than assumed.</summary>
    protected static async Task<Guid> CurrentUserIdAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/auth/me");
        response.EnsureSuccessStatusCode();
        return (await ReadAsync<UserRef>(response)).Id;
    }

    /// <summary>
    /// A teacher-assignment the seeder created for the signed-in teacher. Filtered by
    /// teacher id rather than taking the first row: the suite shares a database, so other
    /// tests' mappings are present too.
    ///
    /// Pass <paramref name="classId"/> whenever a student has to see the resulting
    /// assignment. The demo teacher holds several mappings across different classes and
    /// they all sort equally (same teacher name), so picking blind returns an arbitrary
    /// one — and an assignment for the wrong class is correctly a 403 for that student.
    /// </summary>
    protected static async Task<Guid> SeededTeacherAssignmentIdAsync(HttpClient teacherClient, Guid? classId = null)
    {
        var teacherId = await CurrentUserIdAsync(teacherClient);

        var url = $"/api/v1/teacher-assignments?teacherId={teacherId}";
        if (classId.HasValue)
        {
            url += $"&classId={classId.Value}";
        }

        var response = await teacherClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var mappings = await ReadAsync<List<TeacherAssignmentRef>>(response);
        mappings.Should().NotBeEmpty("the seeder links the demo teacher to a class and course");
        return mappings[0].Id;
    }

    /// <summary>The signed-in student's class, read from their profile rather than assumed.</summary>
    protected static async Task<Guid> CurrentUserClassIdAsync(HttpClient studentClient)
    {
        var response = await studentClient.GetAsync("/api/v1/auth/me");
        response.EnsureSuccessStatusCode();

        var profile = await ReadAsync<UserClassRef>(response);
        profile.ClassId.Should().NotBeNull("the caller is expected to be a student");
        return profile.ClassId!.Value;
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        response.EnsureSuccessStatusCode();
        return await ReadAsync<T>(response);
    }

    /// <summary>Reads the refresh-token cookie value out of a response's Set-Cookie headers.</summary>
    protected static string? ReadRefreshCookie(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            return null;
        }

        const string prefix = AuthCookieName + "=";
        var header = cookies.FirstOrDefault(c => c.StartsWith(prefix, StringComparison.Ordinal));
        if (header is null)
        {
            return null;
        }

        // Return the value exactly as sent so it round-trips through the same encoding.
        var value = header[prefix.Length..];
        var end = value.IndexOf(';');
        return end < 0 ? value : value[..end];
    }

    // ── Minimal shapes for provisioning responses ─────────────────────────────
    protected sealed record TestWorld(
        Guid ClassId,
        Guid CourseId,
        Guid TeacherAssignmentId,
        Guid TeacherId,
        string TeacherEmail,
        Guid StudentId,
        string StudentEmail);

    private sealed record ClassRef(Guid Id);
    private sealed record UserClassRef(Guid Id, Guid? ClassId);
    private sealed record DepartmentRef(Guid Id);
    private sealed record CourseRef(Guid Id);
    private sealed record UserRef(Guid Id);
    private sealed record TeacherAssignmentRef(Guid Id);
}
