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

    // ── Domain events ────────────────────────────────────────────────────────
    // Cleared by the UnitOfWork after SaveChanges succeeds.
    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
