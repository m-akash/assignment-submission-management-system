using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.Courses;

namespace AssignmentSystem.Application.Features.Courses;

internal sealed class CourseByCodeSpecification : Specification<Course>
{
    public CourseByCodeSpecification(string code)
    {
        var normalized = code.Trim().ToUpperInvariant();
        Criteria = s => s.Code == normalized;
    }
}

internal sealed class CourseWithDepartmentByIdSpecification : Specification<Course>
{
    public CourseWithDepartmentByIdSpecification(Guid id)
    {
        Criteria = s => s.Id == id;
        AddInclude(s => s.Department);
        AddInclude(s => s.Group!);
    }
}

internal sealed class CoursesPagedSpecification : Specification<Course>
{
    public CoursesPagedSpecification(string? search, Guid? departmentId, int page, int pageSize)
    {
        ApplyNoTracking();
        AddInclude(s => s.Department);
        AddInclude(s => s.Group!);
        ApplyOrderBy(s => s.Name);
        ApplyPaging(page, pageSize);

        var searchLower = search?.Trim().ToLowerInvariant();

        // ToLower() (not ToLowerInvariant()) below: this Criteria is an expression tree that EF
        // Core translates to SQL LOWER(...), which ToLowerInvariant() cannot be translated to.
        // The column value never touches client culture, so the CA1304/CA1311 concern doesn't apply.
#pragma warning disable CA1304, CA1311
        Criteria = s =>
            (!departmentId.HasValue || s.DepartmentId == departmentId.Value) &&
            (string.IsNullOrWhiteSpace(searchLower) ||
             s.Name.ToLower().Contains(searchLower) ||
             s.Code.ToLower().Contains(searchLower));
#pragma warning restore CA1304, CA1311
    }
}
