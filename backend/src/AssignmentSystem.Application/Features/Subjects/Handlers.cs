using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.Common;
using AssignmentSystem.Domain.Subjects;
using AssignmentSystem.Shared.Common;

namespace AssignmentSystem.Application.Features.Subjects;

public sealed class CreateSubjectHandler : ICommandHandler<CreateSubjectCommand, SubjectDto>
{
    private readonly IRepository<Subject> _subjectRepository;
    private readonly IUnitOfWork _unitOfWork;
    private static readonly SubjectMapper Mapper = new();

    public CreateSubjectHandler(IRepository<Subject> subjectRepository, IUnitOfWork unitOfWork)
    {
        _subjectRepository = subjectRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<SubjectDto>> HandleAsync(CreateSubjectCommand command, CancellationToken ct = default)
    {
        var codeSpec = new SubjectByCodeSpecification(command.Code);
        var codeExists = await _subjectRepository.AnyAsync(codeSpec, ct);
        if (codeExists)
        {
            return Result<SubjectDto>.Failure(Error.Conflict("Subject.CodeAlreadyExists", "A subject with this code already exists."));
        }

        try
        {
            var subject = Subject.Create(command.Name, command.Code);
            await _subjectRepository.AddAsync(subject, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return Mapper.MapToDto(subject);
        }
        catch (DomainException ex)
        {
            return Result<SubjectDto>.Failure(Error.Validation("Subject.Invalid", ex.Message));
        }
    }
}

public sealed class UpdateSubjectHandler : ICommandHandler<UpdateSubjectCommand, SubjectDto>
{
    private readonly IRepository<Subject> _subjectRepository;
    private readonly IUnitOfWork _unitOfWork;
    private static readonly SubjectMapper Mapper = new();

    public UpdateSubjectHandler(IRepository<Subject> subjectRepository, IUnitOfWork unitOfWork)
    {
        _subjectRepository = subjectRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<SubjectDto>> HandleAsync(UpdateSubjectCommand command, CancellationToken ct = default)
    {
        var subject = await _subjectRepository.GetByIdAsync(command.Id, ct);
        if (subject is null)
        {
            return Result<SubjectDto>.Failure(Error.NotFound("Subject.NotFound", "The specified subject was not found."));
        }

        var normalizedCode = command.Code.Trim().ToUpperInvariant();
        if (normalizedCode != subject.Code)
        {
            var codeSpec = new SubjectByCodeSpecification(command.Code);
            var codeExists = await _subjectRepository.AnyAsync(codeSpec, ct);
            if (codeExists)
            {
                return Result<SubjectDto>.Failure(Error.Conflict("Subject.CodeAlreadyExists", "A subject with this code already exists."));
            }
        }

        try
        {
            subject.Update(command.Name, command.Code);
            _subjectRepository.Update(subject);
            await _unitOfWork.SaveChangesAsync(ct);

            return Mapper.MapToDto(subject);
        }
        catch (DomainException ex)
        {
            return Result<SubjectDto>.Failure(Error.Validation("Subject.Invalid", ex.Message));
        }
    }
}

public sealed class DeleteSubjectHandler : ICommandHandler<DeleteSubjectCommand>
{
    private readonly IRepository<Subject> _subjectRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteSubjectHandler(IRepository<Subject> subjectRepository, IUnitOfWork unitOfWork)
    {
        _subjectRepository = subjectRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(DeleteSubjectCommand command, CancellationToken ct = default)
    {
        var subject = await _subjectRepository.GetByIdAsync(command.Id, ct);
        if (subject is null)
        {
            return Result.Failure(Error.NotFound("Subject.NotFound", "The specified subject was not found."));
        }

        _subjectRepository.Remove(subject);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}

public sealed class GetSubjectByIdHandler : IQueryHandler<GetSubjectByIdQuery, SubjectDto>
{
    private readonly IRepository<Subject> _subjectRepository;
    private static readonly SubjectMapper Mapper = new();

    public GetSubjectByIdHandler(IRepository<Subject> subjectRepository)
    {
        _subjectRepository = subjectRepository;
    }

    public async Task<Result<SubjectDto>> HandleAsync(GetSubjectByIdQuery query, CancellationToken ct = default)
    {
        var subject = await _subjectRepository.GetByIdAsync(query.Id, ct);
        if (subject is null)
        {
            return Result<SubjectDto>.Failure(Error.NotFound("Subject.NotFound", "The specified subject was not found."));
        }

        return Mapper.MapToDto(subject);
    }
}

public sealed class GetSubjectsHandler : IQueryHandler<GetSubjectsQuery, PageResult<SubjectDto>>
{
    private readonly IRepository<Subject> _subjectRepository;
    private static readonly SubjectMapper Mapper = new();

    public GetSubjectsHandler(IRepository<Subject> subjectRepository)
    {
        _subjectRepository = subjectRepository;
    }

    public async Task<Result<PageResult<SubjectDto>>> HandleAsync(GetSubjectsQuery query, CancellationToken ct = default)
    {
        var spec = new SubjectsPagedSpecification(query.Search, query.Page, query.PageSize);
        var pagedSubjects = await _subjectRepository.ListPagedAsync(spec, ct);

        var items = pagedSubjects.Items.Select(Mapper.MapToDto).ToList();
        var result = new PageResult<SubjectDto>(items, pagedSubjects.Page, pagedSubjects.PageSize, pagedSubjects.Total);

        return result;
    }
}
