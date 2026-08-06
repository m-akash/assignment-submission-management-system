using AssignmentSystem.Application.Common.Interfaces;

namespace AssignmentSystem.Infrastructure.Persistence.Repositories;

/// <summary>
/// Unit of Work: wraps SaveChanges so a handler's multiple writes — including the
/// notification rows queued alongside them — commit atomically.
/// </summary>
internal sealed class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    public UnitOfWork(AppDbContext context) => _context = context;

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _context.SaveChangesAsync(ct);
}
