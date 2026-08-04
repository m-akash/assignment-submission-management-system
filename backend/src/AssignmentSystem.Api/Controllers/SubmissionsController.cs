using AssignmentSystem.Api.Common;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Application.Features.Submissions;
using AssignmentSystem.Domain.Enums;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AssignmentSystem.Api.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize]
public sealed class SubmissionsController : ControllerBase
{
    private readonly ICommandHandler<SubmitAssignmentCommand, SubmissionDto> _submitHandler;
    private readonly ICommandHandler<UpdateSubmissionCommand, SubmissionDto> _updateHandler;
    private readonly ICommandHandler<ReviewSubmissionCommand, SubmissionDto> _reviewHandler;
    private readonly IQueryHandler<GetSubmissionByIdQuery, SubmissionDto> _getByIdHandler;
    private readonly IQueryHandler<GetStudentSubmissionQuery, SubmissionDto> _getStudentSubmissionHandler;
    private readonly IQueryHandler<GetSubmissionsQuery, Shared.Common.PageResult<SubmissionDto>> _getListHandler;
    private readonly ICommandHandler<UploadSubmissionFileCommand, SubmissionFileDto> _uploadFileHandler;
    private readonly IQueryHandler<DownloadSubmissionFileQuery, SubmissionFileDownloadResult> _downloadFileHandler;
    private readonly ICommandHandler<DeleteSubmissionFileCommand> _deleteFileHandler;

    public SubmissionsController(
        ICommandHandler<SubmitAssignmentCommand, SubmissionDto> submitHandler,
        ICommandHandler<UpdateSubmissionCommand, SubmissionDto> updateHandler,
        ICommandHandler<ReviewSubmissionCommand, SubmissionDto> reviewHandler,
        IQueryHandler<GetSubmissionByIdQuery, SubmissionDto> getByIdHandler,
        IQueryHandler<GetStudentSubmissionQuery, SubmissionDto> getStudentSubmissionHandler,
        IQueryHandler<GetSubmissionsQuery, Shared.Common.PageResult<SubmissionDto>> getListHandler,
        ICommandHandler<UploadSubmissionFileCommand, SubmissionFileDto> uploadFileHandler,
        IQueryHandler<DownloadSubmissionFileQuery, SubmissionFileDownloadResult> downloadFileHandler,
        ICommandHandler<DeleteSubmissionFileCommand> deleteFileHandler)
    {
        _submitHandler = submitHandler;
        _updateHandler = updateHandler;
        _reviewHandler = reviewHandler;
        _getByIdHandler = getByIdHandler;
        _getStudentSubmissionHandler = getStudentSubmissionHandler;
        _getListHandler = getListHandler;
        _uploadFileHandler = uploadFileHandler;
        _downloadFileHandler = downloadFileHandler;
        _deleteFileHandler = deleteFileHandler;
    }

    [HttpGet("submissions")]
    public async Task<IActionResult> GetSubmissions(
        [FromQuery] Guid? assignmentId,
        [FromQuery] Guid? studentId,
        [FromQuery] SubmissionStatus? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new GetSubmissionsQuery(assignmentId, studentId, status, search, page, pageSize);
        var result = await _getListHandler.HandleAsync(query, ct);
        if (!result.IsSuccess)
        {
            return result.ToActionResult(this);
        }
        return ResultExtensions.PagedOk(this, result.Value!);
    }

    [HttpGet("submissions/{id:guid}")]
    public async Task<IActionResult> GetSubmissionById(Guid id, CancellationToken ct)
    {
        var result = await _getByIdHandler.HandleAsync(new GetSubmissionByIdQuery(id), ct);
        return result.ToActionResult(this);
    }

