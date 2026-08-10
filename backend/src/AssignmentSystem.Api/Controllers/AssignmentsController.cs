using AssignmentSystem.Api.Common;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Application.Features.Assignments;
using AssignmentSystem.Application.Features.AssignmentFiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AssignmentSystem.Api.Controllers;

[ApiController]
[Route("api/v1/assignments")]
[Authorize]
public sealed class AssignmentsController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public AssignmentsController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    [HttpGet]
    public async Task<IActionResult> GetAssignments(
        [FromQuery(Name = "classId")] Guid[]? classIds,
        [FromQuery(Name = "courseId")] Guid[]? courseIds,
        [FromQuery(Name = "classCourseId")] Guid[]? classCourseIds,
        [FromQuery(Name = "teacherId")] Guid[]? teacherIds,
        [FromQuery(Name = "status")] Domain.Enums.AssignmentStatus[]? statuses,
        [FromQuery] string? search,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDir,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new GetAssignmentsQuery(classIds, courseIds, classCourseIds, teacherIds, statuses, search, sortBy, sortDir, page, pageSize);
        var result = await _dispatcher.QueryAsync(query, ct);
        if (!result.IsSuccess)
        {
            return result.ToActionResult(this);
        }
        return ResultExtensions.PagedOk(this, result.Value!);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAssignmentById(Guid id, CancellationToken ct)
    {
        var result = await _dispatcher.QueryAsync(new GetAssignmentByIdQuery(id), ct);
        return result.ToActionResult(this);
    }

    [HttpPost]
    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> CreateAssignment([FromBody] CreateAssignmentRequest request, CancellationToken ct)
    {
        var command = new CreateAssignmentCommand(
            request.ClassCourseId,
            request.Title,
            request.Description,
            request.DeadlineUtc,
            request.MaxMarks,
            request.AllowResubmission);

        var result = await _dispatcher.SendAsync(command, ct);
        if (!result.IsSuccess)
        {
            return result.ToActionResult(this);
        }
        return CreatedAtAction(nameof(GetAssignmentById), new { id = result.Value!.Id }, new ApiResponse<AssignmentDto> { Success = true, Data = result.Value });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> UpdateAssignment(Guid id, [FromBody] UpdateAssignmentRequest request, CancellationToken ct)
    {
        var command = new UpdateAssignmentCommand(
            id,
            request.Title,
            request.Description,
            request.DeadlineUtc,
            request.MaxMarks,
            request.AllowResubmission);

        var result = await _dispatcher.SendAsync(command, ct);
        return result.ToActionResult(this);
    }

    [HttpPost("{id:guid}/publish")]
    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> PublishAssignment(Guid id, CancellationToken ct)
    {
        var result = await _dispatcher.SendAsync(new PublishAssignmentCommand(id), ct);
        return result.ToActionResult(this);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> DeleteAssignment(Guid id, CancellationToken ct)
    {
        var result = await _dispatcher.SendAsync(new DeleteAssignmentCommand(id), ct);
        return result.ToActionResult(this);
    }

    // ── Attachments (teacher-uploaded reference material) ──────────────────────

    // The request body limit comes from FileStorage:MaxBytes (wired to FormOptions in
    // Program.cs) rather than a hard-coded attribute, so one setting governs the cap.
    [HttpPost("{id:guid}/attachments/upload")]
    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> UploadAttachment(Guid id, [FromForm] FileUploadRequest request, CancellationToken ct)
    {
        var file = request.File;
        if (file is null || file.Length == 0)
        {
            return BadRequest(new ApiResponse<object> { Success = false, Message = "No file uploaded." });
        }

        await using var stream = file.OpenReadStream();
        var command = new UploadAssignmentFileCommand(id, file.FileName, file.Length, stream);
        var result = await _dispatcher.SendAsync(command, ct);
        return result.ToActionResult(this);
    }

    [HttpGet("attachments/{fileId:guid}")]
    public async Task<IActionResult> DownloadAttachment(Guid fileId, CancellationToken ct)
    {
        var result = await _dispatcher.QueryAsync(new DownloadAssignmentFileQuery(fileId), ct);
        if (!result.IsSuccess)
        {
            return result.ToActionResult(this);
        }

        var fileStream = result.Value!.Stream;
        return File(fileStream, result.Value.ContentType, result.Value.FileName, enableRangeProcessing: true);
    }

    /// <summary>
    /// Renames an attachment. The stored file is untouched — this changes only the name
    /// students see and download under, and the extension comes back unchanged whatever
    /// the request asks for.
    /// </summary>
    [HttpPut("attachments/{fileId:guid}")]
    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> RenameAttachment(Guid fileId, [FromBody] RenameFileRequest request, CancellationToken ct)
    {
        var result = await _dispatcher.SendAsync(new RenameAssignmentFileCommand(fileId, request.FileName), ct);
        return result.ToActionResult(this);
    }

    [HttpDelete("attachments/{fileId:guid}")]
    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> DeleteAttachment(Guid fileId, CancellationToken ct)
    {
        var result = await _dispatcher.SendAsync(new DeleteAssignmentFileCommand(fileId), ct);
        return result.ToActionResult(this);
    }
}

public sealed record CreateAssignmentRequest(
    Guid ClassCourseId,
    string Title,
    string Description,
    DateTime DeadlineUtc,
    decimal MaxMarks,
    bool AllowResubmission);

public sealed record UpdateAssignmentRequest(
    string Title,
    string Description,
    DateTime DeadlineUtc,
    decimal MaxMarks,
    bool AllowResubmission);

