using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AssignmentSystem.Api.Controllers;
using AssignmentSystem.Domain.Enums;
using Xunit;

namespace AssignmentSystem.Api.Tests;

/// <summary>
/// Academic years: the reference data enrollments are scoped to.
///
/// The rules worth pinning down are the ones a read-then-write check alone would not give:
/// a session's name is unique, exactly one session is current at a time, a session with
/// enrollments cannot be deleted, and — the reason the year exists at all — the same student
/// may sit in the same class again in a later session.
///
/// The suite shares one database, so every year created here carries a unique tag and no
/// test asserts on totals. Tests that would move the "current" flag create their own years
/// and put it back, since the seeded current year is what the rest of the suite enrols into.
/// </summary>
public sealed class AcademicYearTests : IntegrationTestBase
{
    public AcademicYearTests(ApiFactory api) : base(api) { }

    private static readonly DateOnly Start = new(2040, 7, 1);
    private static readonly DateOnly End = new(2041, 6, 30);

    private static string Tag(string label) => $"{label}-{Guid.NewGuid():N}"[..(label.Length + 9)];

    private static Task<AcademicYearRow> CreateYearAsync(
        HttpClient admin, string name, bool isCurrent = false, DateOnly? start = null, DateOnly? end = null) =>
        PostAsync<AcademicYearRow>(admin, "/api/v1/academic-years",
            new CreateAcademicYearRequest(name, start ?? Start, end ?? End, isCurrent));

    // ── CRUD ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreatingAnAcademicYear_ReturnsItWithNoEnrollments()
    {
        using var admin = await SignInAsAdminAsync();
        var name = Tag("ay-new");

        var year = await CreateYearAsync(admin, name);

        year.Name.Should().Be(name);
        year.StartDate.Should().Be(Start);
        year.EndDate.Should().Be(End);
        year.IsCurrent.Should().BeFalse();
        year.EnrollmentCount.Should().Be(0);
    }

