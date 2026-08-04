using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.Classes;
using AssignmentSystem.Domain.Common;
using AssignmentSystem.Domain.Enrollments;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Domain.Users;
using AssignmentSystem.Shared.Common;

namespace AssignmentSystem.Application.Features.Enrollments;

public sealed class CreateEnrollmentHandler : ICommandHandler<CreateEnrollmentCommand, EnrollmentDto>
{
    private readonly IRepository<StudentEnrollment> _enrollmentRepository;
    private readonly IRepository<ApplicationUser> _userRepository;
    private readonly IRepository<Class> _classRepository;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private static readonly EnrollmentMapper Mapper = new();

    public CreateEnrollmentHandler(
        IRepository<StudentEnrollment> enrollmentRepository,
        IRepository<ApplicationUser> userRepository,
        IRepository<Class> classRepository,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _enrollmentRepository = enrollmentRepository;
        _userRepository = userRepository;
        _classRepository = classRepository;
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
    private static readonly EnrollmentMapper Mapper = new();

    public GetEnrollmentsHandler(IRepository<StudentEnrollment> enrollmentRepository)
    {
        _enrollmentRepository = enrollmentRepository;
    }

    public async Task<Result<PageResult<EnrollmentDto>>> HandleAsync(GetEnrollmentsQuery query, CancellationToken ct = default)
    {
        var spec = new EnrollmentsPagedSpecification(
            query.StudentId, query.ClassId, query.Search, query.Page, query.PageSize);
        var paged = await _enrollmentRepository.ListPagedAsync(spec, ct);

        var items = paged.Items.Select(Mapper.MapToDto).ToList();
        return new PageResult<EnrollmentDto>(items, paged.Page, paged.PageSize, paged.Total);
    }
}
