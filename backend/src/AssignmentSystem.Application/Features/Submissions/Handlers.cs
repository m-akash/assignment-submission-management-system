using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Application.Common.Authorization;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Application.Common.Interfaces;
// AssignmentWithScopeSpecification: the submission paths need the assignment's offering to
// resolve its class (rule B1) and to name the course in a notification. Reused rather than
// redeclared so there is one definition of "an assignment plus its scope".
using AssignmentSystem.Application.Features.Assignments;
using AssignmentSystem.Domain.Assignments;
using AssignmentSystem.Domain.Common;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Domain.Submissions;
using AssignmentSystem.Shared.Common;
using Microsoft.Extensions.Logging;

namespace AssignmentSystem.Application.Features.Submissions;

public sealed class SubmitAssignmentHandler : ICommandHandler<SubmitAssignmentCommand, SubmissionDto>
{
    private readonly IRepository<Submission> _submissionRepository;
    private readonly IRepository<Assignment> _assignmentRepository;
    private readonly IClassRosterRepository _roster;
    private readonly INotificationOutbox _notifications;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private static readonly SubmissionMapper Mapper = new();

    public SubmitAssignmentHandler(
        IRepository<Submission> submissionRepository,
        IRepository<Assignment> assignmentRepository,
        IClassRosterRepository roster,
        INotificationOutbox notifications,
        ICurrentUser currentUser,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _submissionRepository = submissionRepository;
        _assignmentRepository = assignmentRepository;
        _roster = roster;
        _notifications = notifications;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<SubmissionDto>> HandleAsync(SubmitAssignmentCommand command, CancellationToken ct = default)
    {
        // Loaded with its offering: the class id behind the B1 check lives there, and the
        // notification body needs the class and course names.
        var scopeSpec = new AssignmentWithScopeSpecification(command.AssignmentId);
        var assignment = await _assignmentRepository.FirstOrDefaultAsync(scopeSpec, ct);
        if (assignment is null)
        {
            return Result<SubmissionDto>.Failure(Error.NotFound("Assignment.NotFound", "The specified assignment was not found."));
        }

        // B1: Student can only submit to assignments for a class they are enrolled in
        var isEnrolled = await _roster.IsEnrolledAsync(
            _currentUser.UserId.GetValueOrDefault(), assignment.ClassCourse.ClassId, ct);
        if (!isEnrolled)
        {
            return Result<SubmissionDto>.Failure(Error.Forbidden("Submission.Forbidden", "You do not belong to the class for this assignment."));
        }

        // X3: Cannot submit to a draft assignment
        if (assignment.Status != AssignmentStatus.Published)
        {
            return Result<SubmissionDto>.Failure(Error.Forbidden("Submission.Forbidden", "Cannot submit to an unpublished assignment."));
        }

        // Check if submission already exists (e.g. created during file upload)
        var spec = new SubmissionByStudentAndAssignmentSpecification(_currentUser.UserId.GetValueOrDefault(), command.AssignmentId);
        var submission = await _submissionRepository.FirstOrDefaultAsync(spec, ct);

        // Whether the teacher has already been told about this one. A student may edit a
        // draft submission repeatedly before the deadline; only the first crossing into a
        // submitted state is news, so the teacher is not mailed on every keystroke-save.
        var wasAlreadySubmitted = submission is { Status: SubmissionStatus.Submitted or SubmissionStatus.Late };

        try
        {
            if (submission is not null)
            {
                // Submission already exists (files were uploaded first). Update it.
                // "Has files" comes from what is stored, not from what the client claims.
                var hasFiles = submission.Files.Count > 0;
                submission.UpdateContent(command.Content, hasFiles, assignment.AllowResubmission, assignment.DeadlineUtc, _clock);
                _submissionRepository.Update(submission);
            }
            else
            {
                // No prior upload created a submission row, so text is the only possible content.
                const bool hasFiles = false;
                submission = Submission.Create(
                    command.AssignmentId,
                    _currentUser.UserId.GetValueOrDefault(),
                    command.Content,
                    hasFiles,
                    assignment,
                    _clock,
                    finalize: true);

                await _submissionRepository.AddAsync(submission, ct);

                // Track submission count increment in assignment
                assignment.IncrementSubmissionCount();
                _assignmentRepository.Update(assignment);
            }

            var isNowSubmitted = submission.Status is SubmissionStatus.Submitted or SubmissionStatus.Late;
            if (isNowSubmitted && !wasAlreadySubmitted)
            {
                await _notifications.QueueSubmissionReceivedAsync(submission, assignment, ct);
            }

            await _unitOfWork.SaveChangesAsync(ct);

            // Fetch details for mapping
            var detailSpec = new SubmissionWithDetailsSpecification(submission.Id);
            var result = await _submissionRepository.FirstOrDefaultAsync(detailSpec, ct);

            return Mapper.MapToDto(result ?? submission);
        }
        catch (DomainException ex)
        {
            return Result<SubmissionDto>.Failure(Error.Validation("Submission.Invalid", ex.Message));
        }
    }
}

public sealed class UpdateSubmissionHandler : ICommandHandler<UpdateSubmissionCommand, SubmissionDto>
{
    private readonly IRepository<Submission> _submissionRepository;
    private readonly IRepository<Assignment> _assignmentRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private static readonly SubmissionMapper Mapper = new();

