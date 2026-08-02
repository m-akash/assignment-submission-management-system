using AssignmentSystem.Domain.Common;

namespace AssignmentSystem.Application.Common.Interfaces;

/// <summary>
/// Dispatches domain events after they belong to persisted state. A null-object
/// (no-op) default is registered until concrete event handlers exist; the UnitOfWork
/// tolerates a null dispatcher so this never blocks persistence.
/// </summary>
public interface IDomainEventDispatcher
{
    Task DispatchAsync(IReadOnlyCollection<IDomainEvent> events, CancellationToken ct = default);
}
