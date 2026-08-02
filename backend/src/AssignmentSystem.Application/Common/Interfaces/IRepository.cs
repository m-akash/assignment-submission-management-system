using AssignmentSystem.Domain.Common;

namespace AssignmentSystem.Application.Common.Interfaces;

/// <summary>
/// Generic repository port. Implements only data access — never business logic
/// (per the architecture contract). Persistence is the UnitOfWork's job, so this
/// interface has no SaveChanges. Specifications keep <see cref="IQueryable"/> from
/// leaking into the Application layer.
/// </summary>
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<T>> ListAsync(ISpecification<T> spec, CancellationToken ct = default);

    Task<Shared.Common.PageResult<T>> ListPagedAsync(ISpecification<T> spec, CancellationToken ct = default);

    Task<int> CountAsync(ISpecification<T> spec, CancellationToken ct = default);

    Task<bool> AnyAsync(ISpecification<T> spec, CancellationToken ct = default);

    Task<T?> FirstOrDefaultAsync(ISpecification<T> spec, CancellationToken ct = default);

    Task AddAsync(T entity, CancellationToken ct = default);

    void Update(T entity);

    void Remove(T entity);
}
