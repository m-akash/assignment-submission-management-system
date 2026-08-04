namespace AssignmentSystem.Application.Abstractions;

/// <summary>
/// The roster queries that don't fit the generic Specification pattern — counting and
/// membership lookups that would otherwise mean loading whole <c>User</c> rows just to
/// count or test them. A specific port alongside the generic
/// <c>IRepository&lt;StudentEnrollment&gt;</c>, per the "specific repositories only where a
/// query is too complex to express as a spec" rule.
/// </summary>
public interface IClassRosterRepository
{
    /// <summary>
    /// Number of enrolled students per class, for exactly the given class ids. Classes
    /// with no students are simply absent from the result — callers should default to 0.
    /// Counts only students whose account is still live (not soft-deleted).
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> GetStudentCountsAsync(
        IReadOnlyCollection<Guid> classIds, CancellationToken ct = default);

    /// <summary>
    /// The next student-id sequence number for an id prefix such as "IX-A": one more than
    /// the highest ever issued under it (1 if none yet).
    ///
    /// Scoped by prefix rather than by class on purpose — the numbers have to be unique
    /// per grade+section, and nothing stops an admin creating two classes that share one.
    /// Looks past soft-deleted students too, so a removed student's number is never
    /// reissued.
    /// </summary>
    Task<int> GetNextStudentSequenceAsync(string studentIdPrefix, CancellationToken ct = default);

    /// <summary>
    /// Every class the student is enrolled in. This is the authoritative answer to rule
    /// B1 and is read per request rather than carried in the access token: an admin
    /// moving a student between classes takes effect immediately instead of waiting for
    /// the student's token to expire.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetEnrolledClassIdsAsync(Guid studentId, CancellationToken ct = default);

    /// <summary>Whether the student is enrolled in the given class (rule B1).</summary>
    Task<bool> IsEnrolledAsync(Guid studentId, Guid classId, CancellationToken ct = default);

    /// <summary>
    /// Email addresses of the active, non-deleted students enrolled in a class — the
    /// recipient list for an "assignment published" notification. Deactivated accounts
    /// are excluded: they cannot log in to act on it.
    /// </summary>
    Task<IReadOnlyList<NotificationRecipient>> GetClassRecipientsAsync(
        Guid classId, CancellationToken ct = default);
}

/// <summary>Just enough of a user to address an email to them.</summary>
public sealed record NotificationRecipient(Guid UserId, string Email, string FullName);