    public UpdateSubmissionHandler(
        IRepository<Submission> submissionRepository,
        IRepository<Assignment> assignmentRepository,
        ICurrentUser currentUser,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _submissionRepository = submissionRepository;
        _assignmentRepository = assignmentRepository;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<SubmissionDto>> HandleAsync(UpdateSubmissionCommand command, CancellationToken ct = default)
    {
        var spec = new SubmissionWithDetailsSpecification(command.Id);
        var submission = await _submissionRepository.FirstOrDefaultAsync(spec, ct);
        if (submission is null)
        {
            return Result<SubmissionDto>.Failure(Error.NotFound("Submission.NotFound", "The specified submission was not found."));
        }

        if (!submission.IsOwnedBy(_currentUser.UserId.GetValueOrDefault()))
        {
            return Result<SubmissionDto>.Failure(Error.Forbidden("Submission.Forbidden", "You do not own this submission."));
        }

        var assignment = await _assignmentRepository.GetByIdAsync(submission.AssignmentId, ct);
        if (assignment is null)
        {
            return Result<SubmissionDto>.Failure(Error.NotFound("Assignment.NotFound", "The associated assignment was not found."));
        }

        try
        {
            var hasFiles = submission.Files.Count > 0;
            submission.UpdateContent(command.Content, hasFiles, assignment.AllowResubmission, assignment.DeadlineUtc, _clock);

            _submissionRepository.Update(submission);
            await _unitOfWork.SaveChangesAsync(ct);

            return Mapper.MapToDto(submission);
        }
        catch (DomainException ex)
        {
            return Result<SubmissionDto>.Failure(Error.Validation("Submission.Invalid", ex.Message));
        }
    }
}

public sealed class ReviewSubmissionHandler : ICommandHandler<ReviewSubmissionCommand, SubmissionDto>
{
    private readonly IRepository<Submission> _submissionRepository;
    private readonly IRepository<Assignment> _assignmentRepository;
    private readonly INotificationOutbox _notifications;
    private readonly IAssignmentAccess _assignmentAccess;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private static readonly SubmissionMapper Mapper = new();

