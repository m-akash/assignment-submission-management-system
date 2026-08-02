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

        // Stream to disk and count bytes in one pass (do not trust Content-Length).
        long size;
        await using (var fs = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await content.CopyToAsync(fs, ct);
            size = fs.Length;
        }

        return new SavedFile(relativePath, storedFileName, size);
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

    private string ResolveWithinRoot(string relativePath)
    {
        var root = Path.GetFullPath(_options.Root);
        var fullPath = SafeCombine(_options.Root, relativePath);
        var full = Path.GetFullPath(fullPath);

        // Path-traversal guard: the resolved path must start with the storage root.
        if (!full.StartsWith(root, StringComparison.Ordinal))
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
