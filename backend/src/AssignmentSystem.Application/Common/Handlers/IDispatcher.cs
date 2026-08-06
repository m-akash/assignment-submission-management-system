using AssignmentSystem.Shared.Common;

namespace AssignmentSystem.Application.Common.Handlers;

/// <summary>
/// Resolves and invokes the handler for a message.
///
/// Controllers depend on this one service instead of injecting a handler per endpoint —
/// <c>AssignmentsController</c> alone used to take nine. More importantly it is the seam
/// the pipeline hangs from: authorization and validation decorators wrap every handler
/// resolved through here, so there is no route into a handler that bypasses them.
/// </summary>
public interface IDispatcher
{
    Task<Result<TResult>> SendAsync<TResult>(ICommand<TResult> command, CancellationToken ct = default);

    Task<Result> SendAsync(ICommand command, CancellationToken ct = default);

    Task<Result<TResult>> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken ct = default);
}
