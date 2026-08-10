using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using AssignmentSystem.Application.Features.Users;
using AssignmentSystem.Domain.Enums;
using Xunit;

namespace AssignmentSystem.Api.Tests;

/// <summary>
/// The user list's search box against the columns the list actually shows. The school id is
/// the one worth pinning down: it is the column a reader is most likely to copy out of the
/// table and paste back into the box, and a name-and-email search could never find it.
/// </summary>
public sealed class UserSearchTests : IntegrationTestBase
{
    public UserSearchTests(ApiFactory api) : base(api) { }

    [Fact]
    public async Task Search_ShouldFindAStudentByTheirSchoolId()
    {
        var world = await ProvisionWorldAsync("usrch");
        using var admin = await SignInAsAdminAsync();

        var student = await FindOneAsync(admin, world.StudentEmail);
        // "8-<section>-001": derived from the class, and this world's class section is unique
        // to it, so the id identifies exactly one student across the whole shared database.
        student.StudentId.Should().NotBeNull();

        var byId = await FindAsync(admin, student.StudentId!);
        byId.Should().ContainSingle("the school id belongs to one student")
            .Which.Id.Should().Be(student.Id);

        // A reader narrowing down rather than pasting the whole id: the grade-and-section
        // prefix is what the column's first characters are.
        var prefix = student.StudentId![..student.StudentId!.LastIndexOf('-')];
        var byPrefix = await FindAsync(admin, prefix);
        byPrefix.Should().ContainSingle("this world's class holds one student")
            .Which.Id.Should().Be(student.Id);
    }

    [Fact]
    public async Task Search_ShouldFindATeacherByTheirStaffId()
    {
        var world = await ProvisionWorldAsync("usrchins");
        using var admin = await SignInAsAdminAsync();

        var teacher = await FindOneAsync(admin, world.TeacherEmail);
        teacher.TeacherId.Should().NotBeNull();

        var found = await FindAsync(admin, teacher.TeacherId!);
        found.Should().Contain(u => u.Id == teacher.Id, "the staff id is a column of this list");
    }

    [Fact]
    public async Task Search_ShouldReachTheGradeSectionAndSessionOfAnEnrollment()
    {
        var world = await ProvisionWorldAsync("usrchenr");
        using var admin = await SignInAsAdminAsync();

        var student = await FindOneAsync(admin, world.StudentEmail);
        var enrollment = student.Classes.Should().ContainSingle().Subject;

        // Each of these is a column the row renders, and none of them is on the user record —
        // they are reached through the enrollment, which is why the arms are an EXISTS.
        foreach (var term in new[]
        {
            enrollment.ClassSection!,
            enrollment.AcademicYearName,
            enrollment.ClassLevel.ToString(CultureInfo.InvariantCulture),
        })
        {
            var found = await FindAsync(admin, term, Role.Student);
            found.Should().Contain(u => u.Id == student.Id, $"'{term}' is on this student's row");
        }
    }

    [Fact]
    public async Task Search_ShouldMatchARoleByName()
    {
        await ProvisionWorldAsync("usrchrole");
        using var admin = await SignInAsAdminAsync();

        // "teach" is nobody's name, email or id — the only thing it can be matching is the
        // role column, and a prefix of it at that.
        var found = await FindAsync(admin, "teach");
        found.Should().NotBeEmpty();
        found.Should().OnlyContain(u => u.Role == Role.Teacher);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>The one user an email address belongs to.</summary>
    private static async Task<UserDto> FindOneAsync(HttpClient admin, string email)
    {
        var found = await FindAsync(admin, email);
        return found.Should().ContainSingle("an email address belongs to one account").Subject;
    }

    private static async Task<List<UserDto>> FindAsync(HttpClient admin, string search, Role? role = null)
    {
        var roleFilter = role is null ? string.Empty : $"&role={role}";
        var response = await admin.GetAsync(
            $"/api/v1/users?pageSize=100{roleFilter}&search={Uri.EscapeDataString(search)}");

        response.EnsureSuccessStatusCode();
        var (items, _) = await ReadPageAsync<UserDto>(response);
        return items;
    }
}
