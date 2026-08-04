using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.Assignments;
using AssignmentSystem.Domain.Enums;

namespace AssignmentSystem.Application.Features.Assignments;

/// <summary>
/// An assignment with everything the DTO and the authorization checks need. The offering
/// is included two levels deep because the class and course names live on the far side of
/// it, and because rule B1 needs the offering's class id to test enrollment.
/// </summary>
internal sealed class AssignmentWithDetailsSpecification : Specification<Assignment>
{
    public AssignmentWithDetailsSpecification(Guid id)
    {
        Criteria = a => a.Id == id;
        AddInclude(a => a.Teacher);
        AddInclude("ClassCourse.Class");
        AddInclude("ClassCourse.Course");
        AddInclude(a => a.Files);
    }
}

/// <summary>
/// The scope of an assignment without its files — for the write paths that need to answer
/// "which class and course is this for?" before they mutate something, and for composing
/// notification bodies.
/// </summary>
internal sealed class AssignmentWithScopeSpecification : Specification<Assignment>
{
    public AssignmentWithScopeSpecification(Guid id)
    {
        Criteria = a => a.Id == id;
        AddInclude("ClassCourse.Class");
        AddInclude("ClassCourse.Course");
    }
}

internal sealed class AssignmentsPagedSpecification : Specification<Assignment>
{
    public AssignmentsPagedSpecification(
        Guid? classId,
        Guid? courseId,
        Guid? classCourseId,
        IReadOnlyList<Guid>? restrictToClassIds,
        Guid? teacherId,
        AssignmentStatus? status,
        string? search,
        int page,
        int pageSize)
    {
        ApplyNoTracking();
        AddInclude(a => a.Teacher);
        AddInclude("ClassCourse.Class");
        AddInclude("ClassCourse.Course");
        AddInclude(a => a.Files);
        ApplyOrderByDescending(a => a.CreatedAtUtc);
        ApplyPaging(page, pageSize);

        var searchLower = search?.Trim().ToLowerInvariant();

        // ToLower() (not ToLowerInvariant()) below: this Criteria is an expression tree that EF
        // Core translates to SQL LOWER(...), which ToLowerInvariant() cannot be translated to.
        // The column value never touches client culture, so the CA1304/CA1311 concern doesn't apply.
        //
        // restrictToClassIds is the student scope (rule B1): the classes they are enrolled
        // in. A student with no enrollment must see nothing, so an EMPTY list has to match
        // nothing — which is why the bypass tests for null rather than for emptiness.
#pragma warning disable CA1304, CA1311
        Criteria = a =>
            (!classCourseId.HasValue || a.ClassCourseId == classCourseId.Value) &&
            (!classId.HasValue || a.ClassCourse.ClassId == classId.Value) &&
            (!courseId.HasValue || a.ClassCourse.CourseId == courseId.Value) &&
            (restrictToClassIds == null || restrictToClassIds.Contains(a.ClassCourse.ClassId)) &&
            (!teacherId.HasValue || a.TeacherId == teacherId.Value) &&
            (!status.HasValue || a.Status == status.Value) &&
            (string.IsNullOrWhiteSpace(searchLower) ||
             a.Title.ToLower().Contains(searchLower) ||
             a.Description.ToLower().Contains(searchLower));
#pragma warning restore CA1304, CA1311
    }
}
