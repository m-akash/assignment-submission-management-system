using AssignmentSystem.Shared.Common;

namespace AssignmentSystem.Application.Common.Handlers;

/// <summary>
/// Query handler (CQRS read side). Side-effect free. Returns <see cref="Result{TResult}"/>
/// so expected failures (not-found, forbidden) flow without exceptions.
/// </summary>
public interface IQueryHandler<in TQuery, TResult> where TQuery : IQuery<TResult>
{
    Task<Result<TResult>> HandleAsync(TQuery query, CancellationToken ct = default);
}

public interface IQuery<out TResult> { }
