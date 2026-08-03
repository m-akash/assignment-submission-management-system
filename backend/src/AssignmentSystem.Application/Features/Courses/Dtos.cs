using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Shared.Common;

namespace AssignmentSystem.Application.Features.Subjects;

public sealed record SubjectDto(
    Guid Id,
    string Name,
    string Code
);

public sealed record CreateSubjectCommand(
    string Name,
    string Code
) : ICommand<SubjectDto>;

public sealed record UpdateSubjectCommand(
    Guid Id,
    string Name,
    string Code
) : ICommand<SubjectDto>;

public sealed record DeleteSubjectCommand(Guid Id) : ICommand;

public sealed record GetSubjectByIdQuery(Guid Id) : IQuery<SubjectDto>;

public sealed record GetSubjectsQuery(
    string? Search = null,
    int Page = 1,
    int PageSize = 20
) : IQuery<PageResult<SubjectDto>>;
