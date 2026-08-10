using Microsoft.AspNetCore.Mvc;

namespace AssignmentSystem.Api.Common;

/// <summary>
/// Wrapper for single-file form uploads. Swashbuckle cannot describe a bare
/// <c>[FromForm] IFormFile</c> parameter and throws while generating the OpenAPI
/// document; binding the file through a DTO lets it emit the correct
/// <c>multipart/form-data</c> schema. The wire contract (a single <c>file</c> part) is
/// unchanged — <see cref="File"/> binds from the lower-case <c>file</c> form field the
/// clients already send.
/// </summary>
public sealed class FileUploadRequest
{
    [FromForm(Name = "file")]
    public IFormFile File { get; init; } = null!;
}

/// <summary>
/// The body of a rename. Shared by both attachment kinds — an assignment's material and a
/// submission's files rename identically, and only the route says which is which.
/// </summary>
public sealed record RenameFileRequest(string FileName);
