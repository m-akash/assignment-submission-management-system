namespace AssignmentSystem.Domain.Common;

/// <summary>
/// Base for every persisted domain entity. Centralises identity, audit fields, and
/// the optimistic-concurrency token. Domain behaviour mutates entities through
/// methods (never public setters) so invariants are guaranteed at the source.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();

    // ── Audit fields ─────────────────────────────────────────────────────────
    // Populated by the AuditSaveChangesInterceptor reading ICurrentUser — never
    // trusted from the client. CreatedBy/UpdatedBy store the immutable user Guid.
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    // ── Optimistic concurrency ───────────────────────────────────────────────
    // Mapped to Postgres row version (xmin) per-entity where concurrency matters.
    public uint RowVersion { get; set; }
}

// A domain-event mechanism used to live here — a collection on every entity, dispatch after
// commit in the UnitOfWork, the lot. No entity ever raised one, and the obvious consumer
// (notifications) goes through the transactional outbox instead, which gives a stronger
// guarantee than in-process events could: the message is committed with the change that
// caused it and survives a crash. Rather than leave the scaffolding standing, it was removed
// — the UnitOfWork no longer walks the ChangeTracker on every save looking for events that
// could not exist. If this system ever needs decoupled in-process reactions, the outbox is
// the pattern to extend; see INotificationOutbox.
