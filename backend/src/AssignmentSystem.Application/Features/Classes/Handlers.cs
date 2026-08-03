using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.Classes;
using AssignmentSystem.Domain.Common;
using AssignmentSystem.Shared.Common;

namespace AssignmentSystem.Application.Features.Classes;

public sealed class CreateClassHandler : ICommandHandler<CreateClassCommand, ClassDto>
{
    private readonly IRepository<Class> _classRepository;
    private readonly IUnitOfWork _unitOfWork;
    private static readonly ClassMapper Mapper = new();

    public CreateClassHandler(IRepository<Class> classRepository, IUnitOfWork unitOfWork)
    {
        _classRepository = classRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ClassDto>> HandleAsync(CreateClassCommand command, CancellationToken ct = default)
    {
        try
        {
            var classObj = Class.Create(command.Name, command.Grade, command.Section);
            await _classRepository.AddAsync(classObj, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return Mapper.MapToDto(classObj);
        }
        catch (DomainException ex)
        {
            return Result<ClassDto>.Failure(Error.Validation("Class.Invalid", ex.Message));
        }
    }
}

public sealed class UpdateClassHandler : ICommandHandler<UpdateClassCommand, ClassDto>
{
    private readonly IRepository<Class> _classRepository;
    private readonly IUnitOfWork _unitOfWork;
    private static readonly ClassMapper Mapper = new();

    public UpdateClassHandler(IRepository<Class> classRepository, IUnitOfWork unitOfWork)
    {
        _classRepository = classRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ClassDto>> HandleAsync(UpdateClassCommand command, CancellationToken ct = default)
    {
        var classObj = await _classRepository.GetByIdAsync(command.Id, ct);
        if (classObj is null)
        {
            return Result<ClassDto>.Failure(Error.NotFound("Class.NotFound", "The specified class was not found."));
        }

        try
        {
            classObj.Update(command.Name, command.Grade, command.Section);
            _classRepository.Update(classObj);
            await _unitOfWork.SaveChangesAsync(ct);

            return Mapper.MapToDto(classObj);
        }
        catch (DomainException ex)
        {
            return Result<ClassDto>.Failure(Error.Validation("Class.Invalid", ex.Message));
        }
    }
}

public sealed class DeleteClassHandler : ICommandHandler<DeleteClassCommand>
{
    private readonly IRepository<Class> _classRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteClassHandler(IRepository<Class> classRepository, IUnitOfWork unitOfWork)
    {
        _classRepository = classRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(DeleteClassCommand command, CancellationToken ct = default)
    {
        var classObj = await _classRepository.GetByIdAsync(command.Id, ct);
        if (classObj is null)
        {
            return Result.Failure(Error.NotFound("Class.NotFound", "The specified class was not found."));
        }

        _classRepository.Remove(classObj);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}

public sealed class GetClassByIdHandler : IQueryHandler<GetClassByIdQuery, ClassDto>
{
    private readonly IRepository<Class> _classRepository;
    private readonly IClassRosterRepository _classRosterRepository;
    private static readonly ClassMapper Mapper = new();

    public GetClassByIdHandler(IRepository<Class> classRepository, IClassRosterRepository classRosterRepository)
    {
        _classRepository = classRepository;
        _classRosterRepository = classRosterRepository;
    }

    public async Task<Result<ClassDto>> HandleAsync(GetClassByIdQuery query, CancellationToken ct = default)
    {
        var classObj = await _classRepository.GetByIdAsync(query.Id, ct);
        if (classObj is null)
        {
            return Result<ClassDto>.Failure(Error.NotFound("Class.NotFound", "The specified class was not found."));
        }

        var counts = await _classRosterRepository.GetStudentCountsAsync([classObj.Id], ct);
        return Mapper.MapToDto(classObj) with { StudentCount = counts.GetValueOrDefault(classObj.Id) };
    }
}

public sealed class GetClassesHandler : IQueryHandler<GetClassesQuery, PageResult<ClassDto>>
{
    private readonly IRepository<Class> _classRepository;
    private readonly IClassRosterRepository _classRosterRepository;
    private static readonly ClassMapper Mapper = new();

    public GetClassesHandler(IRepository<Class> classRepository, IClassRosterRepository classRosterRepository)
    {
        _classRepository = classRepository;
        _classRosterRepository = classRosterRepository;
    }

    public async Task<Result<PageResult<ClassDto>>> HandleAsync(GetClassesQuery query, CancellationToken ct = default)
    {
        var spec = new ClassesPagedSpecification(query.Search, query.Page, query.PageSize);
        var pagedClasses = await _classRepository.ListPagedAsync(spec, ct);

        var classIds = pagedClasses.Items.Select(c => c.Id).ToList();
        var counts = await _classRosterRepository.GetStudentCountsAsync(classIds, ct);

        var items = pagedClasses.Items
            .Select(c => Mapper.MapToDto(c) with { StudentCount = counts.GetValueOrDefault(c.Id) })
            .ToList();
        var result = new PageResult<ClassDto>(items, pagedClasses.Page, pagedClasses.PageSize, pagedClasses.Total);

        return result;
    }
}
