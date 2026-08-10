using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using AssignmentSystem.Application.Features.Assignments;
using Xunit;

namespace AssignmentSystem.Api.Tests;

/// <summary>
/// The assignment list's search box against every column the list renders. One box has to
/// reach text, numbers and the status enum alike, so each shape is asserted through the API
/// rather than against the expression — a criteria arm the database provider cannot translate
/// fails only at query time.
/// </summary>
public sealed class AssignmentSearchTests : IntegrationTestBase
{
    public AssignmentSearchTests(ApiFactory api) : base(api) { }

    [Fact]
    public async Task Search_ShouldMatchEveryColumnTheListShows()
    {
        var world = await ProvisionWorldAsync("asrch");
        using var teacher = await SignInAsync(world.TeacherEmail);

        // A teacher sees only their own work, and this world's teacher has written exactly
        // one assignment — so every term below either finds that row or finds nothing, with
        // no risk of another test's fixtures answering instead.
        var created = await CreateAssignmentAsync(
            teacher, world.ClassCourseId, title: "Kinetics brief", maxMarks: 77.5m);

        var terms = new Dictionary<string, string>
        {
            ["title"] = "Kinetics",
            ["description"] = "every question",
            ["course name"] = created.CourseName,
            ["course code"] = created.CourseCode,
            ["teacher"] = created.TeacherName,
            ["section"] = created.ClassSection!,
            ["grade"] = created.ClassLevel.ToString(CultureInfo.InvariantCulture),
            ["marks"] = created.MaxMarks.ToString(CultureInfo.InvariantCulture),
            ["status"] = "draft",
        };

        foreach (var (column, term) in terms)
        {
            var found = await FindAsync(teacher, term);
            found.Should().ContainSingle($"'{term}' is this assignment's {column}")
                .Which.Id.Should().Be(created.Id);
        }
    }

    [Fact]
    public async Task Search_ShouldNotMatchAColumnTheAssignmentDoesNotHave()
    {
        var world = await ProvisionWorldAsync("asrchno");
        using var teacher = await SignInAsync(world.TeacherEmail);

        var created = await CreateAssignmentAsync(
            teacher, world.ClassCourseId, title: "Kinetics brief", maxMarks: 77.5m);

        // The row is a draft worth 77.5 in grade 8. A term of the right *shape* for a column
        // but the wrong value must not match it — which is what separates a real comparison
        // from an arm that quietly matches everything.
        //
        // The number is a run of nines rather than something like "88": a world's names carry
        // a generated hex tag, so a short digit string can turn up inside the course code or
        // the section by chance, and the test would then fail on the text arms it is not
        // about. Nothing here is longer than eight hex characters, so nine nines cannot occur.
        foreach (var term in new[] { "published", "999999999" })
        {
            var found = await FindAsync(teacher, term);
            found.Should().BeEmpty($"'{term}' is not this assignment's status, grade or marks");
        }

        created.Status.Should().Be(Domain.Enums.AssignmentStatus.Draft);
    }

    private static async Task<List<AssignmentDto>> FindAsync(HttpClient client, string search)
    {
        var response = await client.GetAsync(
            $"/api/v1/assignments?pageSize=100&search={Uri.EscapeDataString(search)}");

        response.EnsureSuccessStatusCode();
        var (items, _) = await ReadPageAsync<AssignmentDto>(response);
        return items;
    }
}
