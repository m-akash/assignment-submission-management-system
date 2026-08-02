using System.Linq.Expressions;
using AssignmentSystem.Domain.Common;

namespace AssignmentSystem.Application.Common.Interfaces;

/// <summary>
/// Specification: a declarative query description (criteria, includes, ordering,
/// paging). Built in the Application layer; evaluated by the Infrastructure
/// repository against <see cref="IQueryable{T}"/>. Keeps EF out of Application.
/// </summary>
public interface ISpecification<T> where T : class
{
    Expression<Func<T, bool>>? Criteria { get; }
    List<Expression<Func<T, object>>> Includes { get; }
    List<string> IncludeStrings { get; }
    Expression<Func<T, object>>? OrderBy { get; }
    Expression<Func<T, object>>? OrderByDescending { get; }
    int? Page { get; }
    int? PageSize { get; }
    bool AsNoTracking { get; }
}
