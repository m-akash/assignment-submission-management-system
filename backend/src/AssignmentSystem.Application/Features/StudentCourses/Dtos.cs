using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Shared.Common;

namespace AssignmentSystem.Application.Features.StudentCourses;

/// <summary>
/// One teacher assigned to a course the student takes. An offering may have more than
/// one teacher, so the course carries a list rather than a single name.
/// </summary>
public sealed record StudentCourseTeacherDto(
    Guid TeacherId,
    string TeacherName,
    string TeacherEmail
);

/// <summary>
/// One course this student is enrolled in, flattened out of its offering with the class
/// and the teacher(s) for that offering. The student reaches courses through their class
/// (StudentEnrollment -> Class -> ClassCourse -> Course) and the teacher through
/// TeacherAssignment, so the offering id (<see cref="Id"/>) is the stable row key.
/// </summary>
public sealed record StudentCourseDto(
    Guid Id,
    Guid CourseId,
    string CourseName,
    string CourseCode,
    Guid ClassId,
    string ClassName,
    int ClassLevel,
    string? ClassSection,
    IReadOnlyList<StudentCourseTeacherDto> Teachers
);

/// <summary>
/// The signed-in student's own courses. Self-scoped server-side: no student id travels
/// in the query, mirroring the rule-B1 read pattern used by the submissions endpoints.
/// </summary>
public sealed record GetStudentCoursesQuery(
    string? Search = null,
    /// <summary>One of: course, courseCode, class, teacher. Anything else keeps the natural order.</summary>
    int Page = 1,
    int PageSize = 50
) : IQuery<PageResult<StudentCourseDto>>;
