using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.Common;
using AssignmentSystem.Domain.Departments;
using AssignmentSystem.Shared.Common;

namespace AssignmentSystem.Application.Features.Departments;

public sealed class CreateDepartmentHandler : ICommandHandler<CreateDepartmentCommand, DepartmentDto>
{
    private readonly IRepository<Department> _departmentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private static readonly DepartmentMapper Mapper = new();

    public CreateDepartmentHandler(IRepository<Department> departmentRepository, IUnitOfWork unitOfWork)
    {
        _departmentRepository = departmentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<DepartmentDto>> HandleAsync(CreateDepartmentCommand command, CancellationToken ct = default)
    {
        var codeSpec = new DepartmentByCodeSpecification(command.Code);
        if (await _departmentRepository.AnyAsync(codeSpec, ct))
        {
            return Result<DepartmentDto>.Failure(
                Error.Conflict("Department.CodeAlreadyExists", "A department with this code already exists."));
        }

        try
        {
            var department = Department.Create(command.Name, command.Code);
            await _departmentRepository.AddAsync(department, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return Mapper.MapToDto(department);
        }
        catch (DomainException ex)
        {
            return Result<DepartmentDto>.Failure(Error.Validation("Department.Invalid", ex.Message));
        }
    }
}

public sealed class UpdateDepartmentHandler : ICommandHandler<UpdateDepartmentCommand, DepartmentDto>
{
    private readonly IRepository<Department> _departmentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private static readonly DepartmentMapper Mapper = new();

    public UpdateDepartmentHandler(IRepository<Department> departmentRepository, IUnitOfWork unitOfWork)
    {
        _departmentRepository = departmentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<DepartmentDto>> HandleAsync(UpdateDepartmentCommand command, CancellationToken ct = default)
    {
        var department = await _departmentRepository.GetByIdAsync(command.Id, ct);
        if (department is null)
        {
            return Result<DepartmentDto>.Failure(
                Error.NotFound("Department.NotFound", "The specified department was not found."));
        }

        var normalizedCode = command.Code.Trim().ToUpperInvariant();
        if (normalizedCode != department.Code)
        {
            var codeSpec = new DepartmentByCodeSpecification(command.Code);
            if (await _departmentRepository.AnyAsync(codeSpec, ct))
            {
                return Result<DepartmentDto>.Failure(
                    Error.Conflict("Department.CodeAlreadyExists", "A department with this code already exists."));
            }
        }

        try
        {
            department.Update(command.Name, command.Code);
            _departmentRepository.Update(department);
            await _unitOfWork.SaveChangesAsync(ct);

            return Mapper.MapToDto(department);
        }
        catch (DomainException ex)
        {
            return Result<DepartmentDto>.Failure(Error.Validation("Department.Invalid", ex.Message));
        }
    }
}

public sealed class DeleteDepartmentHandler : ICommandHandler<DeleteDepartmentCommand>
{
    private readonly IRepository<Department> _departmentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteDepartmentHandler(IRepository<Department> departmentRepository, IUnitOfWork unitOfWork)
    {
        _departmentRepository = departmentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(DeleteDepartmentCommand command, CancellationToken ct = default)
    {
        var department = await _departmentRepository.GetByIdAsync(command.Id, ct);
        if (department is null)
        {
            return Result.Failure(Error.NotFound("Department.NotFound", "The specified department was not found."));
        }

        // Courses reference departments with RESTRICT, so a department that still owns
        // courses is refused by the database and surfaces as a 409.
        _departmentRepository.Remove(department);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}

public sealed class GetDepartmentByIdHandler : IQueryHandler<GetDepartmentByIdQuery, DepartmentDto>
{
    private readonly IRepository<Department> _departmentRepository;
    private static readonly DepartmentMapper Mapper = new();

    public GetDepartmentByIdHandler(IRepository<Department> departmentRepository)
    {
        _departmentRepository = departmentRepository;
    }

    public async Task<Result<DepartmentDto>> HandleAsync(GetDepartmentByIdQuery query, CancellationToken ct = default)
    {
        var department = await _departmentRepository.GetByIdAsync(query.Id, ct);
        if (department is null)
        {
            return Result<DepartmentDto>.Failure(
                Error.NotFound("Department.NotFound", "The specified department was not found."));
        }

        return Mapper.MapToDto(department);
    }
}

public sealed class GetDepartmentsHandler : IQueryHandler<GetDepartmentsQuery, PageResult<DepartmentDto>>
{
    private readonly IRepository<Department> _departmentRepository;
    private static readonly DepartmentMapper Mapper = new();

    public GetDepartmentsHandler(IRepository<Department> departmentRepository)
    {
        _departmentRepository = departmentRepository;
    }

    public async Task<Result<PageResult<DepartmentDto>>> HandleAsync(GetDepartmentsQuery query, CancellationToken ct = default)
    {
        var spec = new DepartmentsPagedSpecification(query.Search, query.Page, query.PageSize);
        var paged = await _departmentRepository.ListPagedAsync(spec, ct);

        var items = paged.Items.Select(Mapper.MapToDto).ToList();
        return new PageResult<DepartmentDto>(items, paged.Page, paged.PageSize, paged.Total);
    }
}
