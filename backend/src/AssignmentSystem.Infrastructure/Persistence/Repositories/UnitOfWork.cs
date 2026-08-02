using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace AssignmentSystem.Infrastructure.Persistence.Repositories;

/// <summary>
/// Unit of Work: wraps SaveChanges so a handler's multiple writes commit atomically,
/// then dispatches domain events raised by the affected entities. Events fire only
/// after the commit succeeds, guaranteeing they correspond to persisted state.
/// </summary>
internal sealed class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private readonly IDomainEventDispatcher? _dispatcher;

    public UnitOfWork(AppDbContext context, IDomainEventDispatcher? dispatcher = null)
    {
        _context = context;
        _dispatcher = dispatcher;
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var entitiesWithEvents = _context.ChangeTracker
            .Entries<BaseEntity>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        var result = await _context.SaveChangesAsync(ct);

        if (_dispatcher is not null && entitiesWithEvents.Count > 0)
        {
            var events = entitiesWithEvents.SelectMany(e => e.DomainEvents).ToList();
            foreach (var entity in entitiesWithEvents)
            {
                entity.ClearDomainEvents();
            }
            await _dispatcher.DispatchAsync(events, ct);
        }

        return result;
    }
}
