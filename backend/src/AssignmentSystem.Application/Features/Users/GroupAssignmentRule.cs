using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.Classes;
using AssignmentSystem.Domain.Groups;
using AssignmentSystem.Shared.Common;

namespace AssignmentSystem.Application.Features.Users;

/// <summary>
/// Whether a student's group fits their class. Groups start at class IX, so a student
/// there must have one and a student below it must not.
///
/// Shared by create and update because both can pair a class with a group, and it lives
/// outside <c>ApplicationUser</c> because deciding it needs the class loaded (and the
/// group looked up), which an entity cannot do.
/// </summary>
internal static class GroupAssignmentRule
{
    /// <summary>Returns null when the class/group pairing is valid.</summary>
    public static async Task<Error?> ValidateAsync(
        Class classObj,
        Guid? groupId,
        IRepository<Group> groupRepository,
        CancellationToken ct)
    {
        if (!classObj.HasGroups)
        {
            return groupId.HasValue
                ? Error.Validation("User.GroupNotApplicable",
                    $"{classObj.Name} does not have groups — they start at class {Class.GroupStartLevel}.")
                : null;
        }

        if (!groupId.HasValue)
        {
            return Error.Validation("User.GroupRequired",
                $"A student in {classObj.Name} must be assigned to a group.");
        }

        var group = await groupRepository.GetByIdAsync(groupId.Value, ct);
        return group is null
            ? Error.NotFound("Group.NotFound", "The specified group was not found.")
            : null;
    }
}
