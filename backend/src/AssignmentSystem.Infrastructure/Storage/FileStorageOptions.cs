namespace AssignmentSystem.Infrastructure.Storage;

/// <summary>
/// Submission file storage configuration bound from "FileStorage" section.
/// Root is the on-disk path (local dev: ./_uploads; Docker: /data/submissions volume).
/// </summary>
internal sealed class FileStorageOptions
{
    public string Root { get; init; } = "../_uploads";
    public long MaxBytes { get; init; } = 10 * 1024 * 1024; // 10 MB
    public int MaxFilesPerSubmission { get; init; } = 3;
    public int MaxFilesPerAssignment { get; init; } = 5;
    /// <summary>
    /// Extensions accepted on upload. Enforced together with a file-signature check, so a
    /// type is only listed here if its bytes can be verified. Archives are excluded
    /// deliberately: a signature check cannot see what is inside them.
    /// </summary>
    public IReadOnlyList<string> AllowedExtensions { get; init; } = [".pdf", ".docx", ".doc", ".txt", ".png", ".jpg", ".jpeg"];
}
