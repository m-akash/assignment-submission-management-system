namespace AssignmentSystem.Domain.Common;

/// <summary>
/// The rules a user-facing file name obeys. Both attachment kinds — an assignment's
/// material and a student's submission — were sanitising names with the same private
/// copy of this code, and a rename has to reach exactly the same conclusion an upload
/// did, so the rules live in one place.
///
/// Nothing here touches stored bytes: <c>StoredFileName</c> is server-generated and
/// never renamed. This is the label a person reads and downloads under.
/// </summary>
public static class FileNames
{
    /// <summary>Matches the column width for both <c>OriginalFileName</c> mappings.</summary>
    public const int MaxLength = 255;

    /// <summary>Whatever a name is when nothing usable survives sanitising.</summary>
    private const string Fallback = "file";

    /// <summary>
    /// A name safe to store and to hand back in a download header: no directory
    /// components, nothing a filesystem rejects, and bounded in length.
    /// </summary>
    public static string Sanitize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Fallback;
        }

        // Strip any directory components — keep only the file name portion.
        var fileName = name.Replace('\\', '/').Split('/').Last();
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(c, '_');
        }

        fileName = fileName.Trim();
        if (fileName.Length == 0)
        {
            return Fallback;
        }

        return fileName.Length > MaxLength ? fileName[..MaxLength] : fileName;
    }

    /// <summary>
    /// The requested name, carrying the extension of the file actually on disk.
    ///
    /// This is what makes renaming safe. The extension was derived from the uploaded
    /// bytes — the upload policy reads the file's signature and refuses anything whose
    /// contents disagree with it — so a later rename may change the label but never the
    /// claim about what the file *is*. A <c>.pdf</c> cannot become a <c>.exe</c> by
    /// being renamed, and a download keeps opening in the right thing.
    /// </summary>
    public static string WithExtensionOf(string storedFileName, string requestedName)
    {
        var extension = Path.GetExtension(storedFileName);
        var sanitized = Sanitize(requestedName);

        var baseName = Path.GetFileNameWithoutExtension(sanitized).Trim();
        if (baseName.Length == 0)
        {
            baseName = Fallback;
        }

        // The extension is not negotiable, so it is the base name that gives way.
        var room = MaxLength - extension.Length;
        if (baseName.Length > room)
        {
            baseName = baseName[..room];
        }

        return baseName + extension;
    }
}