    public ReviewSubmissionHandler(
        IRepository<Submission> submissionRepository,
        IRepository<Assignment> assignmentRepository,
        INotificationOutbox notifications,
        IAssignmentAccess assignmentAccess,
        ICurrentUser currentUser,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _submissionRepository = submissionRepository;
        _assignmentRepository = assignmentRepository;
        _notifications = notifications;
        _assignmentAccess = assignmentAccess;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<SubmissionDto>> HandleAsync(ReviewSubmissionCommand command, CancellationToken ct = default)
    {
        var spec = new SubmissionWithDetailsSpecification(command.Id);
        var submission = await _submissionRepository.FirstOrDefaultAsync(spec, ct);
        if (submission is null)
        {
            return Result<SubmissionDto>.Failure(Error.NotFound("Submission.NotFound", "The specified submission was not found."));
        }

        // With its offering loaded: the notification body names the course and class.
        var assignmentSpec = new AssignmentWithScopeSpecification(submission.AssignmentId);
        var assignment = await _assignmentRepository.FirstOrDefaultAsync(assignmentSpec, ct);
        if (assignment is null)
        {
            return Result<SubmissionDto>.Failure(Error.NotFound("Assignment.NotFound", "The associated assignment was not found."));
        }

        // B3: a teacher grades only work they set.
        if (_assignmentAccess.MustBeAuthor(assignment) is { } denied)
        {
            return Result<SubmissionDto>.Failure(denied);
        }

        try
        {
            if (command.Status == SubmissionStatus.Graded)
            {
                submission.Grade(command.Marks, command.Feedback, _currentUser.UserId.GetValueOrDefault(), assignment, _clock);

                // Only Graded is worth an email. A teacher moving a submission back to
                // Pending for re-evaluation (rule B7) is bookkeeping, not news — and mailing
                // it would tell the student marks are ready when they have just been withdrawn.
                await _notifications.QueueSubmissionGradedAsync(submission, assignment, ct);
            }
            else
            {
                submission.SetStatus(command.Status, _currentUser.UserId.GetValueOrDefault(), _clock);
            }

            _submissionRepository.Update(submission);
            await _unitOfWork.SaveChangesAsync(ct);

            // Re-fetch to get Reviewer Name updated
            var updated = await _submissionRepository.FirstOrDefaultAsync(spec, ct);
            return Mapper.MapToDto(updated ?? submission);
        }
        catch (DomainException ex)
        {
            return Result<SubmissionDto>.Failure(Error.Validation("Submission.Invalid", ex.Message));
        }
    }
}

public sealed class GetSubmissionByIdHandler : IQueryHandler<GetSubmissionByIdQuery, SubmissionDto>
{
    private readonly IRepository<Submission> _submissionRepository;
    private readonly ISubmissionAccess _access;
    private static readonly SubmissionMapper Mapper = new();

    public GetSubmissionByIdHandler(IRepository<Submission> submissionRepository, ISubmissionAccess access)
    {
        _submissionRepository = submissionRepository;
        _access = access;
    }

    public async Task<Result<SubmissionDto>> HandleAsync(GetSubmissionByIdQuery query, CancellationToken ct = default)
    {
        var spec = new SubmissionWithDetailsSpecification(query.Id);
        var submission = await _submissionRepository.FirstOrDefaultAsync(spec, ct);
        if (submission is null)
        {
            return Result<SubmissionDto>.Failure(Error.NotFound("Submission.NotFound", "The specified submission was not found."));
        }

        if (await _access.CanViewAsync(submission, ct) is { } denied)
        {
            return Result<SubmissionDto>.Failure(denied);
        }

        return Mapper.MapToDto(submission);
    }
}

public sealed class GetStudentSubmissionHandler : IQueryHandler<GetStudentSubmissionQuery, SubmissionDto>
{
    private readonly IRepository<Submission> _submissionRepository;
    private readonly ICurrentUser _currentUser;
    private static readonly SubmissionMapper Mapper = new();

    public GetStudentSubmissionHandler(IRepository<Submission> submissionRepository, ICurrentUser currentUser)
    {
        _submissionRepository = submissionRepository;
        _currentUser = currentUser;
    }

    public async Task<Result<SubmissionDto>> HandleAsync(GetStudentSubmissionQuery query, CancellationToken ct = default)
    {
        var spec = new SubmissionByStudentAndAssignmentSpecification(_currentUser.UserId.GetValueOrDefault(), query.AssignmentId);
        var submission = await _submissionRepository.FirstOrDefaultAsync(spec, ct);
        if (submission is null)
        {
            return Result<SubmissionDto>.Failure(Error.NotFound("Submission.NotFound", "No submission found for this assignment."));
        }

        return Mapper.MapToDto(submission);
    }
}

public sealed class GetSubmissionsHandler : IQueryHandler<GetSubmissionsQuery, PageResult<SubmissionDto>>
{
    private readonly IRepository<Submission> _submissionRepository;
    private readonly IRepository<Assignment> _assignmentRepository;
    private readonly ICurrentUser _currentUser;
    private static readonly SubmissionMapper Mapper = new();

    public GetSubmissionsHandler(
        IRepository<Submission> submissionRepository,
        IRepository<Assignment> assignmentRepository,
        ICurrentUser currentUser)
    {
        _submissionRepository = submissionRepository;
        _assignmentRepository = assignmentRepository;
        _currentUser = currentUser;
    }

