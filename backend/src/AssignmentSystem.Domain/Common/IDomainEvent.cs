namespace AssignmentSystem.Domain.Common;

/// <summary>
/// Marker for domain events. Raised by entities when something domain-significant
/// happens (e.g. assignment published, submission graded). Dispatched after
/// SaveChanges by the UnitOfWork so events are only published for persisted state.
/// </summary>
public interface IDomainEvent
{
    DateTime OccurredOnUtc { get; }
}
