using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Shared.Common;
using Microsoft.Extensions.Options;

namespace AssignmentSystem.Infrastructure.Storage;

/// <summary>
/// Config-driven upload validation: size ceiling, extension allow-list, file-signature
/// (magic byte) check, and server-derived MIME type.
///
/// The signature check is what stops a renamed executable. Types with no reliable header
/// (plain text) are instead required to look like text, so <c>payload.exe → notes.txt</c>
/// is rejected rather than waved through.
/// </summary>
internal sealed class FileUploadPolicy : IFileUploadPolicy
{
    /// <summary>Enough bytes for every signature below.</summary>
    private const int HeaderBytes = 8;

    private readonly FileStorageOptions _options;

    public FileUploadPolicy(IOptions<FileStorageOptions> options)
    {
        _options = options.Value;
        AllowedExtensions = _options.AllowedExtensions
            .Select(NormalizeExtension)
            .Where(e => e.Length > 1)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    public long MaxBytes => _options.MaxBytes;

    public int MaxFilesPerSubmission => _options.MaxFilesPerSubmission;

    public int MaxFilesPerAssignment => _options.MaxFilesPerAssignment;

    public IReadOnlyList<string> AllowedExtensions { get; }

    public Result<ValidatedUpload> Validate(string fileName, long sizeBytes, Stream content)
    {
        if (sizeBytes <= 0)
        {
            return Result<ValidatedUpload>.Failure(
                Error.Validation("SubmissionFile.Empty", "The file is empty."));
        }

        if (sizeBytes > MaxBytes)
        {
            return Result<ValidatedUpload>.Failure(Error.Validation(
                "SubmissionFile.TooLarge",
                $"The file exceeds the maximum allowed size of {MaxBytes / (1024 * 1024)} MB."));
        }

        var extension = NormalizeExtension(Path.GetExtension(fileName));
        if (!AllowedExtensions.Contains(extension, StringComparer.Ordinal))
        {
            return Result<ValidatedUpload>.Failure(Error.Validation(
                "SubmissionFile.InvalidExtension",
                $"File type '{extension}' is not allowed. Permitted types: {string.Join(", ", AllowedExtensions)}."));
        }

        if (!HasMatchingSignature(extension, content))
        {
            return Result<ValidatedUpload>.Failure(Error.Validation(
                "SubmissionFile.InvalidContent",
                "The file contents do not match its extension."));
        }

        return new ValidatedUpload(extension, ContentTypeFor(extension));
    }

    // ── Signatures ────────────────────────────────────────────────────────────

    /// <summary>Leading bytes each format must begin with. Order matters only for lookup.</summary>
    private static readonly Dictionary<string, byte[][]> Signatures = new(StringComparer.Ordinal)
    {
        [".pdf"] = [[0x25, 0x50, 0x44, 0x46]],                              // %PDF
        [".docx"] = [[0x50, 0x4B, 0x03, 0x04], [0x50, 0x4B, 0x05, 0x06]],   // ZIP (OOXML)
        [".doc"] = [[0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1]],      // OLE2 compound file
        [".png"] = [[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]],
        [".jpg"] = [[0xFF, 0xD8, 0xFF]],
        [".jpeg"] = [[0xFF, 0xD8, 0xFF]],
    };

    private static bool HasMatchingSignature(string extension, Stream content)
    {
        var header = ReadHeader(content);
        if (header.Length == 0)
        {
            return false;
        }

        if (Signatures.TryGetValue(extension, out var candidates))
        {
            return candidates.Any(signature => StartsWith(header, signature));
        }

        // No signature exists for this type (plain text). Require it to actually look like
        // text: a NUL byte in the header is the cheapest reliable tell for a binary payload.
        return !header.Contains((byte)0x00);
    }

    private static byte[] ReadHeader(Stream content)
    {
        if (!content.CanSeek)
        {
            // Cannot inspect without consuming the stream the caller still needs to store.
            return [];
        }

        var origin = content.Position;
        try
        {
            var buffer = new byte[HeaderBytes];
            var read = content.ReadAtLeast(buffer, HeaderBytes, throwOnEndOfStream: false);
            return buffer[..read];
        }
        finally
        {
            content.Position = origin;
        }
    }

    private static bool StartsWith(byte[] header, byte[] signature) =>
        header.Length >= signature.Length && header.AsSpan(0, signature.Length).SequenceEqual(signature);

    // ── MIME ──────────────────────────────────────────────────────────────────

    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.Ordinal)
    {
        [".pdf"] = "application/pdf",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".txt"] = "text/plain",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
    };

    /// <summary>Unknown-but-allowed types download as a generic binary rather than rendering.</summary>
    private static string ContentTypeFor(string extension) =>
        ContentTypes.TryGetValue(extension, out var contentType) ? contentType : "application/octet-stream";

    private static string NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return string.Empty;
        }

        var trimmed = extension.Trim().ToLowerInvariant();
        return trimmed.StartsWith('.') ? trimmed : "." + trimmed;
    }
}
