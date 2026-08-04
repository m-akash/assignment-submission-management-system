using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.Assignments;
using AssignmentSystem.Domain.Common;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Shared.Common;

namespace AssignmentSystem.Application.Features.AssignmentFiles;

public sealed class UploadAssignmentFileHandler : ICommandHandler<UploadAssignmentFileCommand, AssignmentFileDto>
{
    private readonly IRepository<Assignment> _assignmentRepository;
    private readonly IRepository<AssignmentFile> _fileRepository;
    private readonly IFileStorage _fileStorage;
    private readonly IFileUploadPolicy _uploadPolicy;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private static readonly AssignmentFileMapper Mapper = new();

    public UploadAssignmentFileHandler(
        IRepository<Assignment> assignmentRepository,
        IRepository<AssignmentFile> fileRepository,
        IFileStorage fileStorage,
        IFileUploadPolicy uploadPolicy,
        ICurrentUser currentUser,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _assignmentRepository = assignmentRepository;
        _fileRepository = fileRepository;
        _fileStorage = fileStorage;
        _uploadPolicy = uploadPolicy;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AssignmentFileDto>> HandleAsync(UploadAssignmentFileCommand command, CancellationToken ct = default)
    {
        if (_currentUser.Role != Role.Teacher && _currentUser.Role != Role.Admin)
        {
            return Result<AssignmentFileDto>.Failure(Error.Forbidden("AssignmentFile.Forbidden", "Only teachers or admins can attach files to an assignment."));
        }

        var assignment = await _assignmentRepository.GetByIdAsync(command.AssignmentId, ct);
        if (assignment is null)
        {
            return Result<AssignmentFileDto>.Failure(Error.NotFound("Assignment.NotFound", "The specified assignment was not found."));
        }

        if (_currentUser.Role == Role.Teacher && !assignment.IsOwnedBy(_currentUser.UserId.GetValueOrDefault()))
        {
            return Result<AssignmentFileDto>.Failure(Error.Forbidden("AssignmentFile.Forbidden", "You do not have permission to attach files to this assignment."));
        }

        var existingCount = await _fileRepository.CountAsync(new AssignmentFilesByAssignmentSpecification(assignment.Id), ct);
        if (existingCount >= _uploadPolicy.MaxFilesPerAssignment)
        {
            return Result<AssignmentFileDto>.Failure(Error.Validation(
                "AssignmentFile.TooMany",
                $"An assignment may have at most {_uploadPolicy.MaxFilesPerAssignment} attachments."));
        }

        // Size, extension allow-list, and file signature — all server-side. The returned
        // content type is derived from the validated extension, never taken from the client.
        var validation = _uploadPolicy.Validate(command.FileName, command.SizeBytes, command.Content);
        if (!validation.IsSuccess)
        {
            return Result<AssignmentFileDto>.Failure(validation.Error);
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
            return Result<AssignmentFileDto>.Failure(Error.Validation("AssignmentFile.TooLarge", ex.Message));
        }

        try
        {
            var file = AssignmentFile.Create(
                assignment.Id,
                _currentUser.UserId.GetValueOrDefault(),
                savedFile.StoredFileName,
                command.FileName,
                validated.ContentType,
                savedFile.SizeBytes,
                savedFile.RelativePath,
                _clock.UtcNow);

            assignment.AttachFile(file);
            await _fileRepository.AddAsync(file, ct);
            _assignmentRepository.Update(assignment);

            await _unitOfWork.SaveChangesAsync(ct);

            return Mapper.MapToDto(file);
        }
        catch (DomainException ex)
        {
            // Cleanup written file if DB write fails
            _fileStorage.Delete(savedFile.RelativePath);
            return Result<AssignmentFileDto>.Failure(Error.Validation("AssignmentFile.Invalid", ex.Message));
        }
    }
}

public sealed class DownloadAssignmentFileHandler : IQueryHandler<DownloadAssignmentFileQuery, AssignmentFileDownloadResult>
{
    private readonly IRepository<AssignmentFile> _fileRepository;
    private readonly IClassRosterRepository _roster;
    private readonly IFileStorage _fileStorage;
    private readonly ICurrentUser _currentUser;

    public DownloadAssignmentFileHandler(
        IRepository<AssignmentFile> fileRepository,
        IClassRosterRepository roster,
        IFileStorage fileStorage,
        ICurrentUser currentUser)
    {
        _fileRepository = fileRepository;
        _roster = roster;
        _fileStorage = fileStorage;
        _currentUser = currentUser;
    }

    public async Task<Result<AssignmentFileDownloadResult>> HandleAsync(DownloadAssignmentFileQuery query, CancellationToken ct = default)
    {
        var spec = new AssignmentFileByIdSpecification(query.FileId);
        var file = await _fileRepository.FirstOrDefaultAsync(spec, ct);
        if (file is null)
        {
            return Result<AssignmentFileDownloadResult>.Failure(Error.NotFound("AssignmentFile.NotFound", "The specified file was not found."));
        }

        var assignment = file.Assignment;

        if (_currentUser.Role == Role.Teacher && !assignment.IsOwnedBy(_currentUser.UserId.GetValueOrDefault()))
        {
            return Result<AssignmentFileDownloadResult>.Failure(Error.Forbidden("AssignmentFile.Forbidden", "You do not have permission to download this file."));
        }

        if (_currentUser.Role == Role.Student)
        {
            // B1 + X3: enrolled in the offering's class, and the assignment published — a
            // draft's attachments are not visible either. Status is checked first because it
            // needs no query.
            var isEnrolled = assignment.Status == AssignmentStatus.Published
                && await _roster.IsEnrolledAsync(
                    _currentUser.UserId.GetValueOrDefault(), assignment.ClassCourse.ClassId, ct);

            if (!isEnrolled)
            {
                return Result<AssignmentFileDownloadResult>.Failure(Error.Forbidden("AssignmentFile.Forbidden", "You do not have permission to download this file."));
            }
        }

        try
        {
            var stream = _fileStorage.OpenRead(file.RelativePath);
            return new AssignmentFileDownloadResult(stream, file.ContentType, file.OriginalFileName);
        }
        catch (Exception ex)
        {
            return Result<AssignmentFileDownloadResult>.Failure(Error.Validation("AssignmentFile.ReadError", ex.Message));
        }
    }
}

public sealed class DeleteAssignmentFileHandler : ICommandHandler<DeleteAssignmentFileCommand>
{
    private readonly IRepository<AssignmentFile> _fileRepository;
    private readonly IFileStorage _fileStorage;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteAssignmentFileHandler(
        IRepository<AssignmentFile> fileRepository,
        IFileStorage fileStorage,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork)
    {
        _fileRepository = fileRepository;
        _fileStorage = fileStorage;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(DeleteAssignmentFileCommand command, CancellationToken ct = default)
    {
        var spec = new AssignmentFileByIdSpecification(command.FileId);
        var file = await _fileRepository.FirstOrDefaultAsync(spec, ct);
        if (file is null)
        {
            return Result.Failure(Error.NotFound("AssignmentFile.NotFound", "The specified file was not found."));
        }

        if (_currentUser.Role == Role.Teacher && !file.Assignment.IsOwnedBy(_currentUser.UserId.GetValueOrDefault()))
        {
            return Result.Failure(Error.Forbidden("AssignmentFile.Forbidden", "You do not have permission to delete this file."));
        }

        if (_currentUser.Role == Role.Student)
        {
            return Result.Failure(Error.Forbidden("AssignmentFile.Forbidden", "You do not have permission to delete this file."));
        }

        _fileStorage.Delete(file.RelativePath);
        _fileRepository.Remove(file);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}
