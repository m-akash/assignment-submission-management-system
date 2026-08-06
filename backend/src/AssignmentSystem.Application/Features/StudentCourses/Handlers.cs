using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Domain.TeacherAssignments;
using AssignmentSystem.Shared.Common;

namespace AssignmentSystem.Application.Features.StudentCourses;

/// <summary>
/// The courses the signed-in student is enrolled in, each with the teacher(s) for its
/// offering. Self-scoped: the student id comes from <see cref="ICurrentUser"/> and the
/// class membership is read through <see cref="IClassRosterRepository"/> (the authoritative
/// rule-B1 read, not a token claim), so an admin moving a student between classes takes
/// effect on the next request.
/// </summary>
public sealed class GetStudentCoursesHandler : IQueryHandler<GetStudentCoursesQuery, PageResult<StudentCourseDto>>
{
    private readonly IRepository<TeacherAssignment> _teacherAssignmentRepository;
    private readonly IClassRosterRepository _classRosterRepository;
    private readonly ICurrentUser _currentUser;

    public GetStudentCoursesHandler(
        IRepository<TeacherAssignment> teacherAssignmentRepository,
        IClassRosterRepository classRosterRepository,
        ICurrentUser currentUser)
    {
        _teacherAssignmentRepository = teacherAssignmentRepository;
        _classRosterRepository = classRosterRepository;
        _currentUser = currentUser;
    }

    public async Task<Result<PageResult<StudentCourseDto>>> HandleAsync(GetStudentCoursesQuery query, CancellationToken ct = default)
    {
        // The role is [RequiresRole(Role.Student)] on the query and enforced by the pipeline.
        // The id is still checked: a token that authenticates but carries no subject claim
        // would otherwise fall through to querying the roster for Guid.Empty.
        if (_currentUser.UserId is null)
        {
            return Result<PageResult<StudentCourseDto>>.Failure(Error.Unauthorized(
                "StudentCourses.NoIdentity", "Your session does not identify a student account."));
        }

        var classIds = await _classRosterRepository.GetEnrolledClassIdsAsync(_currentUser.UserId.Value, ct);
        if (classIds.Count == 0)
        {
            return PageResult<StudentCourseDto>.Empty(query.Page, query.PageSize);
        }

        // One row per offering, but a TeacherAssignment is one row per teacher — so fetch the
        // (small) set for the student's classes and group in memory before paging.
        var spec = new StudentCoursesByClassesSpecification(classIds);
        var assignments = await _teacherAssignmentRepository.ListAsync(spec, ct);

        var courses = assignments
            .GroupBy(ta => ta.ClassCourseId)
            .Select(group =>
            {
                var first = group.First();
                var offering = first.ClassCourse;
                return new StudentCourseDto(
                    Id: offering.Id,
                    CourseId: offering.CourseId,
                    CourseName: offering.Course.Name,
                    CourseCode: offering.Course.Code,
                    ClassId: offering.ClassId,
                    ClassName: offering.Class.Name,
                    ClassLevel: offering.Class.Level,
                    ClassSection: offering.Class.Section,
                    Teachers: group
                        .Select(ta => new StudentCourseTeacherDto(ta.TeacherId, ta.Teacher.FullName, ta.Teacher.Email.Value))
                        .OrderBy(t => t.TeacherName)
                        .ToList());
            })
            .ToList();

        // Search runs in memory (after the EF query and grouping), so invariant culture is the
        // correct, deterministic choice — unlike the spec-based searches elsewhere in this
        // layer, this string never becomes an EF-translated expression tree.
#pragma warning disable CA1311
        var search = query.Search?.Trim().ToLowerInvariant();
#pragma warning restore CA1311
        var filtered = string.IsNullOrEmpty(search)
            ? courses
            : courses.Where(c =>
                c.CourseName.ToLowerInvariant().Contains(search) ||
                c.CourseCode.ToLowerInvariant().Contains(search) ||
                c.ClassName.ToLowerInvariant().Contains(search) ||
                c.Teachers.Any(t => t.TeacherName.ToLowerInvariant().Contains(search))).ToList();

        // Sorted in memory for the same reason the search is: the rows are grouped after the
        // EF query, so there is no IQueryable left to order. The allow-list is spelled out
        // rather than resolved by reflection, matching how SortMap guards the spec-based
        // endpoints. ClassName then CourseName is the natural order when nothing is asked for.
        var sorted = SortStudentCourses(filtered, query.SortBy, query.SortDir);

        var total = sorted.Count;
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Max(query.PageSize, 1);
        var pageItems = sorted
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PageResult<StudentCourseDto>(pageItems, page, pageSize, total);
    }

    private static List<StudentCourseDto> SortStudentCourses(
        List<StudentCourseDto> courses, string? sortBy, string? sortDir)
    {
        Func<StudentCourseDto, string> key = sortBy?.Trim().ToLowerInvariant() switch
        {
            "course" => c => c.CourseName,
            "coursecode" => c => c.CourseCode,
            "class" => c => c.ClassName,
            "teacher" => c => c.Teachers.Count > 0 ? c.Teachers[0].TeacherName : string.Empty,
            _ => null!,
        };

        if (key is null)
        {
            return [.. courses.OrderBy(c => c.ClassName, StringComparer.OrdinalIgnoreCase)
                              .ThenBy(c => c.CourseName, StringComparer.OrdinalIgnoreCase)];
        }

        var ordered = SortDirection.IsDescending(sortDir)
            ? courses.OrderByDescending(key, StringComparer.OrdinalIgnoreCase)
            : courses.OrderBy(key, StringComparer.OrdinalIgnoreCase);

        // Offering id as the tiebreaker, for the same reason SortMap carries one: without it,
        // two courses with the same name can swap places between pages.
        return [.. ordered.ThenBy(c => c.Id)];
    }
}