    public async Task<Result<PageResult<SubmissionDto>>> HandleAsync(GetSubmissionsQuery query, CancellationToken ct = default)
    {
        var studentId = query.StudentId;
        var assignmentId = query.AssignmentId;

        // The assignments a teacher authored — used to restrict the list to submissions
        // against their own work. Named for what it holds: these are assignment ids, not
        // ids of the teacher↔offering mappings that TeacherAssignment now refers to.
        List<Guid>? authoredAssignmentIds = null;

        // Scoping
        if (_currentUser.Role == Role.Student)
        {
            // Student sees only their own
            studentId = _currentUser.UserId.GetValueOrDefault();
        }
        else if (_currentUser.Role == Role.Teacher)
        {
            var teacherId = _currentUser.UserId.GetValueOrDefault();
            if (assignmentId.HasValue)
            {
                var assignment = await _assignmentRepository.GetByIdAsync(assignmentId.Value, ct);
                if (assignment is null || !assignment.IsOwnedBy(teacherId))
                {
                    return Result<PageResult<SubmissionDto>>.Failure(Error.Forbidden("Submission.Forbidden", "You do not own this assignment."));
                }
            }
            else
            {
                // Every assignment this teacher authored
                var authoredSpec = new AssignmentsByTeacherSpecification(teacherId);
                var authored = await _assignmentRepository.ListAsync(authoredSpec, ct);
                authoredAssignmentIds = authored.Select(a => a.Id).ToList();
            }
        }

        var spec = new SubmissionsPagedSpecification(
            assignmentId, authoredAssignmentIds, studentId, query.Status, query.Search, query.Page, query.PageSize);
        var pagedSubmissions = await _submissionRepository.ListPagedAsync(spec, ct);

        var items = pagedSubmissions.Items.Select(Mapper.MapToDto).ToList();
        var result = new PageResult<SubmissionDto>(items, pagedSubmissions.Page, pagedSubmissions.PageSize, pagedSubmissions.Total);

        return result;
    }
}

internal sealed class AssignmentsByTeacherSpecification : Specification<Assignment>
{
    public AssignmentsByTeacherSpecification(Guid teacherId)
    {
        Criteria = a => a.TeacherId == teacherId;
    }
}

public sealed class UploadSubmissionFileHandler : ICommandHandler<UploadSubmissionFileCommand, SubmissionFileDto>
{
    private readonly IRepository<Submission> _submissionRepository;
    private readonly IRepository<SubmissionFile> _fileRepository;
    private readonly IRepository<Assignment> _assignmentRepository;
    private readonly IClassRosterRepository _roster;
    private readonly IFileStorage _fileStorage;
    private readonly IFileUploadPolicy _uploadPolicy;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private static readonly SubmissionMapper Mapper = new();

