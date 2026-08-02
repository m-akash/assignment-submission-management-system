namespace AssignmentSystem.Domain.Common;

/// <summary>
/// Marker implemented by entities that support soft delete (Assignment, User).
/// EF Core applies a global query filter so soft-deleted rows are hidden by default.
/// Soft delete is used only where it genuinely improves the design (restore,
/// referential history) — see the architecture document.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAtUtc { get; set; }
}
