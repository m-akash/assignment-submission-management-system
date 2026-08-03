using AssignmentSystem.Shared.Common;

namespace AssignmentSystem.Application.Abstractions;

/// <summary>
/// The server's rules for what may be attached to a submission. Everything a client
/// sends about a file — its name, its declared content type, its length — is a claim;
/// this port is where those claims are checked against configuration and against the
/// bytes themselves.
/// </summary>
public interface IFileUploadPolicy
{
    long MaxBytes { get; }

    int MaxFilesPerSubmission { get; }

    IReadOnlyList<string> AllowedExtensions { get; }

    /// <summary>
    /// Validates size, extension allow-list and file signature, and returns the
    /// server-derived content type. The client's <c>Content-Type</c> header is never
    /// echoed back: it would let an uploader choose how the browser renders the
    /// download (e.g. a "text file" served as HTML).
    /// </summary>
    Result<ValidatedUpload> Validate(string fileName, long sizeBytes, Stream content);
}

/// <summary>The canonical extension and MIME type the server decided on.</summary>
public sealed record ValidatedUpload(string Extension, string ContentType);
