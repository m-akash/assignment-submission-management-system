using AssignmentSystem.Domain.Common;
using AssignmentSystem.Domain.Users;

namespace AssignmentSystem.Domain.Submissions;

/// <summary>
/// Metadata for a file attached to a submission. The file BYTES live on disk (or a
/// Docker volume) via <c>IFileStorage</c> — never in this row. One submission may
/// have many files (so resubmission keeps history). Path is stored relative to the
/// storage root so the store can move hosts/backends.
/// </summary>
public sealed class SubmissionFile : BaseEntity
{
    public Guid SubmissionId { get; private set; }
    public Submission Submission { get; private set; } = null!;

    public Guid UploadedById { get; private set; }
    public ApplicationUser UploadedBy { get; private set; } = null!;

    /// <summary>Server-generated unique stored name (e.g. "&lt;guid&gt;.pdf").</summary>
    public string StoredFileName { get; private set; } = null!;

    /// <summary>Sanitized user-facing original filename.</summary>
    public string OriginalFileName { get; private set; } = null!;

    public string ContentType { get; private set; } = null!;
    public long FileSizeBytes { get; private set; }

    /// <summary>Path under the storage root (no host-specific prefix).</summary>
    public string RelativePath { get; private set; } = null!;

    public DateTime UploadedAtUtc { get; private set; }

    private SubmissionFile() { }

    public static SubmissionFile Create(
        Guid submissionId,
        Guid uploadedById,
        string storedFileName,
        string originalFileName,
        string contentType,
        long fileSizeBytes,
        string relativePath,
        DateTime uploadedAtUtc)
    {
        if (submissionId == Guid.Empty || uploadedById == Guid.Empty)
        {
            throw new DomainException("Submission id and uploader id are required.");
        }

        if (string.IsNullOrWhiteSpace(storedFileName) || string.IsNullOrWhiteSpace(relativePath))
        {
            throw new DomainException("Stored file name and relative path are required.");
        }

        if (fileSizeBytes <= 0)
        {
            throw new DomainException("File size must be greater than zero.");
        }

        return new SubmissionFile
        {
            SubmissionId = submissionId,
            UploadedById = uploadedById,
            StoredFileName = storedFileName,
            OriginalFileName = SanitizeOriginalFileName(originalFileName),
            ContentType = contentType,
            FileSizeBytes = fileSizeBytes,
            RelativePath = relativePath,
            UploadedAtUtc = uploadedAtUtc,
        };
    }

    private static string SanitizeOriginalFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "file";
        }

        // strip any directory components — keep only the file name portion
        var fileName = name.Replace('\\', '/').Split('/').Last();
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(c, '_');
        }

        return fileName.Length > 255 ? fileName[..255] : fileName;
    }
}
