using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Application.Common.Authorization;
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
    private readonly IAssignmentAccess _access;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private static readonly AssignmentFileMapper Mapper = new();

    public UploadAssignmentFileHandler(
        IRepository<Assignment> assignmentRepository,
        IRepository<AssignmentFile> fileRepository,
        IFileStorage fileStorage,
        IFileUploadPolicy uploadPolicy,
        IAssignmentAccess access,
        ICurrentUser currentUser,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _assignmentRepository = assignmentRepository;
        _fileRepository = fileRepository;
        _fileStorage = fileStorage;
        _uploadPolicy = uploadPolicy;
        _access = access;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AssignmentFileDto>> HandleAsync(UploadAssignmentFileCommand command, CancellationToken ct = default)
    {
        var assignment = await _assignmentRepository.GetByIdAsync(command.AssignmentId, ct);
        if (assignment is null)
        {
            return Result<AssignmentFileDto>.Failure(Error.NotFound("Assignment.NotFound", "The specified assignment was not found."));
        }

        if (_access.MustBeAuthor(assignment) is { } denied)
        {
            return Result<AssignmentFileDto>.Failure(denied);
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
    private readonly IFileStorage _fileStorage;
    private readonly IAssignmentAccess _access;

    public DownloadAssignmentFileHandler(
        IRepository<AssignmentFile> fileRepository,
        IFileStorage fileStorage,
        IAssignmentAccess access)
    {
        _fileRepository = fileRepository;
        _fileStorage = fileStorage;
        _access = access;
    }

    public async Task<Result<AssignmentFileDownloadResult>> HandleAsync(DownloadAssignmentFileQuery query, CancellationToken ct = default)
    {
        var spec = new AssignmentFileByIdSpecification(query.FileId);
        var file = await _fileRepository.FirstOrDefaultAsync(spec, ct);
        if (file is null)
        {
            return Result<AssignmentFileDownloadResult>.Failure(Error.NotFound("AssignmentFile.NotFound", "The specified file was not found."));
        }

        // An attachment is only ever as visible as the assignment it hangs off, so this is
        // the same question the assignment itself would be asked.
        if (await _access.CanViewAsync(file.Assignment, ct) is { } denied)
        {
            return Result<AssignmentFileDownloadResult>.Failure(denied);
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
    private readonly IAssignmentAccess _access;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteAssignmentFileHandler(
        IRepository<AssignmentFile> fileRepository,
        IFileStorage fileStorage,
        IAssignmentAccess access,
        IUnitOfWork unitOfWork)
    {
        _fileRepository = fileRepository;
        _fileStorage = fileStorage;
        _access = access;
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

        if (_access.MustBeAuthor(file.Assignment) is { } denied)
        {
            return Result.Failure(denied);
        }

        _fileStorage.Delete(file.RelativePath);
        _fileRepository.Remove(file);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}
