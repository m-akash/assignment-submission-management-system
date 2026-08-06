using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.Assignments;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Domain.Submissions;
using AssignmentSystem.Shared.Common;

namespace AssignmentSystem.Application.Common.Authorization;

internal sealed class AssignmentAccess : IAssignmentAccess
{
    private static readonly Error Denied =
        Error.Forbidden("Assignment.Forbidden", "You do not have permission to access this assignment.");

    private readonly IClassRosterRepository _roster;
    private readonly ICurrentUser _currentUser;

    public AssignmentAccess(IClassRosterRepository roster, ICurrentUser currentUser)
    {
        _roster = roster;
        _currentUser = currentUser;
    }

    public async Task<Error?> CanViewAsync(Assignment assignment, CancellationToken ct = default)
    {
        var callerId = _currentUser.UserId.GetValueOrDefault();

        switch (_currentUser.Role)
        {
            case Role.Admin:
                return null;

            case Role.Teacher:
                return assignment.IsOwnedBy(callerId) ? null : Denied;

            case Role.Student:
                // A draft is invisible to every student, so it is not worth a roster query to
                // find out which draft they were asking about (X3 before B1).
                if (assignment.Status != AssignmentStatus.Published)
                {
                    return Denied;
                }

                // Read per request rather than taken from the token, so an admin moving a
                // student between classes takes effect on their next request (B1).
                return await _roster.IsEnrolledAsync(callerId, assignment.ClassCourse.ClassId, ct)
                    ? null
                    : Denied;

            default:
                return Denied;
        }
    }

    public Error? MustBeAuthor(Assignment assignment) =>
        assignment.IsOwnedBy(_currentUser.UserId.GetValueOrDefault()) ? null : Denied;
}

internal sealed class SubmissionAccess : ISubmissionAccess
{
    private static readonly Error Denied =
        Error.Forbidden("Submission.Forbidden", "You do not have permission to access this submission.");

    private readonly IRepository<Assignment> _assignments;
    private readonly ICurrentUser _currentUser;

    public SubmissionAccess(IRepository<Assignment> assignments, ICurrentUser currentUser)
    {
        _assignments = assignments;
        _currentUser = currentUser;
    }

    public async Task<Error?> CanViewAsync(Submission submission, CancellationToken ct = default)
    {
        var callerId = _currentUser.UserId.GetValueOrDefault();

        switch (_currentUser.Role)
        {
            case Role.Admin:
                return null;

            case Role.Student:
                return submission.IsOwnedBy(callerId) ? null : Denied;

            case Role.Teacher:
                // Reachable through the assignment, not the submission: a teacher's claim on
                // a submission is entirely derived from having authored the work it answers.
                var assignment = await _assignments.GetByIdAsync(submission.AssignmentId, ct);
                return assignment is not null && assignment.IsOwnedBy(callerId) ? null : Denied;

            default:
                return Denied;
        }
    }

    public Error? MustBeSubmitter(Submission submission) =>
        submission.IsOwnedBy(_currentUser.UserId.GetValueOrDefault()) ? null : Denied;
}
