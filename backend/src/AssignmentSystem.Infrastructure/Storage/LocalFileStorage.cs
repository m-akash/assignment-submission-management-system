using AssignmentSystem.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace AssignmentSystem.Infrastructure.Storage;

/// <summary>
/// File storage backed by the local filesystem (dev) or a Docker volume (prod).
/// Files are stored under {Root}/{yyyy}/{mm}/{guid}.{ext}. RelativePath returned to
/// the caller is always normalized and confirmed to resolve inside the root, making
/// path traversal impossible on download.
/// </summary>
internal sealed class LocalFileStorage : IFileStorage
{
    private readonly FileStorageOptions _options;

    public LocalFileStorage(IOptions<FileStorageOptions> options) => _options = options.Value;

    public async Task<SavedFile> SaveAsync(Stream content, string fileExtension, CancellationToken ct = default)
    {
        EnsureRootExists();

        var ext = NormalizeExtension(fileExtension);
        var storedFileName = $"{Guid.NewGuid():N}{ext}";
        var now = DateTime.UtcNow;
        var relativeDir = Path.Combine(
            now.ToString("yyyy", System.Globalization.CultureInfo.InvariantCulture),
            now.ToString("MM", System.Globalization.CultureInfo.InvariantCulture));
        var relativePath = Path.Combine(relativeDir, storedFileName).Replace('\\', '/');

        var fullDir = SafeCombine(_options.Root, relativeDir);
        Directory.CreateDirectory(fullDir);

        var fullPath = SafeCombine(_options.Root, relativePath);

        // Stream to disk and count bytes in one pass (do not trust Content-Length), aborting
        // the moment the configured ceiling is crossed so an oversized upload cannot fill the
        // volume even if an earlier check was bypassed.
        long size;
        try
        {
            await using var destination = new FileStream(
                fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);

            size = await CopyWithLimitAsync(content, destination, _options.MaxBytes, ct);
        }
        catch
        {
            TryDelete(fullPath);
            throw;
        }

        return new SavedFile(relativePath, storedFileName, size);
    }

    /// <summary>
    /// Copies at most <paramref name="maxBytes"/>, throwing as soon as one byte more arrives.
    /// Returns the number of bytes written.
    /// </summary>
    private static async Task<long> CopyWithLimitAsync(Stream source, Stream destination, long maxBytes, CancellationToken ct)
    {
        var buffer = new byte[81920];
        long total = 0;

        int read;
        while ((read = await source.ReadAsync(buffer, ct)) > 0)
        {
            total += read;
            if (total > maxBytes)
            {
                throw new FileTooLargeException(maxBytes);
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), ct);
        }

        return total;
    }

    private static void TryDelete(string fullPath)
    {
        try
        {
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
        catch (IOException)
        {
            // Cleanup is best effort — never mask the original failure.
        }
    }

    public Stream OpenRead(string relativePath)
    {
        var fullPath = ResolveWithinRoot(relativePath);
        return new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);
    }

    public void Delete(string relativePath)
    {
        var fullPath = ResolveWithinRoot(relativePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }

    /// <summary>
    /// Windows paths are case-insensitive; Linux paths are not. Comparing with the wrong
    /// rule either lets a traversal through or rejects a legitimate path.
    /// </summary>
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private string ResolveWithinRoot(string relativePath)
    {
        var root = Path.GetFullPath(_options.Root);
        var full = Path.GetFullPath(SafeCombine(root, relativePath));

        // Compare against the root *plus its separator*. A bare prefix check would accept
        // "/data/submissions-evil/secret" as being inside "/data/submissions".
        var rootBoundary = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        if (!full.StartsWith(rootBoundary, PathComparison))
        {
            throw new UnauthorizedAccessException("Access to the requested file path is denied.");
        }

        return full;
    }

    /// <summary>Combines and normalizes, refusing absolute paths in <paramref name="relative"/>.</summary>
    private static string SafeCombine(string root, string relative)
    {
        if (Path.IsPathRooted(relative))
        {
            throw new UnauthorizedAccessException("Absolute file paths are not permitted.");
        }

        return Path.Combine(root, relative);
    }

    private void EnsureRootExists()
    {
        if (!Directory.Exists(_options.Root))
        {
            Directory.CreateDirectory(_options.Root);
        }
    }

    private static string NormalizeExtension(string ext)
    {
        if (string.IsNullOrWhiteSpace(ext))
        {
            return string.Empty;
        }

        ext = ext.Trim().ToLowerInvariant();
        return ext.StartsWith('.') ? ext : "." + ext;
    }
}
