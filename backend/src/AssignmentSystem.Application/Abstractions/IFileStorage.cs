namespace AssignmentSystem.Application.Abstractions;

/// <summary>
/// Abstraction over the physical file store. Implementation: <c>LocalFileStorage</c>
/// writing to a configurable root (local disk / Docker volume). Backend is swappable
/// (S3/Azure Blob later) by reimplementing this one interface — which is why file
/// bytes never live in the DB.
/// </summary>
public interface IFileStorage
{
    /// <summary>Persists a stream under the storage root; returns the relative path + size.</summary>
    Task<SavedFile> SaveAsync(Stream content, string fileExtension, CancellationToken ct = default);

    /// <summary>Opens a read stream for a stored relative path.</summary>
    Stream OpenRead(string relativePath);

    /// <summary>Deletes a stored file (no-op if missing).</summary>
    void Delete(string relativePath);
}

public sealed record SavedFile(string RelativePath, string StoredFileName, long SizeBytes);
