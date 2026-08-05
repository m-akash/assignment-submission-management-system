using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Application.Common.Interfaces;
// TeacherAssignmentsByTeacherSpecification: reused rather than redeclared so there is one
// definition of "the classes this teacher teaches" — the enrollments read path needs it to
// scope a teacher to their own classes.
using AssignmentSystem.Application.Features.TeacherAssignments;
using AssignmentSystem.Domain.Classes;
using AssignmentSystem.Domain.Common;
using AssignmentSystem.Domain.Enrollments;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Domain.TeacherAssignments;
using AssignmentSystem.Domain.Users;
using AssignmentSystem.Shared.Common;

namespace AssignmentSystem.Application.Features.Enrollments;

/// <summary>
/// Enrols an existing student in another class. Mails the student in the same transaction
/// (see <see cref="INotificationOutbox"/>) — enrollment is what decides which assignments
/// they can see, so being added to a class is news they need.
/// </summary>
public sealed class CreateEnrollmentHandler : ICommandHandler<CreateEnrollmentCommand, EnrollmentDto>
{
    private readonly IRepository<StudentEnrollment> _enrollmentRepository;
    private readonly IRepository<ApplicationUser> _userRepository;
    private readonly IRepository<Class> _classRepository;
    private readonly INotificationOutbox _notifications;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private static readonly EnrollmentMapper Mapper = new();

