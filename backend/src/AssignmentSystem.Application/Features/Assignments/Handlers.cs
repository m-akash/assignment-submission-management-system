using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.Assignments;
using AssignmentSystem.Domain.ClassCourses;
using AssignmentSystem.Domain.Common;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Domain.Submissions;
using AssignmentSystem.Domain.TeacherAssignments;
using AssignmentSystem.Shared.Common;

namespace AssignmentSystem.Application.Features.Assignments;

/// <summary>
/// Creates a draft assignment against a course offering. The gate is the teaching
/// mapping: the author must be a teacher the admin has assigned to that offering, which is
/// what stops a teacher setting work for a class or course that isn't theirs (rule B3).
/// An admin may create on a teacher's behalf, but only for a teacher already mapped to
/// the offering — otherwise the resulting assignment would be one its own author is not
/// authorized to publish or grade.
/// </summary>
public sealed class CreateAssignmentHandler : ICommandHandler<CreateAssignmentCommand, AssignmentDto>
{
    private readonly IRepository<Assignment> _assignmentRepository;
    private readonly IRepository<TeacherAssignment> _teacherAssignmentRepository;
    private readonly IRepository<ClassCourse> _classCourseRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private static readonly AssignmentMapper Mapper = new();

    public CreateAssignmentHandler(
        IRepository<Assignment> assignmentRepository,
        IRepository<TeacherAssignment> teacherAssignmentRepository,
        IRepository<ClassCourse> classCourseRepository,
        ICurrentUser currentUser,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _assignmentRepository = assignmentRepository;
        _teacherAssignmentRepository = teacherAssignmentRepository;
        _classCourseRepository = classCourseRepository;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AssignmentDto>> HandleAsync(CreateAssignmentCommand command, CancellationToken ct = default)
    {
        if (_currentUser.Role != Role.Teacher)
        {
            return Result<AssignmentDto>.Failure(Error.Forbidden("Assignment.Forbidden", "Only teachers can create assignments."));
        }

        var offering = await _classCourseRepository.GetByIdAsync(command.ClassCourseId, ct);
        if (offering is null)
        {
            return Result<AssignmentDto>.Failure(Error.NotFound("ClassCourse.NotFound", "The specified course offering was not found."));
        }

        // A teacher is always the author of their own work.
        var teacherId = _currentUser.UserId.GetValueOrDefault();

        var mappingSpec = new TeacherAssignmentForOfferingSpecification(teacherId, command.ClassCourseId);
        if (!await _teacherAssignmentRepository.AnyAsync(mappingSpec, ct))
        {
            return Result<AssignmentDto>.Failure(Error.Forbidden(
                "Assignment.Forbidden",
                "You are not assigned to teach this course for this class."));
        }

        try
        {
            var assignment = Assignment.Create(
                teacherId,
                command.ClassCourseId,
                command.Title,
                command.Description,
                command.DeadlineUtc,
                command.MaxMarks,
                command.AllowResubmission,
                _clock);

            await _assignmentRepository.AddAsync(assignment, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            // Re-read with navigations so the DTO carries the class/course/teacher names.
            var spec = new AssignmentWithDetailsSpecification(assignment.Id);
            var savedAssignment = await _assignmentRepository.FirstOrDefaultAsync(spec, ct);

            if (savedAssignment is null)
            {
                // Unreachable: the row was committed a line ago. Mapping `assignment` instead
                // would dereference the class/course/teacher navigations it has never loaded,
                // so this reports the anomaly rather than throwing inside the mapper.
                return Result<AssignmentDto>.Failure(Error.Failure(
                    "Assignment.NotReloaded", "The assignment was created but could not be read back."));
            }

            return Mapper.MapToDto(savedAssignment);
        }
        catch (DomainException ex)
        {
            return Result<AssignmentDto>.Failure(Error.Validation("Assignment.Invalid", ex.Message));
        }
    }
}

public sealed class UpdateAssignmentHandler : ICommandHandler<UpdateAssignmentCommand, AssignmentDto>
{
    private readonly IRepository<Assignment> _assignmentRepository;
    private readonly IRepository<Submission> _submissionRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private static readonly AssignmentMapper Mapper = new();

    public UpdateAssignmentHandler(
        IRepository<Assignment> assignmentRepository,
        IRepository<Submission> submissionRepository,
        ICurrentUser currentUser,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _assignmentRepository = assignmentRepository;
        _submissionRepository = submissionRepository;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AssignmentDto>> HandleAsync(UpdateAssignmentCommand command, CancellationToken ct = default)
    {
        var fetchSpec = new AssignmentWithDetailsSpecification(command.Id);
        var assignment = await _assignmentRepository.FirstOrDefaultAsync(fetchSpec, ct);
        if (assignment is null)
        {
            return Result<AssignmentDto>.Failure(Error.NotFound("Assignment.NotFound", "The specified assignment was not found."));
        }

        if (_currentUser.Role != Role.Teacher || !assignment.IsOwnedBy(_currentUser.UserId.GetValueOrDefault()))
        {
            return Result<AssignmentDto>.Failure(Error.Forbidden("Assignment.Forbidden", "You do not have permission to update this assignment."));
        }

        try
        {
            // Check if there are any submissions for this assignment (either pending or graded)
            var countSpec = new SubmissionsByAssignmentCountSpecification(assignment.Id);
            var hasSubmissions = await _submissionRepository.AnyAsync(countSpec, ct);

            assignment.Update(
                command.Title,
                command.Description,
                command.DeadlineUtc,
                command.MaxMarks,
                command.AllowResubmission,
                _clock,
                hasSubmissions);

            _assignmentRepository.Update(assignment);
            await _unitOfWork.SaveChangesAsync(ct);

            return Mapper.MapToDto(assignment);
        }
        catch (DomainException ex)
        {
            return Result<AssignmentDto>.Failure(Error.Validation("Assignment.Invalid", ex.Message));
        }
    }

    private sealed class SubmissionsByAssignmentCountSpecification : Specification<Submission>
    {
        public SubmissionsByAssignmentCountSpecification(Guid assignmentId)
        {
            Criteria = s => s.AssignmentId == assignmentId;
        }
    }
}

public sealed class DeleteAssignmentHandler : ICommandHandler<DeleteAssignmentCommand>
{
    private readonly IRepository<Assignment> _assignmentRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteAssignmentHandler(
        IRepository<Assignment> assignmentRepository,
        ICurrentUser currentUser,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _assignmentRepository = assignmentRepository;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(DeleteAssignmentCommand command, CancellationToken ct = default)
    {
        var assignment = await _assignmentRepository.GetByIdAsync(command.Id, ct);
        if (assignment is null)
        {
            return Result.Failure(Error.NotFound("Assignment.NotFound", "The specified assignment was not found."));
        }

        if (_currentUser.Role != Role.Teacher || !assignment.IsOwnedBy(_currentUser.UserId.GetValueOrDefault()))
        {
            return Result.Failure(Error.Forbidden("Assignment.Forbidden", "You do not have permission to delete this assignment."));
        }

        assignment.SoftDelete(_clock.UtcNow);
        _assignmentRepository.Update(assignment);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}

/// <summary>
/// Publishes a draft. This is the moment students can first see the work, so it is also
/// the moment the class is emailed — the notifications are queued in the same transaction
/// as the status change (see <see cref="INotificationOutbox"/>), so there is no state where
/// an assignment is visible but the class was never told, or where students are told about
/// something that did not actually publish.
/// </summary>
public sealed class PublishAssignmentHandler : ICommandHandler<PublishAssignmentCommand, AssignmentDto>
{
    private readonly IRepository<Assignment> _assignmentRepository;
    private readonly INotificationOutbox _notifications;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private static readonly AssignmentMapper Mapper = new();

    public PublishAssignmentHandler(
        IRepository<Assignment> assignmentRepository,
        INotificationOutbox notifications,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork)
    {
        _assignmentRepository = assignmentRepository;
        _notifications = notifications;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AssignmentDto>> HandleAsync(PublishAssignmentCommand command, CancellationToken ct = default)
    {
        var spec = new AssignmentWithDetailsSpecification(command.Id);
        var assignment = await _assignmentRepository.FirstOrDefaultAsync(spec, ct);
        if (assignment is null)
        {
            return Result<AssignmentDto>.Failure(Error.NotFound("Assignment.NotFound", "The specified assignment was not found."));
        }

        if (_currentUser.Role != Role.Teacher || !assignment.IsOwnedBy(_currentUser.UserId.GetValueOrDefault()))
        {
            return Result<AssignmentDto>.Failure(Error.Forbidden("Assignment.Forbidden", "You do not have permission to publish this assignment."));
        }

        try
        {
            assignment.Publish();
            _assignmentRepository.Update(assignment);

            await _notifications.QueueAssignmentPublishedAsync(assignment, ct);

            await _unitOfWork.SaveChangesAsync(ct);

            return Mapper.MapToDto(assignment);
        }
        catch (DomainException ex)
        {
            return Result<AssignmentDto>.Failure(Error.Validation("Assignment.Invalid", ex.Message));
        }
    }
}

public sealed class GetAssignmentByIdHandler : IQueryHandler<GetAssignmentByIdQuery, AssignmentDto>
{
    private readonly IRepository<Assignment> _assignmentRepository;
    private readonly IClassRosterRepository _roster;
    private readonly ICurrentUser _currentUser;
    private static readonly AssignmentMapper Mapper = new();

    public GetAssignmentByIdHandler(
        IRepository<Assignment> assignmentRepository,
        IClassRosterRepository roster,
        ICurrentUser currentUser)
    {
        _assignmentRepository = assignmentRepository;
        _roster = roster;
        _currentUser = currentUser;
    }

    public async Task<Result<AssignmentDto>> HandleAsync(GetAssignmentByIdQuery query, CancellationToken ct = default)
    {
        var spec = new AssignmentWithDetailsSpecification(query.Id);
        var assignment = await _assignmentRepository.FirstOrDefaultAsync(spec, ct);
        if (assignment is null)
        {
            return Result<AssignmentDto>.Failure(Error.NotFound("Assignment.NotFound", "The specified assignment was not found."));
        }

        if (_currentUser.Role == Role.Student)
        {
            // X3 before B1: a draft is invisible to every student, so it is not worth a
            // roster query to find out which draft they were asking about.
            if (assignment.Status == AssignmentStatus.Draft)
            {
                return Result<AssignmentDto>.Failure(Error.Forbidden("Assignment.Forbidden", "You do not have access to this draft assignment."));
            }

            // B1: only assignments for a class they are actually enrolled in.
            var isEnrolled = await _roster.IsEnrolledAsync(
                _currentUser.UserId.GetValueOrDefault(), assignment.ClassCourse.ClassId, ct);
            if (!isEnrolled)
            {
                return Result<AssignmentDto>.Failure(Error.Forbidden("Assignment.Forbidden", "You do not have access to this assignment."));
            }
        }

        return Mapper.MapToDto(assignment);
    }
}

public sealed class GetAssignmentsHandler : IQueryHandler<GetAssignmentsQuery, PageResult<AssignmentDto>>
{
    private readonly IRepository<Assignment> _assignmentRepository;
    private readonly IClassRosterRepository _roster;
    private readonly ICurrentUser _currentUser;
    private static readonly AssignmentMapper Mapper = new();

    public GetAssignmentsHandler(
        IRepository<Assignment> assignmentRepository,
        IClassRosterRepository roster,
        ICurrentUser currentUser)
    {
        _assignmentRepository = assignmentRepository;
        _roster = roster;
        _currentUser = currentUser;
    }

    public async Task<Result<PageResult<AssignmentDto>>> HandleAsync(GetAssignmentsQuery query, CancellationToken ct = default)
    {
        var teacherId = query.TeacherId;
        var status = query.Status;
        IReadOnlyList<Guid>? restrictToClassIds = null;

        if (_currentUser.Role == Role.Student)
        {
            // B1 + X3: published work, and only for classes they are enrolled in. The
            // enrollment list is read here rather than taken from the token so an admin
            // moving a student between classes takes effect on their next request.
            restrictToClassIds = await _roster.GetEnrolledClassIdsAsync(_currentUser.UserId.GetValueOrDefault(), ct);
            status = AssignmentStatus.Published;
        }
        else if (_currentUser.Role == Role.Teacher)
        {
            // Teacher sees assignments they own
            teacherId = _currentUser.UserId.GetValueOrDefault();
        }

        var spec = new AssignmentsPagedSpecification(
            query.ClassId, query.CourseId, query.ClassCourseId, restrictToClassIds,
            teacherId, status, query.Search, query.Page, query.PageSize);
        var pagedAssignments = await _assignmentRepository.ListPagedAsync(spec, ct);

        var items = pagedAssignments.Items.Select(Mapper.MapToDto).ToList();
        var result = new PageResult<AssignmentDto>(items, pagedAssignments.Page, pagedAssignments.PageSize, pagedAssignments.Total);

        return result;
    }
}

/// <summary>
/// "Is this teacher allowed to set work for this offering?" — the authorization backbone
/// query behind rules B3 and B7.
/// </summary>
internal sealed class TeacherAssignmentForOfferingSpecification : Specification<TeacherAssignment>
{
    public TeacherAssignmentForOfferingSpecification(Guid teacherId, Guid classCourseId)
    {
        Criteria = ta => ta.TeacherId == teacherId && ta.ClassCourseId == classCourseId;
    }
}
