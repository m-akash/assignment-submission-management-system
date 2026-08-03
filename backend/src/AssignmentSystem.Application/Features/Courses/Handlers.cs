using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.Common;
using AssignmentSystem.Domain.Courses;
using AssignmentSystem.Domain.Departments;
using AssignmentSystem.Shared.Common;

namespace AssignmentSystem.Application.Features.Courses;

public sealed class CreateCourseHandler : ICommandHandler<CreateCourseCommand, CourseDto>
{
    private readonly IRepository<Course> _courseRepository;
    private readonly IRepository<Department> _departmentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private static readonly CourseMapper Mapper = new();

    public CreateCourseHandler(
        IRepository<Course> courseRepository,
        IRepository<Department> departmentRepository,
        IUnitOfWork unitOfWork)
    {
        _courseRepository = courseRepository;
        _departmentRepository = departmentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CourseDto>> HandleAsync(CreateCourseCommand command, CancellationToken ct = default)
    {
        var codeSpec = new CourseByCodeSpecification(command.Code);
        var codeExists = await _courseRepository.AnyAsync(codeSpec, ct);
        if (codeExists)
        {
            return Result<CourseDto>.Failure(Error.Conflict("Course.CodeAlreadyExists", "A course with this code already exists."));
        }

        var department = await _departmentRepository.GetByIdAsync(command.DepartmentId, ct);
        if (department is null)
        {
            return Result<CourseDto>.Failure(Error.NotFound("Department.NotFound", "The specified department was not found."));
        }

        try
        {
            var course = Course.Create(command.Name, command.Code, command.DepartmentId);
            await _courseRepository.AddAsync(course, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            // Re-fetch with Department included so the DTO carries its name and code.
            var saved = await _courseRepository.FirstOrDefaultAsync(new CourseWithDepartmentByIdSpecification(course.Id), ct);
            return Mapper.MapToDto(saved ?? course);
        }
        catch (DomainException ex)
        {
            return Result<CourseDto>.Failure(Error.Validation("Course.Invalid", ex.Message));
        }
    }
}

public sealed class UpdateCourseHandler : ICommandHandler<UpdateCourseCommand, CourseDto>
{
    private readonly IRepository<Course> _courseRepository;
    private readonly IRepository<Department> _departmentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private static readonly CourseMapper Mapper = new();

    public UpdateCourseHandler(
        IRepository<Course> courseRepository,
        IRepository<Department> departmentRepository,
        IUnitOfWork unitOfWork)
    {
        _courseRepository = courseRepository;
        _departmentRepository = departmentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CourseDto>> HandleAsync(UpdateCourseCommand command, CancellationToken ct = default)
    {
        var course = await _courseRepository.GetByIdAsync(command.Id, ct);
        if (course is null)
        {
            return Result<CourseDto>.Failure(Error.NotFound("Course.NotFound", "The specified course was not found."));
        }

        var normalizedCode = command.Code.Trim().ToUpperInvariant();
        if (normalizedCode != course.Code)
        {
            var codeSpec = new CourseByCodeSpecification(command.Code);
            var codeExists = await _courseRepository.AnyAsync(codeSpec, ct);
            if (codeExists)
            {
                return Result<CourseDto>.Failure(Error.Conflict("Course.CodeAlreadyExists", "A course with this code already exists."));
            }
        }

        if (command.DepartmentId != course.DepartmentId)
        {
            var department = await _departmentRepository.GetByIdAsync(command.DepartmentId, ct);
            if (department is null)
            {
                return Result<CourseDto>.Failure(Error.NotFound("Department.NotFound", "The specified department was not found."));
            }
        }

        try
        {
            course.Update(command.Name, command.Code, command.DepartmentId);
            _courseRepository.Update(course);
            await _unitOfWork.SaveChangesAsync(ct);

            var saved = await _courseRepository.FirstOrDefaultAsync(new CourseWithDepartmentByIdSpecification(course.Id), ct);
            return Mapper.MapToDto(saved ?? course);
        }
        catch (DomainException ex)
        {
            return Result<CourseDto>.Failure(Error.Validation("Course.Invalid", ex.Message));
        }
    }
}

public sealed class DeleteCourseHandler : ICommandHandler<DeleteCourseCommand>
{
    private readonly IRepository<Course> _courseRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteCourseHandler(IRepository<Course> courseRepository, IUnitOfWork unitOfWork)
    {
        _courseRepository = courseRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(DeleteCourseCommand command, CancellationToken ct = default)
    {
        var course = await _courseRepository.GetByIdAsync(command.Id, ct);
        if (course is null)
        {
            return Result.Failure(Error.NotFound("Course.NotFound", "The specified course was not found."));
        }

        _courseRepository.Remove(course);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}

public sealed class GetCourseByIdHandler : IQueryHandler<GetCourseByIdQuery, CourseDto>
{
    private readonly IRepository<Course> _courseRepository;
    private static readonly CourseMapper Mapper = new();

    public GetCourseByIdHandler(IRepository<Course> courseRepository)
    {
        _courseRepository = courseRepository;
    }

    public async Task<Result<CourseDto>> HandleAsync(GetCourseByIdQuery query, CancellationToken ct = default)
    {
        var course = await _courseRepository.FirstOrDefaultAsync(new CourseWithDepartmentByIdSpecification(query.Id), ct);
        if (course is null)
        {
            return Result<CourseDto>.Failure(Error.NotFound("Course.NotFound", "The specified course was not found."));
        }

        return Mapper.MapToDto(course);
    }
}

public sealed class GetCoursesHandler : IQueryHandler<GetCoursesQuery, PageResult<CourseDto>>
{
    private readonly IRepository<Course> _courseRepository;
    private static readonly CourseMapper Mapper = new();

    public GetCoursesHandler(IRepository<Course> courseRepository)
    {
        _courseRepository = courseRepository;
    }

    public async Task<Result<PageResult<CourseDto>>> HandleAsync(GetCoursesQuery query, CancellationToken ct = default)
    {
        var spec = new CoursesPagedSpecification(query.Search, query.DepartmentId, query.Page, query.PageSize);
        var pagedCourses = await _courseRepository.ListPagedAsync(spec, ct);

        var items = pagedCourses.Items.Select(Mapper.MapToDto).ToList();
        var result = new PageResult<CourseDto>(items, pagedCourses.Page, pagedCourses.PageSize, pagedCourses.Total);

        return result;
    }
}
