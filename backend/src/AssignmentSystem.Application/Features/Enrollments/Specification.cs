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
        Guid? studentId, Guid? classId, string? search, int page, int pageSize)
    {
        ApplyNoTracking();
        AddInclude(e => e.Student);
        AddInclude(e => e.Class);
        ApplyOrderBy(e => e.Class.Level);
        ApplyThenBy(e => e.Student.FullName);
        ApplyPaging(page, pageSize);

        var searchLower = search?.Trim().ToLowerInvariant();

        // ToLower() (not ToLowerInvariant()) below: this Criteria is an expression tree that EF
        // Core translates to SQL LOWER(...), which ToLowerInvariant() cannot be translated to.
        // The column value never touches client culture, so the CA1304/CA1311 concern doesn't apply.
#pragma warning disable CA1304, CA1311
        Criteria = e =>
            (!studentId.HasValue || e.StudentId == studentId.Value) &&
            (!classId.HasValue || e.ClassId == classId.Value) &&
            (string.IsNullOrWhiteSpace(searchLower) ||
             e.Student.FullName.ToLower().Contains(searchLower) ||
             e.Student.Email.Value.Contains(searchLower) ||
             (e.Student.StudentId != null && e.Student.StudentId.ToLower().Contains(searchLower)) ||
             e.Class.Name.ToLower().Contains(searchLower));
#pragma warning restore CA1304, CA1311
    }
}