    public UploadSubmissionFileHandler(
        IRepository<Submission> submissionRepository,
        IRepository<SubmissionFile> fileRepository,
        IRepository<Assignment> assignmentRepository,
        IClassRosterRepository roster,
        IFileStorage fileStorage,
        IFileUploadPolicy uploadPolicy,
        ICurrentUser currentUser,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _submissionRepository = submissionRepository;
        _fileRepository = fileRepository;
        _assignmentRepository = assignmentRepository;
        _roster = roster;
        _fileStorage = fileStorage;
        _uploadPolicy = uploadPolicy;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<SubmissionFileDto>> HandleAsync(UploadSubmissionFileCommand command, CancellationToken ct = default)
    {
        var scopeSpec = new AssignmentWithScopeSpecification(command.AssignmentId);
        var assignment = await _assignmentRepository.FirstOrDefaultAsync(scopeSpec, ct);
        if (assignment is null)
        {
            return Result<SubmissionFileDto>.Failure(Error.NotFound("Assignment.NotFound", "The specified assignment was not found."));
        }

        // B1: Class check — the student must be enrolled in the offering's class
        var isEnrolled = await _roster.IsEnrolledAsync(
            _currentUser.UserId.GetValueOrDefault(), assignment.ClassCourse.ClassId, ct);
        if (!isEnrolled)
        {
            return Result<SubmissionFileDto>.Failure(Error.Forbidden("SubmissionFile.Forbidden", "You do not belong to the class for this assignment."));
        }

        // X3: Publish check
        if (assignment.Status != AssignmentStatus.Published)
        {
            return Result<SubmissionFileDto>.Failure(Error.Forbidden("SubmissionFile.Forbidden", "Cannot submit files for an unpublished assignment."));
        }

        // Get or Create the Submission row (unique index prevents duplicates)
        var spec = new SubmissionByStudentAndAssignmentSpecification(_currentUser.UserId.GetValueOrDefault(), command.AssignmentId);
        var submission = await _submissionRepository.FirstOrDefaultAsync(spec, ct);

        if (submission is null)
        {
            // Create a new submission container
            try
            {
                submission = Submission.Create(
                    command.AssignmentId,
                    _currentUser.UserId.GetValueOrDefault(),
                    content: null,
                    hasFile: true,
                    assignment,
                    _clock);

                await _submissionRepository.AddAsync(submission, ct);
                
                // Increment submission count
                assignment.IncrementSubmissionCount();
                _assignmentRepository.Update(assignment);
                
                await _unitOfWork.SaveChangesAsync(ct);
            }
            catch (DomainException ex)
            {
                return Result<SubmissionFileDto>.Failure(Error.Validation("Submission.Invalid", ex.Message));
            }
        }
        else
        {
            // Verify if submission is editable
            if (submission.Status == SubmissionStatus.Graded)
            {
                return Result<SubmissionFileDto>.Failure(Error.Validation("Submission.Invalid", "Cannot edit a submission that has already been graded."));
            }

            if (submission.Status == SubmissionStatus.Late)
            {
                return Result<SubmissionFileDto>.Failure(Error.Validation("Submission.Invalid", "Cannot edit a late submission after the deadline."));
            }

            var now = _clock.UtcNow;
            if (now >= assignment.DeadlineUtc && !assignment.AllowResubmission)
            {
                return Result<SubmissionFileDto>.Failure(Error.Validation("Submission.Invalid", "Cannot update a submission after the deadline."));
            }
        }

        // Cap attachments per submission (rule from FileStorage:MaxFilesPerSubmission).
        if (submission.Files.Count >= _uploadPolicy.MaxFilesPerSubmission)
        {
            return Result<SubmissionFileDto>.Failure(Error.Validation(
                "SubmissionFile.TooMany",
                $"A submission may have at most {_uploadPolicy.MaxFilesPerSubmission} attachments."));
        }

        // Size, extension allow-list, and file signature — all server-side. The returned
        // content type is derived from the validated extension, never taken from the client.
        var validation = _uploadPolicy.Validate(command.FileName, command.SizeBytes, command.Content);
        if (!validation.IsSuccess)
        {
            return Result<SubmissionFileDto>.Failure(validation.Error);
        }

        var validated = validation.Value!;

        SavedFile savedFile;
        try
        {
            savedFile = await _fileStorage.SaveAsync(command.Content, validated.Extension, ct);
        }
        catch (FileTooLargeException ex)
        {
            // Backstop for a stream that under-reported its length.
            return Result<SubmissionFileDto>.Failure(Error.Validation("SubmissionFile.TooLarge", ex.Message));
        }

        try
        {
            var file = SubmissionFile.Create(
                submission.Id,
                _currentUser.UserId.GetValueOrDefault(),
                savedFile.StoredFileName,
                command.FileName,
                validated.ContentType,
                savedFile.SizeBytes,
                savedFile.RelativePath,
                _clock.UtcNow);

            submission.AttachFile(file);
            await _fileRepository.AddAsync(file, ct);
            _submissionRepository.Update(submission);

            await _unitOfWork.SaveChangesAsync(ct);

            return Mapper.MapToFileDto(file);
        }
        catch (DomainException ex)
        {
            // Cleanup written file if DB write fails
            _fileStorage.Delete(savedFile.RelativePath);
            return Result<SubmissionFileDto>.Failure(Error.Validation("SubmissionFile.Invalid", ex.Message));
        }
    }
}

public sealed class DownloadSubmissionFileHandler : IQueryHandler<DownloadSubmissionFileQuery, SubmissionFileDownloadResult>
{
    private readonly IRepository<SubmissionFile> _fileRepository;
    private readonly IRepository<Submission> _submissionRepository;
    private readonly IFileStorage _fileStorage;
    private readonly ISubmissionAccess _access;

    public DownloadSubmissionFileHandler(
        IRepository<SubmissionFile> fileRepository,
        IRepository<Submission> submissionRepository,
        IFileStorage fileStorage,
        ISubmissionAccess access)
    {
        _fileRepository = fileRepository;
        _submissionRepository = submissionRepository;
        _fileStorage = fileStorage;
        _access = access;
    }