    public CreateEnrollmentHandler(
        IRepository<StudentEnrollment> enrollmentRepository,
        IRepository<ApplicationUser> userRepository,
        IRepository<Class> classRepository,
        INotificationOutbox notifications,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _enrollmentRepository = enrollmentRepository;
        _userRepository = userRepository;
        _classRepository = classRepository;
        _notifications = notifications;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<EnrollmentDto>> HandleAsync(CreateEnrollmentCommand command, CancellationToken ct = default)
    {
        var student = await _userRepository.GetByIdAsync(command.StudentId, ct);
        if (student is null || student.Role != Role.Student)
        {
            return Result<EnrollmentDto>.Failure(Error.Validation(
                "Enrollment.NotAStudent", "The selected user is not a student."));
        }

        var classObj = await _classRepository.GetByIdAsync(command.ClassId, ct);
        if (classObj is null)
        {
            return Result<EnrollmentDto>.Failure(Error.NotFound("Class.NotFound", "The specified class was not found."));
        }

        var duplicateSpec = new EnrollmentDuplicateSpecification(command.StudentId, command.ClassId);
        if (await _enrollmentRepository.AnyAsync(duplicateSpec, ct))
        {
            return Result<EnrollmentDto>.Failure(Error.Conflict(
                "Enrollment.Duplicate", "This student is already enrolled in this class."));
        }

        try
        {
            var enrollment = StudentEnrollment.Create(command.StudentId, command.ClassId, _clock.UtcNow);
            await _enrollmentRepository.AddAsync(enrollment, ct);

            // Before the save, not after: the notification row has to land in the same
            // transaction as the enrollment that caused it.
            await _notifications.QueueStudentEnrolledAsync(enrollment, ct);

            await _unitOfWork.SaveChangesAsync(ct);

            var fetchSpec = new EnrollmentWithDetailsSpecification(enrollment.Id);
            var saved = await _enrollmentRepository.FirstOrDefaultAsync(fetchSpec, ct);

            if (saved is null)
            {
                // Unreachable — see the note on CreateAssignmentHandler. The DTO needs the
                // student and class names, which live behind navigations.
                return Result<EnrollmentDto>.Failure(Error.Failure(
                    "Enrollment.NotReloaded", "The enrollment was created but could not be read back."));
            }

            return Mapper.MapToDto(saved);
        }
        catch (DomainException ex)
        {
            return Result<EnrollmentDto>.Failure(Error.Validation("Enrollment.Invalid", ex.Message));
        }
    }
}

/// <summary>
/// Un-enrols a student. Refuses to remove their last class: a student with no class can
/// see no assignments at all, which looks like data loss rather than an intended state.
/// Moving a student means adding the new class first, then removing the old one.
/// </summary>
public sealed class DeleteEnrollmentHandler : ICommandHandler<DeleteEnrollmentCommand>
{
    private readonly IRepository<StudentEnrollment> _enrollmentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteEnrollmentHandler(
        IRepository<StudentEnrollment> enrollmentRepository,
        IUnitOfWork unitOfWork)
    {
        _enrollmentRepository = enrollmentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(DeleteEnrollmentCommand command, CancellationToken ct = default)
    {
        var enrollment = await _enrollmentRepository.GetByIdAsync(command.Id, ct);
        if (enrollment is null)
        {
            return Result.Failure(Error.NotFound("Enrollment.NotFound", "The specified enrollment was not found."));
        }

        var remainingSpec = new EnrollmentsByStudentSpecification(enrollment.StudentId);
        var total = await _enrollmentRepository.CountAsync(remainingSpec, ct);
        if (total <= 1)
        {
            return Result.Failure(Error.Conflict(
                "Enrollment.LastClass",
                "A student must belong to at least one class. Enrol them in the new class before removing this one."));
        }

        _enrollmentRepository.Remove(enrollment);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}

public sealed class GetEnrollmentsHandler : IQueryHandler<GetEnrollmentsQuery, PageResult<EnrollmentDto>>
{
    private readonly IRepository<StudentEnrollment> _enrollmentRepository;
    private readonly IRepository<TeacherAssignment> _teacherAssignmentRepository;
    private readonly ICurrentUser _currentUser;
    private static readonly EnrollmentMapper Mapper = new();

    public GetEnrollmentsHandler(
        IRepository<StudentEnrollment> enrollmentRepository,
        IRepository<TeacherAssignment> teacherAssignmentRepository,
        ICurrentUser currentUser)
    {
        _enrollmentRepository = enrollmentRepository;
        _teacherAssignmentRepository = teacherAssignmentRepository;
        _currentUser = currentUser;
    }

    public async Task<Result<PageResult<EnrollmentDto>>> HandleAsync(GetEnrollmentsQuery query, CancellationToken ct = default)
    {
        // A teacher may only see enrollments for classes they teach; an admin may see all.
        // Scoped server-side so the client cannot widen it via the query string, mirroring
        // GetTeacherAssignmentsHandler. If a teacher asks for a classId they do not teach,
        // that is a forbidden widening rather than a silent empty page.
        IReadOnlyCollection<Guid>? allowedClassIds = null;
        if (_currentUser.Role == Role.Teacher)
        {
            var teacherId = _currentUser.UserId.GetValueOrDefault();
            // The classes this teacher teaches, resolved through their offerings. ClassCourseId
            // is the join row; the class id lives on the offering, so the spec includes it.
            allowedClassIds = await ResolveTaughtClassIdsAsync(teacherId, ct);

            if (query.ClassId.HasValue && !allowedClassIds.Contains(query.ClassId.Value))
            {
                return Result<PageResult<EnrollmentDto>>.Failure(Error.Forbidden(
                    "Enrollment.Forbidden", "You do not teach this class."));
            }
        }

        var spec = new EnrollmentsPagedSpecification(
            query.StudentId, query.ClassId, query.Search, query.Page, query.PageSize, allowedClassIds);
        var paged = await _enrollmentRepository.ListPagedAsync(spec, ct);

        var items = paged.Items.Select(Mapper.MapToDto).ToList();
        return new PageResult<EnrollmentDto>(items, paged.Page, paged.PageSize, paged.Total);
    }

    /// <summary>The distinct class ids this teacher teaches, via their offerings.</summary>
    private async Task<IReadOnlyCollection<Guid>> ResolveTaughtClassIdsAsync(Guid teacherId, CancellationToken ct)
    {
        var spec = new TeacherAssignmentsByTeacherSpecification(teacherId);
        var taught = await _teacherAssignmentRepository.ListAsync(spec, ct);
        return taught
            .Select(ta => ta.ClassCourse?.ClassId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
    }
}
