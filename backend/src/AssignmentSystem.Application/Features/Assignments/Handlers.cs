using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.Assignments;
using AssignmentSystem.Domain.Common;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Domain.Submissions;
using AssignmentSystem.Domain.TeacherAssignments;
using AssignmentSystem.Shared.Common;

namespace AssignmentSystem.Application.Features.Assignments;

public sealed class CreateAssignmentHandler : ICommandHandler<CreateAssignmentCommand, AssignmentDto>
{
    private readonly IRepository<Assignment> _assignmentRepository;
    private readonly IRepository<TeacherAssignment> _teacherAssignmentRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private static readonly AssignmentMapper Mapper = new();

    public CreateAssignmentHandler(
        IRepository<Assignment> assignmentRepository,
        IRepository<TeacherAssignment> teacherAssignmentRepository,
        ICurrentUser currentUser,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _assignmentRepository = assignmentRepository;
        _teacherAssignmentRepository = teacherAssignmentRepository;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AssignmentDto>> HandleAsync(CreateAssignmentCommand command, CancellationToken ct = default)
    {
        if (_currentUser.Role != Role.Teacher && _currentUser.Role != Role.Admin)
        {
            return Result<AssignmentDto>.Failure(Error.Forbidden("Assignment.Forbidden", "Only teachers or admins can create assignments."));
        }

        var teacherAssignment = await _teacherAssignmentRepository.GetByIdAsync(command.TeacherAssignmentId, ct);
        if (teacherAssignment is null)
        {
            return Result<AssignmentDto>.Failure(Error.NotFound("TeacherAssignment.NotFound", "The specified teacher assignment was not found."));
        }

        if (_currentUser.Role == Role.Teacher && !teacherAssignment.IsOwnedBy(_currentUser.UserId.GetValueOrDefault()))
        {
            return Result<AssignmentDto>.Failure(Error.Forbidden("Assignment.Forbidden", "You do not have permission to create an assignment for this class/subject."));
        }

        try
        {
            var assignment = Assignment.Create(
                teacherAssignment.TeacherId,
                teacherAssignment.SubjectId,
                teacherAssignment.ClassId,
                teacherAssignment.Id,
                command.Title,
                command.Description,
                command.DeadlineUtc,
                command.MaxMarks,
                command.AllowResubmission,
                _clock);

            await _assignmentRepository.AddAsync(assignment, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            // Fetch with navigation properties for DTO
            var spec = new AssignmentWithDetailsSpecification(assignment.Id);
            var savedAssignment = await _assignmentRepository.FirstOrDefaultAsync(spec, ct);

            return Mapper.MapToDto(savedAssignment ?? assignment);
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

        if (_currentUser.Role == Role.Teacher && !assignment.IsOwnedBy(_currentUser.UserId.GetValueOrDefault()))
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

        if (_currentUser.Role == Role.Teacher && !assignment.IsOwnedBy(_currentUser.UserId.GetValueOrDefault()))
        {
            return Result.Failure(Error.Forbidden("Assignment.Forbidden", "You do not have permission to delete this assignment."));
        }

        assignment.SoftDelete(_clock.UtcNow);
        _assignmentRepository.Update(assignment);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}

public sealed class PublishAssignmentHandler : ICommandHandler<PublishAssignmentCommand, AssignmentDto>
{
    private readonly IRepository<Assignment> _assignmentRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private static readonly AssignmentMapper Mapper = new();

    public PublishAssignmentHandler(
        IRepository<Assignment> assignmentRepository,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork)
    {
        _assignmentRepository = assignmentRepository;
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

        if (_currentUser.Role == Role.Teacher && !assignment.IsOwnedBy(_currentUser.UserId.GetValueOrDefault()))
        {
            return Result<AssignmentDto>.Failure(Error.Forbidden("Assignment.Forbidden", "You do not have permission to publish this assignment."));
        }

        try
        {
            assignment.Publish();
            _assignmentRepository.Update(assignment);
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
    private readonly ICurrentUser _currentUser;
    private static readonly AssignmentMapper Mapper = new();

    public GetAssignmentByIdHandler(IRepository<Assignment> assignmentRepository, ICurrentUser currentUser)
    {
        _assignmentRepository = assignmentRepository;
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

        // B1: Student can only view assignment if it is for their class
        if (_currentUser.Role == Role.Student && assignment.ClassId != _currentUser.ClassId)
        {
            return Result<AssignmentDto>.Failure(Error.Forbidden("Assignment.Forbidden", "You do not have access to this assignment."));
        }

        // Student cannot see draft assignments
        if (_currentUser.Role == Role.Student && assignment.Status == AssignmentStatus.Draft)
        {
            return Result<AssignmentDto>.Failure(Error.Forbidden("Assignment.Forbidden", "You do not have access to this draft assignment."));
        }

        return Mapper.MapToDto(assignment);
    }
}

public sealed class GetAssignmentsHandler : IQueryHandler<GetAssignmentsQuery, PageResult<AssignmentDto>>
{
    private readonly IRepository<Assignment> _assignmentRepository;
    private readonly ICurrentUser _currentUser;
    private static readonly AssignmentMapper Mapper = new();

    public GetAssignmentsHandler(IRepository<Assignment> assignmentRepository, ICurrentUser currentUser)
    {
        _assignmentRepository = assignmentRepository;
        _currentUser = currentUser;
    }

    public async Task<Result<PageResult<AssignmentDto>>> HandleAsync(GetAssignmentsQuery query, CancellationToken ct = default)
    {
        var classId = query.ClassId;
        var teacherId = query.TeacherId;
        var status = query.Status;

        // B1: Student sees only assignments for their class and only published ones
        if (_currentUser.Role == Role.Student)
        {
            classId = _currentUser.ClassId;
            status = AssignmentStatus.Published;
        }
        else if (_currentUser.Role == Role.Teacher)
        {
            // Teacher sees assignments they own
            teacherId = _currentUser.UserId.GetValueOrDefault();
        }

        var spec = new AssignmentsPagedSpecification(classId, query.SubjectId, teacherId, status, query.Search, query.Page, query.PageSize);
        var pagedAssignments = await _assignmentRepository.ListPagedAsync(spec, ct);

        var items = pagedAssignments.Items.Select(Mapper.MapToDto).ToList();
        var result = new PageResult<AssignmentDto>(items, pagedAssignments.Page, pagedAssignments.PageSize, pagedAssignments.Total);

        return result;
    }
}
