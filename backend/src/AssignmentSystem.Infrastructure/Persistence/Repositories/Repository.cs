using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.Common;
using AssignmentSystem.Shared.Common;
using Microsoft.EntityFrameworkCore;

namespace AssignmentSystem.Infrastructure.Persistence.Repositories;

/// <summary>
/// Generic repository implementation. Data access only — no business logic. Paging
/// returns a <see cref="PageResult{T}"/>; the paged query applies Skip/Take via the
/// specification evaluator.
/// </summary>
internal sealed class Repository<T> : IRepository<T> where T : class
{
    private readonly AppDbContext _context;

    public Repository(AppDbContext context) => _context = context;

    private DbSet<T> Set => _context.Set<T>();

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await Set.FindAsync(new object?[] { id }, ct);

    public async Task<IReadOnlyList<T>> ListAsync(ISpecification<T> spec, CancellationToken ct = default) =>
        await SpecificationEvaluator.Apply(Set.AsQueryable(), spec).ToListAsync(ct);

    public async Task<PageResult<T>> ListPagedAsync(ISpecification<T> spec, CancellationToken ct = default)
    {
        var baseQuery = SpecificationEvaluator.Apply(Set.AsQueryable(), spec);

        // Clone criteria for the count without paging/order/includes.
        var countQuery = Set.AsQueryable();
        if (spec.Criteria is not null)
        {
            countQuery = countQuery.Where(spec.Criteria);
        }
        var total = await countQuery.CountAsync(ct);

        var items = await baseQuery.ToListAsync(ct);
        var page = spec.Page ?? 1;
        var pageSize = spec.PageSize ?? items.Count;
        return new PageResult<T>(items, page, pageSize, total);
    }

    public async Task<int> CountAsync(ISpecification<T> spec, CancellationToken ct = default)
    {
        var query = Set.AsQueryable();
        if (spec.Criteria is not null)
        {
            query = query.Where(spec.Criteria);
        }
        return await query.CountAsync(ct);
    }

    public async Task<bool> AnyAsync(ISpecification<T> spec, CancellationToken ct = default)
    {
        var query = Set.AsQueryable();
        if (spec.Criteria is not null)
        {
            query = query.Where(spec.Criteria);
        }
        return await query.AnyAsync(ct);
    }

    public async Task<T?> FirstOrDefaultAsync(ISpecification<T> spec, CancellationToken ct = default) =>
        await SpecificationEvaluator.Apply(Set.AsQueryable(), spec).FirstOrDefaultAsync(ct);

    public async Task AddAsync(T entity, CancellationToken ct = default) => await Set.AddAsync(entity, ct);

    public void Update(T entity) => Set.Update(entity);

    public void Remove(T entity) => Set.Remove(entity);
}
