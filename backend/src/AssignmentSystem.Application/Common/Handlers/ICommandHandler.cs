using AssignmentSystem.Shared.Common;

namespace AssignmentSystem.Application.Common.Handlers;

/// <summary>
/// Command handler (CQRS write side). Performs authorization, enforces domain rules
/// through entity methods, persists via repositories + UnitOfWork, and returns a
/// <see cref="Result{TResult}"/>. Never throws for expected domain failures.
/// </summary>
public interface ICommandHandler<in TCommand, TResult> where TCommand : ICommand<TResult>
{
    Task<Result<TResult>> HandleAsync(TCommand command, CancellationToken ct = default);
}

/// <summary>Command handler for commands that return no value (just success/failure).</summary>
public interface ICommandHandler<in TCommand> where TCommand : ICommand
{
    Task<Result> HandleAsync(TCommand command, CancellationToken ct = default);
}

public interface ICommand<out TResult> : ICommand { }

/// <summary>Marker for commands that produce no result value.</summary>
public interface ICommand { }
