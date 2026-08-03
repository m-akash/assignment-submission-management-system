using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.Classes;
using AssignmentSystem.Domain.Common;
using AssignmentSystem.Domain.Courses;
using AssignmentSystem.Domain.TeacherAssignments;
using AssignmentSystem.Domain.Users;
using AssignmentSystem.Shared.Common;

namespace AssignmentSystem.Application.Features.TeacherAssignments;

public sealed class CreateTeacherAssignmentHandler : ICommandHandler<CreateTeacherAssignmentCommand, TeacherAssignmentDto>
{
    private readonly IRepository<TeacherAssignment> _teacherAssignmentRepository;
    private readonly IRepository<ApplicationUser> _userRepository;
    private readonly IRepository<Course> _courseRepository;
    private readonly IRepository<Class> _classRepository;
    private readonly IUnitOfWork _unitOfWork;
    private static readonly TeacherAssignmentMapper Mapper = new();

    public CreateTeacherAssignmentHandler(
        IRepository<TeacherAssignment> teacherAssignmentRepository,
        IRepository<ApplicationUser> userRepository,
        IRepository<Course> courseRepository,
        IRepository<Class> classRepository,
        IUnitOfWork unitOfWork)
    {
        _teacherAssignmentRepository = teacherAssignmentRepository;
        _userRepository = userRepository;
        _courseRepository = courseRepository;
        _classRepository = classRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<TeacherAssignmentDto>> HandleAsync(CreateTeacherAssignmentCommand command, CancellationToken ct = default)
    {
        var teacher = await _userRepository.GetByIdAsync(command.TeacherId, ct);
        if (teacher is null || teacher.Role != Domain.Enums.Role.Teacher || !teacher.IsActive)
        {
            return Result<TeacherAssignmentDto>.Failure(Error.Validation("Teacher.Invalid", "The selected user is not an active teacher."));
        }

        var course = await _courseRepository.GetByIdAsync(command.CourseId, ct);
        if (course is null)
        {
            return Result<TeacherAssignmentDto>.Failure(Error.NotFound("Course.NotFound", "The specified course was not found."));
        }

        var classObj = await _classRepository.GetByIdAsync(command.ClassId, ct);
        if (classObj is null)
        {
            return Result<TeacherAssignmentDto>.Failure(Error.NotFound("Class.NotFound", "The specified class was not found."));
        }

        var duplicateSpec = new TeacherAssignmentDuplicateSpecification(command.TeacherId, command.CourseId, command.ClassId);
        var alreadyAssigned = await _teacherAssignmentRepository.AnyAsync(duplicateSpec, ct);
        if (alreadyAssigned)
        {
            return Result<TeacherAssignmentDto>.Failure(Error.Conflict("TeacherAssignment.Duplicate", "This teacher is already assigned to this course and class."));
        }

        try
        {
            var teacherAssignment = TeacherAssignment.Create(command.TeacherId, command.CourseId, command.ClassId);
            await _teacherAssignmentRepository.AddAsync(teacherAssignment, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            // Fetch with fully loaded relationships for mapping
            var fetchSpec = new TeacherAssignmentWithDetailsSpecification(teacherAssignment.Id);
            var savedAssignment = await _teacherAssignmentRepository.FirstOrDefaultAsync(fetchSpec, ct);

            return Mapper.MapToDto(savedAssignment ?? teacherAssignment);
        }
        catch (DomainException ex)
        {
            return Result<TeacherAssignmentDto>.Failure(Error.Validation("TeacherAssignment.Invalid", ex.Message));
        }
    }
}

public sealed class DeleteTeacherAssignmentHandler : ICommandHandler<DeleteTeacherAssignmentCommand>
{
    private readonly IRepository<TeacherAssignment> _teacherAssignmentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteTeacherAssignmentHandler(IRepository<TeacherAssignment> teacherAssignmentRepository, IUnitOfWork unitOfWork)
    {
        _teacherAssignmentRepository = teacherAssignmentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(DeleteTeacherAssignmentCommand command, CancellationToken ct = default)
    {
        var teacherAssignment = await _teacherAssignmentRepository.GetByIdAsync(command.Id, ct);
        if (teacherAssignment is null)
        {
            return Result.Failure(Error.NotFound("TeacherAssignment.NotFound", "The specified teacher assignment was not found."));
        }

        _teacherAssignmentRepository.Remove(teacherAssignment);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}

public sealed class GetTeacherAssignmentsHandler : IQueryHandler<GetTeacherAssignmentsQuery, PageResult<TeacherAssignmentDto>>
{
    private readonly IRepository<TeacherAssignment> _teacherAssignmentRepository;
    private readonly ICurrentUser _currentUser;
    private static readonly TeacherAssignmentMapper Mapper = new();

    public GetTeacherAssignmentsHandler(
        IRepository<TeacherAssignment> teacherAssignmentRepository,
        ICurrentUser currentUser)
    {
        _teacherAssignmentRepository = teacherAssignmentRepository;
        _currentUser = currentUser;
    }

    public async Task<Result<PageResult<TeacherAssignmentDto>>> HandleAsync(GetTeacherAssignmentsQuery query, CancellationToken ct = default)
    {
        // A teacher may only see their own mappings; only an admin may filter by teacher.
        // Scoped server-side so the client cannot widen it via the query string.
        var teacherId = _currentUser.Role == Domain.Enums.Role.Teacher
            ? _currentUser.UserId
            : query.TeacherId;

        var spec = new TeacherAssignmentsPagedSpecification(
            teacherId, query.CourseId, query.ClassId, query.Search, query.Page, query.PageSize);
        var pagedAssignments = await _teacherAssignmentRepository.ListPagedAsync(spec, ct);

        var items = pagedAssignments.Items.Select(Mapper.MapToDto).ToList();
        var result = new PageResult<TeacherAssignmentDto>(items, pagedAssignments.Page, pagedAssignments.PageSize, pagedAssignments.Total);

        return result;
    }
}
