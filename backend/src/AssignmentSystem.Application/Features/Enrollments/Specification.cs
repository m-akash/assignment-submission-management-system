using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.Enrollments;

namespace AssignmentSystem.Application.Features.Enrollments;

internal sealed class EnrollmentWithDetailsSpecification : Specification<StudentEnrollment>
{
    public EnrollmentWithDetailsSpecification(Guid id)
    {
        Criteria = e => e.Id == id;
        AddInclude(e => e.Student);
        AddInclude(e => e.Class);
    }
}

internal sealed class EnrollmentDuplicateSpecification : Specification<StudentEnrollment>
{
    public EnrollmentDuplicateSpecification(Guid studentId, Guid classId)
    {
        Criteria = e => e.StudentId == studentId && e.ClassId == classId;
    }
}

internal sealed class EnrollmentsByStudentSpecification : Specification<StudentEnrollment>
{
    public EnrollmentsByStudentSpecification(Guid studentId)
    {
        Criteria = e => e.StudentId == studentId;
    }
}

internal sealed class EnrollmentsPagedSpecification : Specification<StudentEnrollment>
{
    public EnrollmentsPagedSpecification(
        Guid? studentId,
        Guid? classId,
        string? search,
        int page,
        int pageSize,
        IReadOnlyCollection<Guid>? allowedClassIds = null)
    {
        ApplyNoTracking();
        AddInclude(e => e.Student);
        AddInclude(e => e.Class);
        ApplyOrderBy(e => e.Class.Level);
        ApplyThenBy(e => e.Student.FullName);
        ApplyPaging(page, pageSize);

        var searchLower = search?.Trim().ToLowerInvariant();
        // Captured so the EF-translated expression tree can close over it. A null set means
        // "no class restriction" (admin); an empty HashSet means "the caller is allowed to see
        // no classes" and so matches nothing. restrictByClass flips the null check out of the
        // expression tree, which cannot contain an `is null` pattern.
        var classIdSet = allowedClassIds is null || allowedClassIds.Count == 0
            ? null
            : new HashSet<Guid>(allowedClassIds);
        var restrictByClass = classIdSet is not null;

        // ToLower() (not ToLowerInvariant()) below: this Criteria is an expression tree that EF
        // Core translates to SQL LOWER(...), which ToLowerInvariant() cannot be translated to.
        // The column value never touches client culture, so the CA1304/CA1311 concern doesn't apply.
#pragma warning disable CA1304, CA1311
        Criteria = e =>
            (!studentId.HasValue || e.StudentId == studentId.Value) &&
            (!classId.HasValue || e.ClassId == classId.Value) &&
            (!restrictByClass || (classIdSet != null && classIdSet.Contains(e.ClassId))) &&
            (string.IsNullOrWhiteSpace(searchLower) ||
             e.Student.FullName.ToLower().Contains(searchLower) ||
             e.Student.Email.Value.Contains(searchLower) ||
             (e.Student.StudentId != null && e.Student.StudentId.ToLower().Contains(searchLower)) ||
             e.Class.Name.ToLower().Contains(searchLower));
#pragma warning restore CA1304, CA1311
    }
}
