using AssignmentSystem.Domain.Assignments;
using AssignmentSystem.Domain.Submissions;
using AssignmentSystem.Shared.Common;

namespace AssignmentSystem.Application.Common.Authorization;

/// <summary>
/// Resource-level authorization for an assignment.
///
/// The decorator pipeline answers "may this role send this message?"; it cannot answer
/// "is this *your* assignment?", because that needs the row loaded. These checks are the
/// other half, and they live here rather than in each handler because the same two
/// questions were being asked — and phrased slightly differently — in six places.
///
/// Every method returns the <see cref="Error"/> to fail with, or <c>null</c> to allow,
/// matching the shape <c>AuthorizationPolicy.Check</c> uses.
/// </summary>
public interface IAssignmentAccess
{
    /// <summary>
    /// May the caller see this assignment, and anything hanging off it (attachments,
    /// submissions)? An admin sees everything; a teacher sees their own work; a student
    /// sees published work for a class they are currently enrolled in.
    /// </summary>
    Task<Error?> CanViewAsync(Assignment assignment, CancellationToken ct = default);

    /// <summary>
    /// Is the caller the assignment's author? Used for the manage operations — update,
    /// publish, delete, grade, attach. The role gate has already run, so this is purely
    /// the ownership half.
    /// </summary>
    Error? MustBeAuthor(Assignment assignment);
}

/// <summary>
/// Resource-level authorization for a submission: a student may reach their own, a teacher
/// may reach submissions made against an assignment they authored, an admin may reach any.
/// </summary>
public interface ISubmissionAccess
{
    Task<Error?> CanViewAsync(Submission submission, CancellationToken ct = default);

    /// <summary>Is the caller the student who made this submission?</summary>
    Error? MustBeSubmitter(Submission submission);
}
