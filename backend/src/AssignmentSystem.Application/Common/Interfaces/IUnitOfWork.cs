using AssignmentSystem.Domain.Common;

namespace AssignmentSystem.Application.Common.Interfaces;

/// <summary>
/// Unit of Work: commits all repository changes in one transaction. Also dispatches
/// domain events raised by entities AFTER the commit succeeds (so events only fire
/// for persisted state). Beneficial here because handlers frequently touch multiple
/// aggregates atomically (e.g. create submission + bump assignment count).
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
