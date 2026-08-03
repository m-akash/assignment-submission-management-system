namespace AssignmentSystem.Application.Abstractions;

/// <summary>
/// The one Class query that doesn't fit the generic Specification pattern: counting
/// students per class without loading full <c>User</c> rows just to count them.
/// A specific port alongside the generic <c>IRepository&lt;Class&gt;</c>, per the
/// "specific repositories only where a query is too complex to express as a spec" rule.
/// </summary>
public interface IClassRosterRepository
{
    /// <summary>
    /// Number of students per class, for exactly the given class ids. Classes with no
    /// students are simply absent from the result — callers should default to 0.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> GetStudentCountsAsync(
        IReadOnlyCollection<Guid> classIds, CancellationToken ct = default);
}
