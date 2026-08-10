using AssignmentSystem.Domain.Common;
using AssignmentSystem.Domain.Users;

namespace AssignmentSystem.Domain.Assignments;

/// <summary>
/// Metadata for a file the teacher attaches to an assignment (e.g. a worksheet or
/// reference PDF) — distinct from <c>SubmissionFile</c>, which a student attaches to
/// their own submission. The file BYTES live on disk via <c>IFileStorage</c>, never
/// in this row. One assignment may have many files.
/// </summary>
public sealed class AssignmentFile : BaseEntity
{
    public Guid AssignmentId { get; private set; }
    public Assignment Assignment { get; private set; } = null!;

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

    private AssignmentFile() { }

    public static AssignmentFile Create(
        Guid assignmentId,
        Guid uploadedById,
        string storedFileName,
        string originalFileName,
        string contentType,
        long fileSizeBytes,
        string relativePath,
        DateTime uploadedAtUtc)
    {
        if (assignmentId == Guid.Empty || uploadedById == Guid.Empty)
        {
            throw new DomainException("Assignment id and uploader id are required.");
        }

        if (string.IsNullOrWhiteSpace(storedFileName) || string.IsNullOrWhiteSpace(relativePath))
        {
            throw new DomainException("Stored file name and relative path are required.");
        }

        if (fileSizeBytes <= 0)
        {
            throw new DomainException("File size must be greater than zero.");
        }

        return new AssignmentFile
        {
            AssignmentId = assignmentId,
            UploadedById = uploadedById,
            StoredFileName = storedFileName,
            OriginalFileName = FileNames.Sanitize(originalFileName),
            ContentType = contentType,
            FileSizeBytes = fileSizeBytes,
            RelativePath = relativePath,
            UploadedAtUtc = uploadedAtUtc,
        };
    }

    /// <summary>
    /// Relabels the file. Only the name changes: the bytes, the stored name and the
    /// extension all stay as they were, so what students download is the same file under
    /// the title the teacher meant to give it.
    /// </summary>
    public void Rename(string originalFileName)
    {
        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            throw new DomainException("A file name is required.");
        }

        OriginalFileName = FileNames.WithExtensionOf(StoredFileName, originalFileName);
    }
}
