using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AssignmentSystem.Api.Controllers;
using AssignmentSystem.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace AssignmentSystem.Api.Tests;

/// <summary>
/// Groups (Science / Humanities / Business Studies) are chosen from class IX. So the
/// rule cuts both ways: a student at or above that level must have one, and a student
/// below it must not — a class alone does not say which, the level does.
/// </summary>
public class StudentGroupRuleTests : IntegrationTestBase
{
    public StudentGroupRuleTests(ApiFactory api) : base(api) { }

    [Fact]
    public async Task StudentInClassNine_WithoutAGroup_ShouldBeRejected()
    {
        var (admin, world) = await GroupWorldAsync("grp-req");

        var response = await admin.PostAsJsonAsync("/api/v1/users", NewStudent(world.SeniorClassId, groupId: null));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync()).Should().Contain("must be assigned to a group");
    }

    [Fact]
    public async Task StudentInClassNine_WithAGroup_ShouldSucceed()
    {
        var (admin, world) = await GroupWorldAsync("grp-ok");

        var response = await admin.PostAsJsonAsync("/api/v1/users", NewStudent(world.SeniorClassId, world.GroupId));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<StudentRow>(response);
        created.GroupId.Should().Be(world.GroupId);
    }

    [Fact]
    public async Task StudentBelowClassNine_ShouldHaveNoGroup()
    {
        var (admin, world) = await GroupWorldAsync("grp-none");

        var response = await admin.PostAsJsonAsync("/api/v1/users", NewStudent(world.JuniorClassId, groupId: null));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await ReadAsync<StudentRow>(response)).GroupId.Should().BeNull();
    }

    [Fact]
    public async Task StudentBelowClassNine_GivenAGroup_ShouldBeRejected()
    {
        var (admin, world) = await GroupWorldAsync("grp-na");

        var response = await admin.PostAsJsonAsync("/api/v1/users", NewStudent(world.JuniorClassId, world.GroupId));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync()).Should().Contain("does not have groups");
    }

    /// <summary>Moving up into a grade that has groups starts requiring one.</summary>
    [Fact]
    public async Task MovingAStudentUpIntoClassNine_WithoutAGroup_ShouldBeRejected()
    {
        var (admin, world) = await GroupWorldAsync("grp-move");

        var created = await ReadAsync<StudentRow>(
            await admin.PostAsJsonAsync("/api/v1/users", NewStudent(world.JuniorClassId, groupId: null)));

        var response = await admin.PutAsJsonAsync($"/api/v1/users/{created.Id}",
            new UpdateUserRequest("Moved Student", null, world.SeniorClassId, null, null));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync()).Should().Contain("must be assigned to a group");
    }

    /// <summary>And moving back down clears it, rather than leaving a group that no longer applies.</summary>
    [Fact]
    public async Task MovingAStudentDownBelowClassNine_ShouldClearTheirGroup()
    {
        var (admin, world) = await GroupWorldAsync("grp-clear");

        var created = await ReadAsync<StudentRow>(
            await admin.PostAsJsonAsync("/api/v1/users", NewStudent(world.SeniorClassId, world.GroupId)));
        created.GroupId.Should().Be(world.GroupId);

        var response = await admin.PutAsJsonAsync($"/api/v1/users/{created.Id}",
            new UpdateUserRequest("Moved Student", null, world.JuniorClassId, null, null));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<StudentRow>(response)).GroupId.Should().BeNull();
    }

    // ── Fixture ───────────────────────────────────────────────────────────────

    private static CreateUserRequest NewStudent(Guid classId, Guid? groupId) =>
        new(
            $"{Guid.NewGuid():N}@test.local",
            "Group Rule Student",
            TestPassword,
            Role.Student,
            classId,
            null,
            groupId);

    /// <summary>A group plus one class either side of the group threshold.</summary>
    private async Task<(HttpClient Admin, GroupWorld World)> GroupWorldAsync(string label)
    {
        var tag = $"{label}-{Guid.NewGuid():N}"[..(label.Length + 9)];
        var admin = await SignInAsAdminAsync();

        var junior = await ReadAsync<IdRow>(await admin.PostAsJsonAsync("/api/v1/classes",
            new CreateClassRequest($"Class {tag} junior", 8, tag)));

        var senior = await ReadAsync<IdRow>(await admin.PostAsJsonAsync("/api/v1/classes",
            new CreateClassRequest($"Class {tag} senior", 9, tag)));

        // Group codes are capped at 10 characters, so build one from the guid instead.
        var code = $"G{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var group = await ReadAsync<IdRow>(await admin.PostAsJsonAsync("/api/v1/groups",
            new CreateGroupRequest($"Group {tag}", code)));

        return (admin, new GroupWorld(junior.Id, senior.Id, group.Id));
    }

    private sealed record GroupWorld(Guid JuniorClassId, Guid SeniorClassId, Guid GroupId);
    private sealed record IdRow(Guid Id);
    private sealed record StudentRow(Guid Id, Guid? GroupId, string? StudentId);
}