    public async Task<Result<SubmissionFileDownloadResult>> HandleAsync(DownloadSubmissionFileQuery query, CancellationToken ct = default)
    {
        var spec = new SubmissionFileByIdSpecification(query.FileId);
        var file = await _fileRepository.FirstOrDefaultAsync(spec, ct);
        if (file is null)
        {
            return Result<SubmissionFileDownloadResult>.Failure(Error.NotFound("SubmissionFile.NotFound", "The specified file was not found."));
        }

        var submission = await _submissionRepository.GetByIdAsync(file.SubmissionId, ct);
        if (submission is null)
        {
            return Result<SubmissionFileDownloadResult>.Failure(Error.NotFound("Submission.NotFound", "Associated submission was not found."));
        }

        // An attachment is exactly as reachable as the submission it belongs to.
        if (await _access.CanViewAsync(submission, ct) is { } denied)
        {
            return Result<SubmissionFileDownloadResult>.Failure(denied);
        }

        try
        {
            var stream = _fileStorage.OpenRead(file.RelativePath);
            return new SubmissionFileDownloadResult(stream, file.ContentType, file.OriginalFileName);
        }
        catch (Exception ex)
        {
            return Result<SubmissionFileDownloadResult>.Failure(Error.Validation("SubmissionFile.ReadError", ex.Message));
        }
    }
}

public sealed class DeleteSubmissionFileHandler : ICommandHandler<DeleteSubmissionFileCommand>
{
    private readonly IRepository<SubmissionFile> _fileRepository;
    private readonly IRepository<Submission> _submissionRepository;
    private readonly IRepository<Assignment> _assignmentRepository;
    private readonly IFileStorage _fileStorage;
    private readonly ISubmissionAccess _access;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteSubmissionFileHandler(
        IRepository<SubmissionFile> fileRepository,
        IRepository<Submission> submissionRepository,
        IRepository<Assignment> assignmentRepository,
        IFileStorage fileStorage,
        ISubmissionAccess access,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _fileRepository = fileRepository;
        _submissionRepository = submissionRepository;
        _assignmentRepository = assignmentRepository;
        _fileStorage = fileStorage;
        _access = access;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(DeleteSubmissionFileCommand command, CancellationToken ct = default)
    {
        var spec = new SubmissionFileByIdSpecification(command.FileId);
        var file = await _fileRepository.FirstOrDefaultAsync(spec, ct);
        if (file is null)
        {
            return Result.Failure(Error.NotFound("SubmissionFile.NotFound", "The specified file was not found."));
        }

        var submissionSpec = new SubmissionWithDetailsSpecification(file.SubmissionId);
        var submission = await _submissionRepository.FirstOrDefaultAsync(submissionSpec, ct);
        if (submission is null)
        {
            return Result.Failure(Error.NotFound("Submission.NotFound", "Associated submission was not found."));
        }

        if (_access.MustBeSubmitter(submission) is { } denied)
        {
            return Result.Failure(denied);
        }

        var assignment = await _assignmentRepository.GetByIdAsync(submission.AssignmentId, ct);
        if (assignment is null)
        {
            return Result.Failure(Error.NotFound("Assignment.NotFound", "Associated assignment was not found."));
        }

        // Validate editability rules (deadline, status)
        if (submission.Status == SubmissionStatus.Graded)
        {
            return Result.Failure(Error.Validation("Submission.Invalid", "Cannot edit a submission that has already been graded."));
        }

        if (submission.Status == SubmissionStatus.Late)
        {
            return Result.Failure(Error.Validation("Submission.Invalid", "Cannot edit a late submission after the deadline."));
        }

        var now = _clock.UtcNow;
        if (now >= assignment.DeadlineUtc && !assignment.AllowResubmission)
        {
            return Result.Failure(Error.Validation("Submission.Invalid", "Cannot update a submission after the deadline."));
        }

        // Check if removing this file leaves the submission empty (only if it has already been submitted/finalized)
        if (submission.Status != SubmissionStatus.Pending &&
            submission.Files.Count <= 1 &&
            string.IsNullOrWhiteSpace(submission.Content))
        {
            return Result.Failure(Error.Validation("Submission.Empty", "A submission must include a text answer or at least one file attachment."));
        }

        // Delete from storage
        _fileStorage.Delete(file.RelativePath);

        // Delete from DB
        _fileRepository.Remove(file);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}