    [HttpGet("assignments/{assignmentId:guid}/submissions/me")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetMySubmission(Guid assignmentId, CancellationToken ct)
    {
        var result = await _getStudentSubmissionHandler.HandleAsync(new GetStudentSubmissionQuery(assignmentId), ct);
        return result.ToActionResult(this);
    }

    [HttpPost("assignments/{assignmentId:guid}/submissions")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> SubmitAssignment(Guid assignmentId, [FromBody] SubmitAssignmentRequest request, CancellationToken ct)
    {
        var command = new SubmitAssignmentCommand(assignmentId, request.Content);
        var result = await _submitHandler.HandleAsync(command, ct);
        if (!result.IsSuccess)
        {
            return result.ToActionResult(this);
        }
        return CreatedAtAction(nameof(GetSubmissionById), new { id = result.Value!.Id }, new ApiResponse<SubmissionDto> { Success = true, Data = result.Value });
    }

    [HttpPut("submissions/{id:guid}")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> UpdateSubmission(Guid id, [FromBody] UpdateSubmissionRequest request, CancellationToken ct)
    {
        var command = new UpdateSubmissionCommand(id, request.Content);
        var result = await _updateHandler.HandleAsync(command, ct);
        return result.ToActionResult(this);
    }

    [HttpPost("submissions/{id:guid}/review")]
    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> ReviewSubmission(Guid id, [FromBody] ReviewSubmissionRequest request, CancellationToken ct)
    {
        var command = new ReviewSubmissionCommand(id, request.Marks, request.Feedback, request.Status);
        var result = await _reviewHandler.HandleAsync(command, ct);
        return result.ToActionResult(this);
    }

    // ── File upload / download / delete ───────────────────────────────────────

    // The request body limit comes from FileStorage:MaxBytes (wired to FormOptions in
    // Program.cs) rather than a hard-coded attribute, so one setting governs the cap.
    [HttpPost("assignments/{assignmentId:guid}/submissions/upload")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> UploadFile(Guid assignmentId, [FromForm] FileUploadRequest request, CancellationToken ct)
    {
        var file = request.File;
        if (file is null || file.Length == 0)
        {
            return BadRequest(new ApiResponse<object> { Success = false, Message = "No file uploaded." });
        }

        await using var stream = file.OpenReadStream();
        var command = new UploadSubmissionFileCommand(assignmentId, file.FileName, file.Length, stream);
        var result = await _uploadFileHandler.HandleAsync(command, ct);
        return result.ToActionResult(this);
    }

    [HttpGet("submissions/files/{fileId:guid}")]
    public async Task<IActionResult> DownloadFile(Guid fileId, CancellationToken ct)
    {
        var result = await _downloadFileHandler.HandleAsync(new DownloadSubmissionFileQuery(fileId), ct);
        if (!result.IsSuccess)
        {
            return result.ToActionResult(this);
        }

        var fileStream = result.Value!.Stream;
        // Streams the file with safe attachment headers
        return File(fileStream, result.Value.ContentType, result.Value.FileName, enableRangeProcessing: true);
    }

    [HttpDelete("submissions/files/{fileId:guid}")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> DeleteFile(Guid fileId, CancellationToken ct)
    {
        var result = await _deleteFileHandler.HandleAsync(new DeleteSubmissionFileCommand(fileId), ct);
        return result.ToActionResult(this);
    }
}

/// <summary>
/// Attachments are referenced implicitly: whatever the student has already uploaded for
/// this assignment is part of the submission. The request carries no file ids, so it
/// cannot claim files it does not own.
/// </summary>
public sealed record SubmitAssignmentRequest(string? Content);
public sealed record UpdateSubmissionRequest(string? Content);
public sealed record ReviewSubmissionRequest(decimal Marks, string? Feedback, SubmissionStatus Status);

public sealed class ReviewSubmissionRequestValidator : AbstractValidator<ReviewSubmissionRequest>
{
    public ReviewSubmissionRequestValidator()
    {
        RuleFor(x => x.Marks)
            .GreaterThanOrEqualTo(0).WithMessage("Marks cannot be negative.");

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("A valid submission status is required.");
    }
}
