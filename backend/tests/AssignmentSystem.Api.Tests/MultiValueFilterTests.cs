using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace AssignmentSystem.Api.Tests;

/// <summary>
/// Multi-value filtering on the list endpoints.
///
/// Every narrowing filter binds from its singular query parameter repeated —
/// <c>?role=Teacher&amp;role=Student</c> — and matches their union. Two things are worth
/// proving against a real database rather than in isolation: that the repeated parameter
/// reaches the handler at all (it is bound by name, not by the C# parameter's name), and
/// that the resulting <c>Contains</c> translates to SQL instead of silently falling back to
/// client evaluation, which paging would then slice wrongly.
///
/// The assertions are written against pagination totals wherever the suite's shared
/// database means a single page cannot be assumed to hold every match.
/// </summary>
public sealed class MultiValueFilterTests : IntegrationTestBase
{
    public MultiValueFilterTests(ApiFactory api) : base(api) { }

    private sealed record UserRow(Guid Id, string Role);
    private sealed record OfferingRow(Guid Id);
    private sealed record AssignmentRow(Guid Id, string Status);
    private sealed record EnrollmentRow(Guid Id);

    private static async Task<(List<T> Items, int Total)> ListAsync<T>(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var (items, pagination) = await ReadPageAsync<T>(response);
        return (items, pagination.Total);
    }

    [Fact]
    public async Task ARepeatedFilter_MatchesTheUnionOfItsValues()
    {
        using var admin = await SignInAsAdminAsync();

        var (_, teachers) = await ListAsync<UserRow>(admin, "/api/v1/users?role=Teacher");
        var (_, students) = await ListAsync<UserRow>(admin, "/api/v1/users?role=Student");
        var (page, both) = await ListAsync<UserRow>(admin, "/api/v1/users?role=Teacher&role=Student&pageSize=100");

        // A union, not an intersection and not a last-one-wins: nobody holds two roles, so
        // the two totals have to add up exactly.
        both.Should().Be(teachers + students);
        page.Should().NotBeEmpty();
        page.Should().OnlyContain(u => u.Role == "Teacher" || u.Role == "Student");
    }

    [Fact]
    public async Task ASingleValue_StillBindsToTheWidenedFilter()
    {
        using var admin = await SignInAsAdminAsync();

        var (page, total) = await ListAsync<UserRow>(admin, "/api/v1/users?role=Admin&pageSize=100");

        // The parameter kept its singular name precisely so links built before it accepted
        // several values — the sidebar's /users?role=Teacher — keep working.
        total.Should().BeGreaterThan(0);
        page.Should().OnlyContain(u => u.Role == "Admin");
    }

    [Fact]
    public async Task ARepeatedIdFilter_SpansSeveralClasses()
    {
        var first = await ProvisionWorldAsync("multi-a");
        var second = await ProvisionWorldAsync("multi-b");
        using var admin = await SignInAsAdminAsync();

        // Each world enrols its own student in its own class, so the two sets are disjoint.
        var (_, onlyFirst) = await ListAsync<UserRow>(admin, $"/api/v1/users?classId={first.ClassId}");
        var (_, onlySecond) = await ListAsync<UserRow>(admin, $"/api/v1/users?classId={second.ClassId}");
        var (rows, together) = await ListAsync<UserRow>(
            admin, $"/api/v1/users?classId={first.ClassId}&classId={second.ClassId}&pageSize=100");

        onlyFirst.Should().Be(1);
        onlySecond.Should().Be(1);
        together.Should().Be(2);
        rows.Select(u => u.Id).Should().BeEquivalentTo([first.StudentId, second.StudentId]);
    }

    [Fact]
    public async Task ARepeatedFilter_NarrowsOfferingsToTheClassesNamed()
    {
        var first = await ProvisionWorldAsync("multi-c");
        var second = await ProvisionWorldAsync("multi-d");
        using var admin = await SignInAsAdminAsync();

        var (rows, total) = await ListAsync<OfferingRow>(
            admin, $"/api/v1/class-courses?classId={first.ClassId}&classId={second.ClassId}&pageSize=100");

        total.Should().Be(2, "each world adds exactly one course to its own class");
        rows.Select(o => o.Id).Should().BeEquivalentTo([first.ClassCourseId, second.ClassCourseId]);
    }

    [Fact]
    public async Task ARepeatedStatusFilter_ReturnsEveryStatusNamed()
    {
        var world = await ProvisionWorldAsync("multi-e");
        using var teacher = await SignInAsync(world.TeacherEmail);

        var draft = await CreateAssignmentAsync(teacher, world.ClassCourseId, "Draft one");
        var published = await CreatePublishedAssignmentAsync(teacher, world.ClassCourseId, "Published one");

        // Scoped to the teacher server-side, so this list is exactly the two above.
        var (draftsOnly, _) = await ListAsync<AssignmentRow>(teacher, "/api/v1/assignments?status=Draft");
        var (rows, _) = await ListAsync<AssignmentRow>(
            teacher, "/api/v1/assignments?status=Draft&status=Published&pageSize=100");

        draftsOnly.Select(a => a.Id).Should().BeEquivalentTo([draft.Id]);
        rows.Select(a => a.Id).Should().BeEquivalentTo([draft.Id, published.Id]);
    }

    [Fact]
    public async Task ATeacher_CannotWidenARepeatedFilterOntoAClassTheyDoNotTeach()
    {
        var mine = await ProvisionWorldAsync("multi-f");
        var theirs = await ProvisionWorldAsync("multi-g");
        using var teacher = await SignInAsync(mine.TeacherEmail);

        var ownClass = await teacher.GetAsync($"/api/v1/enrollments?classId={mine.ClassId}");
        var mixed = await teacher.GetAsync(
            $"/api/v1/enrollments?classId={mine.ClassId}&classId={theirs.ClassId}");

        ownClass.StatusCode.Should().Be(HttpStatusCode.OK);
        // Refused outright rather than quietly narrowed to the taught subset: naming a class
        // they do not teach is an attempt to widen, and it got a 403 as a lone value too.
        mixed.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ATeacher_SeesTheUnionOfTheirOwnClassesWhenTheyNameSeveral()
    {
        var world = await ProvisionWorldAsync("multi-h");
        var other = await ProvisionWorldAsync("multi-i");
        using var admin = await SignInAsAdminAsync();

        // Give this teacher a second class to ask about. It has to be a brand-new offering:
        // the other world already put its own teacher on its offering, and an offering takes
        // at most one teacher.
        var offering = await PostAsync<OfferingRow>(admin, "/api/v1/class-courses", new
        {
            classId = other.ClassId,
            courseId = world.CourseId,
        });
        await PostAsync<OfferingRow>(admin, "/api/v1/teacher-assignments", new
        {
            teacherId = world.TeacherId,
            classCourseId = offering.Id,
        });

        using var teacher = await SignInAsync(world.TeacherEmail);
        var (rows, total) = await ListAsync<EnrollmentRow>(
            teacher, $"/api/v1/enrollments?classId={world.ClassId}&classId={other.ClassId}&pageSize=100");

        total.Should().Be(2, "each world enrols one student in its own class");
        rows.Should().HaveCount(2);
    }
}
