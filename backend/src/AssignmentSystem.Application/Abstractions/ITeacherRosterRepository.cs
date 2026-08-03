namespace AssignmentSystem.Application.Abstractions;

/// <summary>
/// The Teacher-side equivalent of <see cref="IClassRosterRepository"/>: issuing the
/// next teacher-id sequence number for a department (a course, reused as "department"
/// here — see <c>ApplicationUser.DepartmentId</c>).
/// </summary>
public interface ITeacherRosterRepository
{
    /// <summary>
    /// The next teacher-id sequence number for a department: one more than the highest
    /// ever issued for it (1 if none yet). Looks past soft-deleted teachers too, so a
    /// removed teacher's number is never reissued.
    /// </summary>
    Task<int> GetNextTeacherSequenceAsync(Guid departmentId, CancellationToken ct = default);
}
