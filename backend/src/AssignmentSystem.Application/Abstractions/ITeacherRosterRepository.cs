namespace AssignmentSystem.Application.Abstractions;

/// <summary>The Teacher-side equivalent of <see cref="IClassRosterRepository"/>: issuing
/// the next teacher-id sequence number.</summary>
public interface ITeacherRosterRepository
{
    /// <summary>
    /// The next teacher-id sequence number: one more than the highest ever issued.
    /// Looks past soft-deleted teachers too, so a removed teacher's number is never
    /// reissued.
    /// </summary>
    Task<int> GetNextTeacherSequenceAsync(CancellationToken ct = default);
}
