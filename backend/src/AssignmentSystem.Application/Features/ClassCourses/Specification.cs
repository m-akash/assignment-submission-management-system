using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.ClassCourses;

namespace AssignmentSystem.Application.Features.ClassCourses;

/// <summary>
/// An offering with both sides loaded. Used well beyond this feature — anything that
/// needs to render or describe an assignment's scope (including the notification bodies)
/// needs the class and course names, not just their ids.
/// </summary>
internal sealed class ClassCourseWithDetailsSpecification : Specification<ClassCourse>
{
    public ClassCourseWithDetailsSpecification(Guid id)
    {
        Criteria = cc => cc.Id == id;
        AddInclude(cc => cc.Class);
        AddInclude(cc => cc.Course);
    }
}

internal sealed class ClassCourseDuplicateSpecification : Specification<ClassCourse>
{
    public ClassCourseDuplicateSpecification(Guid classId, Guid courseId)
    {
        Criteria = cc => cc.ClassId == classId && cc.CourseId == courseId;
    }
}

internal sealed class ClassCoursesPagedSpecification : Specification<ClassCourse>
{
    public ClassCoursesPagedSpecification(
        Guid? classId, Guid? courseId, string? search, int page, int pageSize)
    {
        ApplyNoTracking();
        AddInclude(cc => cc.Class);
        AddInclude(cc => cc.Course);
        // By grade then course name: an offering list is read class-by-class, and class
        // names carry Roman numerals, so sorting them as text puts "Class IX" before "Class VI".
        ApplyOrderBy(cc => cc.Class.Level);
        ApplyThenBy(cc => cc.Course.Name);
        ApplyPaging(page, pageSize);

        var searchLower = search?.Trim().ToLowerInvariant();

        // ToLower() (not ToLowerInvariant()) below: this Criteria is an expression tree that EF
        // Core translates to SQL LOWER(...), which ToLowerInvariant() cannot be translated to.
        // The column value never touches client culture, so the CA1304/CA1311 concern doesn't apply.
#pragma warning disable CA1304, CA1311
        Criteria = cc =>
            (!classId.HasValue || cc.ClassId == classId.Value) &&
            (!courseId.HasValue || cc.CourseId == courseId.Value) &&
            (string.IsNullOrWhiteSpace(searchLower) ||
             cc.Class.Name.ToLower().Contains(searchLower) ||
             cc.Course.Name.ToLower().Contains(searchLower) ||
             cc.Course.Code.ToLower().Contains(searchLower));
#pragma warning restore CA1304, CA1311
    }
}
