using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Shared.Common;

namespace AssignmentSystem.Application.Features.TeacherAssignments;

public sealed record TeacherAssignmentDto(
    Guid Id,
    Guid TeacherId,
    string TeacherName,
    string TeacherEmail,
    Guid SubjectId,
    string SubjectName,
    string SubjectCode,
    Guid ClassId,
    string ClassName
);

public sealed record CreateTeacherAssignmentCommand(
    Guid TeacherId,
    Guid SubjectId,
    Guid ClassId
) : ICommand<TeacherAssignmentDto>;

public sealed record DeleteTeacherAssignmentCommand(Guid Id) : ICommand;

public sealed record GetTeacherAssignmentsQuery(
    Guid? TeacherId = null,
    Guid? SubjectId = null,
    Guid? ClassId = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20
) : IQuery<PageResult<TeacherAssignmentDto>>;
