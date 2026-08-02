using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.Common;

namespace AssignmentSystem.Infrastructure.Persistence.Repositories;

/// <summary>
/// Default no-op domain event dispatcher. Registered until concrete event handlers
/// exist; ensures SaveChanges never blocks on dispatch.
/// </summary>
internal sealed class NullDomainEventDispatcher : IDomainEventDispatcher
{
    public Task DispatchAsync(IReadOnlyCollection<IDomainEvent> events, CancellationToken ct = default) =>
        Task.CompletedTask;
}
