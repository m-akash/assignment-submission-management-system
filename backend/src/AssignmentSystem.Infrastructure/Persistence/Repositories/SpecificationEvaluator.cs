using AssignmentSystem.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AssignmentSystem.Infrastructure.Persistence.Repositories;

/// <summary>
/// Translates an <see cref="ISpecification{T}"/> into an <see cref="IQueryable{T}"/>
/// (criteria, includes, ordering, paging, no-tracking). The single place EF expressions
/// are assembled — keeping the Application layer EF-free.
/// </summary>
public static class SpecificationEvaluator
{
    public static IQueryable<T> Apply<T>(IQueryable<T> source, ISpecification<T> spec) where T : class
    {
        var query = source;

        if (spec.AsNoTracking)
        {
            query = query.AsNoTracking();
        }

        if (spec.Criteria is not null)
        {
            query = query.Where(spec.Criteria);
        }

        foreach (var include in spec.Includes)
        {
            query = query.Include(include);
        }

        foreach (var includeString in spec.IncludeStrings)
        {
            query = query.Include(includeString);
        }

        IOrderedQueryable<T>? ordered = null;
        if (spec.OrderByDescending is not null)
        {
            ordered = query.OrderByDescending(spec.OrderByDescending);
        }
        else if (spec.OrderBy is not null)
        {
            ordered = query.OrderBy(spec.OrderBy);
        }

        if (ordered is not null)
        {
            // A secondary sort only means anything once there is a primary one.
            query = spec.ThenBy is not null ? ordered.ThenBy(spec.ThenBy) : ordered;
        }

        if (spec.Page is { } page && spec.PageSize is { } pageSize && page > 0 && pageSize > 0)
        {
            query = query.Skip((page - 1) * pageSize).Take(pageSize);
        }

        return query;
    }
}