    [Fact]
    public async Task CreatingAYearWithADuplicateName_Returns409()
    {
        using var admin = await SignInAsAdminAsync();
        var name = Tag("ay-dup");
        await CreateYearAsync(admin, name);

        var response = await admin.PostAsJsonAsync("/api/v1/academic-years",
            new CreateAcademicYearRequest(name, Start, End, false));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreatingAYearEndingBeforeItStarts_Returns422()
    {
        using var admin = await SignInAsAdminAsync();

        var response = await admin.PostAsJsonAsync("/api/v1/academic-years",
            new CreateAcademicYearRequest(Tag("ay-bad"), End, Start, false));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task UpdatingAYear_ChangesItsNameAndDates()
    {
        using var admin = await SignInAsAdminAsync();
        var year = await CreateYearAsync(admin, Tag("ay-upd"));
        var newName = Tag("ay-upd2");
        var newEnd = End.AddDays(1);

        var response = await admin.PutAsJsonAsync($"/api/v1/academic-years/{year.Id}",
            new UpdateAcademicYearRequest(newName, Start, newEnd, false));
        response.IsSuccessStatusCode.Should().BeTrue();

        var updated = await ReadAsync<AcademicYearRow>(response);
        updated.Name.Should().Be(newName);
        updated.EndDate.Should().Be(newEnd);
    }

    [Fact]
    public async Task RenamingAYearToAnotherYearsName_Returns409()
    {
        using var admin = await SignInAsAdminAsync();
        var taken = await CreateYearAsync(admin, Tag("ay-taken"));
        var mine = await CreateYearAsync(admin, Tag("ay-mine"));

        var response = await admin.PutAsJsonAsync($"/api/v1/academic-years/{mine.Id}",
            new UpdateAcademicYearRequest(taken.Name, Start, End, false));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    /// <summary>Keeping its own name is not a clash with itself.</summary>
    [Fact]
    public async Task UpdatingAYearWithoutChangingItsName_Succeeds()
    {
        using var admin = await SignInAsAdminAsync();
        var year = await CreateYearAsync(admin, Tag("ay-same"));

        var response = await admin.PutAsJsonAsync($"/api/v1/academic-years/{year.Id}",
            new UpdateAcademicYearRequest(year.Name, Start, End.AddDays(1), false));

        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task DeletingAYearWithNoEnrollments_Succeeds()
    {
        using var admin = await SignInAsAdminAsync();
        var year = await CreateYearAsync(admin, Tag("ay-del"));

        var response = await admin.DeleteAsync($"/api/v1/academic-years/{year.Id}");
        response.IsSuccessStatusCode.Should().BeTrue();

        var gone = await admin.GetAsync($"/api/v1/academic-years/{year.Id}");
        gone.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// The enrollments naming the year would lose the session they describe, so the delete is
    /// refused with an explanation rather than surfacing as the foreign key's 500.
    /// </summary>
    [Fact]
    public async Task DeletingAYearThatHasEnrollments_Returns409()
    {
        var world = await ProvisionWorldAsync("ay-inuse");
        using var admin = await SignInAsAdminAsync();

        var response = await admin.DeleteAsync($"/api/v1/academic-years/{world.AcademicYearId}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task EnrollmentCount_ReflectsTheEnrollmentsAgainstTheYear()
    {
        var world = await ProvisionWorldAsync("ay-count");
        using var admin = await SignInAsAdminAsync();

        var response = await admin.GetAsync($"/api/v1/academic-years/{world.AcademicYearId}");
        var year = await ReadAsync<AcademicYearRow>(response);

        // The suite shares the seeded school, so this is "at least the one just provisioned"
        // rather than an exact total.
        year.EnrollmentCount.Should().BeGreaterThan(0);
    }

    // ── The current session ───────────────────────────────────────────────────

    /// <summary>
    /// Promoting a year takes the flag off whoever held it. Both years here are created by
    /// the test and the flag is handed back to the seeded year at the end, so the rest of the
    /// suite still finds a current session to enrol into.
    /// </summary>
    [Fact]
    public async Task MarkingAYearCurrent_ClearsThePreviousHolder()
    {
        using var admin = await SignInAsAdminAsync();
        var seededId = await CurrentAcademicYearIdAsync(admin);
        // Read before promoting, so the restore below puts the seeded year back exactly as
        // it was rather than renaming or re-dating it.
        var seeded = await ReadAsync<AcademicYearRow>(
            await admin.GetAsync($"/api/v1/academic-years/{seededId}"));

        var promoted = await CreateYearAsync(admin, Tag("ay-cur"), isCurrent: true);

        try
        {
            promoted.IsCurrent.Should().BeTrue();
            (await CurrentAcademicYearIdAsync(admin)).Should().Be(promoted.Id);

            var previous = await ReadAsync<AcademicYearRow>(
                await admin.GetAsync($"/api/v1/academic-years/{seededId}"));
            previous.IsCurrent.Should().BeFalse();
        }
        finally
        {
            // Hand it back before anything else in the suite creates a student.
            var restore = await admin.PutAsJsonAsync($"/api/v1/academic-years/{seededId}",
                new UpdateAcademicYearRequest(seeded.Name, seeded.StartDate, seeded.EndDate, true));
            restore.IsSuccessStatusCode.Should().BeTrue(
                "the rest of the suite enrols into the current year");
        }
    }

    // ── What the year is for ──────────────────────────────────────────────────

    /// <summary>
    /// The point of the whole feature: the same student in the same class in a later session
    /// is a new enrollment, not a duplicate. Under the old two-column unique key this was a
    /// 409, which made repeating a grade impossible to record.
    /// </summary>
    [Fact]
    public async Task EnrollingTheSameStudentInTheSameClassInAnotherYear_Succeeds()
    {
        var world = await ProvisionWorldAsync("ay-repeat");
        using var admin = await SignInAsAdminAsync();

        var nextYear = await CreateYearAsync(admin, Tag("ay-next"));

        var response = await admin.PostAsJsonAsync("/api/v1/enrollments",
            new CreateEnrollmentRequest(world.StudentId, world.ClassId, nextYear.Id));

        response.IsSuccessStatusCode.Should().BeTrue();

        var (rows, _) = await ReadPageAsync<EnrollmentYearRow>(
            await admin.GetAsync($"/api/v1/enrollments?studentId={world.StudentId}"));
        rows.Should().HaveCount(2);
        rows.Select(r => r.AcademicYearId)
            .Should().BeEquivalentTo(new[] { world.AcademicYearId, nextYear.Id });
    }

    [Fact]
    public async Task EnrollmentsCanBeFilteredByAcademicYear()
    {
        var world = await ProvisionWorldAsync("ay-filter");
        using var admin = await SignInAsAdminAsync();

        var nextYear = await CreateYearAsync(admin, Tag("ay-filt2"));
        var enrol = await admin.PostAsJsonAsync("/api/v1/enrollments",
            new CreateEnrollmentRequest(world.StudentId, world.ClassId, nextYear.Id));
        enrol.IsSuccessStatusCode.Should().BeTrue();

        var response = await admin.GetAsync(
            $"/api/v1/enrollments?studentId={world.StudentId}&academicYearId={nextYear.Id}");
        var (rows, _) = await ReadPageAsync<EnrollmentYearRow>(response);

        rows.Should().ContainSingle().Which.AcademicYearId.Should().Be(nextYear.Id);
    }

    [Fact]
    public async Task EnrollingIntoAYearThatDoesNotExist_Returns404()
    {
        var world = await ProvisionWorldAsync("ay-missing");
        using var admin = await SignInAsAdminAsync();

        var response = await admin.PostAsJsonAsync("/api/v1/enrollments",
            new CreateEnrollmentRequest(world.StudentId, world.ClassId, Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Creating a student without naming a year puts them in the current one — the case the
    /// admin form defaults to and the only sensible reading of an omitted field.
    /// </summary>
    [Fact]
    public async Task CreatingAStudentWithoutAYear_UsesTheCurrentOne()
    {
        var world = await ProvisionWorldAsync("ay-default");
        using var admin = await SignInAsAdminAsync();
        var tag = Tag("ay-def");

        var student = await PostAsync<UserRef>(admin, "/api/v1/users",
            new CreateUserRequest($"{tag}@test.local", $"Student {tag}", TestPassword,
                Role.Student, world.ClassId, null));

        var (rows, _) = await ReadPageAsync<EnrollmentYearRow>(
            await admin.GetAsync($"/api/v1/enrollments?studentId={student.Id}"));

        rows.Should().ContainSingle()
            .Which.AcademicYearId.Should().Be(await CurrentAcademicYearIdAsync(admin));
    }

    // ── Authorization ─────────────────────────────────────────────────────────

    /// <summary>
    /// Reads are open to any signed-in user — a student's own class list names its session —
    /// while writing stays with the admin, like the rest of the reference data.
    /// </summary>
    [Fact]
    public async Task AcademicYears_ReadableByAnyone_WritableOnlyByAdmin()
    {
        var world = await ProvisionWorldAsync("ay-role");
        using var student = await SignInAsync(world.StudentEmail);
        using var teacher = await SignInAsync(world.TeacherEmail);

        (await student.GetAsync("/api/v1/academic-years")).StatusCode
            .Should().Be(HttpStatusCode.OK);
        (await teacher.GetAsync("/api/v1/academic-years")).StatusCode
            .Should().Be(HttpStatusCode.OK);

        var body = new CreateAcademicYearRequest(Tag("ay-nope"), Start, End, false);
        (await student.PostAsJsonAsync("/api/v1/academic-years", body)).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
        (await teacher.PostAsJsonAsync("/api/v1/academic-years", body)).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
        (await teacher.DeleteAsync($"/api/v1/academic-years/{world.AcademicYearId}")).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
    }

    private sealed record AcademicYearRow(
        Guid Id, string Name, DateOnly StartDate, DateOnly EndDate, bool IsCurrent, int EnrollmentCount);

    private sealed record EnrollmentYearRow(Guid Id, Guid ClassId, Guid AcademicYearId);
}
