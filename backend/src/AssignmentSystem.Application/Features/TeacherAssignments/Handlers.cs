using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.ClassCourses;
using AssignmentSystem.Domain.Common;
using AssignmentSystem.Domain.TeacherAssignments;
using AssignmentSystem.Domain.Users;
using AssignmentSystem.Shared.Common;

namespace AssignmentSystem.Application.Features.TeacherAssignments;

/// <summary>
/// Maps a teacher to a course offering. This is the moment the teacher gains the right to
/// create assignments and grade for that class, so it is also the moment they are emailed —
/// queued in the same transaction as the mapping itself (see
/// <see cref="INotificationOutbox"/>), so there is no state where a teacher owns a course
/// nobody told them about.
/// </summary>
public sealed class CreateTeacherAssignmentHandler : ICommandHandler<CreateTeacherAssignmentCommand, TeacherAssignmentDto>
{
    private readonly IRepository<TeacherAssignment> _teacherAssignmentRepository;
    private readonly IRepository<ApplicationUser> _userRepository;
    private readonly IRepository<ClassCourse> _classCourseRepository;
    private readonly INotificationOutbox _notifications;
    private readonly IUnitOfWork _unitOfWork;
    private static readonly TeacherAssignmentMapper Mapper = new();

    public CreateTeacherAssignmentHandler(
        IRepository<TeacherAssignment> teacherAssignmentRepository,
        IRepository<ApplicationUser> userRepository,
        IRepository<ClassCourse> classCourseRepository,
        INotificationOutbox notifications,
        IUnitOfWork unitOfWork)
    {
        _teacherAssignmentRepository = teacherAssignmentRepository;
        _userRepository = userRepository;
        _classCourseRepository = classCourseRepository;
        _notifications = notifications;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<TeacherAssignmentDto>> HandleAsync(CreateTeacherAssignmentCommand command, CancellationToken ct = default)
    {
        var teacher = await _userRepository.GetByIdAsync(command.TeacherId, ct);
        if (teacher is null || teacher.Role != Domain.Enums.Role.Teacher || !teacher.IsActive)
        {
            return Result<TeacherAssignmentDto>.Failure(Error.Validation("Teacher.Invalid", "The selected user is not an active teacher."));
        }

        // One lookup instead of the two this used to need. The offering already guarantees
        // the class actually studies the course, so there is no longer a (class, course)
        // pair to validate against each other here.
        var offering = await _classCourseRepository.GetByIdAsync(command.ClassCourseId, ct);
        if (offering is null)
        {
            return Result<TeacherAssignmentDto>.Failure(Error.NotFound("ClassCourse.NotFound", "The specified course offering was not found."));
        }

        // Only one teacher per offering: if this offering already has a mapping, either it's
        // this same teacher (plain duplicate) or a different one — and a different teacher
        // must be removed via delete before another can take the offering.
        var existingSpec = new TeacherAssignmentByClassCourseSpecification(command.ClassCourseId);
        var existingAssignment = await _teacherAssignmentRepository.FirstOrDefaultAsync(existingSpec, ct);
        if (existingAssignment is not null)
        {
            if (existingAssignment.TeacherId == command.TeacherId)
            {
                return Result<TeacherAssignmentDto>.Failure(Error.Conflict("TeacherAssignment.Duplicate", "This teacher is already assigned to this course and class."));
            }

            return Result<TeacherAssignmentDto>.Failure(Error.Conflict("TeacherAssignment.AlreadyHasTeacher", "This class and course already has an assigned teacher. Remove that mapping before assigning a different one."));
        }

        try
        {
            var teacherAssignment = TeacherAssignment.Create(command.TeacherId, command.ClassCourseId);
            await _teacherAssignmentRepository.AddAsync(teacherAssignment, ct);

            // Before the save, not after: the notification row has to land in the same
            // transaction as the mapping that caused it.
            await _notifications.QueueTeacherAssignedAsync(teacherAssignment, ct);

            await _unitOfWork.SaveChangesAsync(ct);

            // Fetch with fully loaded relationships for mapping
            var fetchSpec = new TeacherAssignmentWithDetailsSpecification(teacherAssignment.Id);
            var savedAssignment = await _teacherAssignmentRepository.FirstOrDefaultAsync(fetchSpec, ct);

            if (savedAssignment is null)
            {
                // Unreachable — see the note on CreateAssignmentHandler. The DTO needs the
                // teacher, class and course names, which live behind navigations.
                return Result<TeacherAssignmentDto>.Failure(Error.Failure(
                    "TeacherAssignment.NotReloaded", "The mapping was created but could not be read back."));
            }

            return Mapper.MapToDto(savedAssignment);
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
        IReadOnlyList<Guid>? teacherIds = _currentUser.Role == Domain.Enums.Role.Teacher
            ? _currentUser.UserId is { } userId ? [userId] : null
            : query.TeacherIds;

        var spec = new TeacherAssignmentsPagedSpecification(
            teacherIds, query.CourseIds, query.ClassIds, query.ClassCourseIds, query.Search, query.SortBy, query.SortDir, query.Page, query.PageSize);
        var pagedAssignments = await _teacherAssignmentRepository.ListPagedAsync(spec, ct);

        var items = pagedAssignments.Items.Select(Mapper.MapToDto).ToList();
        var result = new PageResult<TeacherAssignmentDto>(items, pagedAssignments.Page, pagedAssignments.PageSize, pagedAssignments.Total);

        return result;
    }
}
