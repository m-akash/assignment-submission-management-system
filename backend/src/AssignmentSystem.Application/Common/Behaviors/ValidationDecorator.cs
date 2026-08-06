using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Shared.Common;
using FluentValidation;
using FluentValidation.Results;

namespace AssignmentSystem.Application.Common.Behaviors;

// Runs every registered IValidator<TMessage> before the handler.
//
// The validators in Features/*/Validators.cs used to be dead code: the only thing running
// them was an MVC action filter, which validated the Api layer's request records — a
// different set of types with a duplicate set of rules. Commands are now the single
// validated shape, and these validators are the single place the rules live.
//
// Request-shape only. Business rules stay in the entities and handlers, which is why a
// failure here maps to the same 422 a DomainException does — the caller cannot tell, and
// should not need to, which side of that line rejected them.

internal sealed class ValidationDecorator<TCommand, TResult> : ICommandHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    private readonly ICommandHandler<TCommand, TResult> _inner;
    private readonly IEnumerable<IValidator<TCommand>> _validators;

    public ValidationDecorator(ICommandHandler<TCommand, TResult> inner, IEnumerable<IValidator<TCommand>> validators)
    {
        _inner = inner;
        _validators = validators;
    }

    public async Task<Result<TResult>> HandleAsync(TCommand command, CancellationToken ct = default)
    {
        var error = await ValidationRunner.RunAsync(_validators, command, ct);
        return error is not null
            ? Result<TResult>.Failure(error)
            : await _inner.HandleAsync(command, ct);
    }
}

internal sealed class ValidationDecorator<TCommand> : ICommandHandler<TCommand>
    where TCommand : ICommand
{
    private readonly ICommandHandler<TCommand> _inner;
    private readonly IEnumerable<IValidator<TCommand>> _validators;

    public ValidationDecorator(ICommandHandler<TCommand> inner, IEnumerable<IValidator<TCommand>> validators)
    {
        _inner = inner;
        _validators = validators;
    }

    public async Task<Result> HandleAsync(TCommand command, CancellationToken ct = default)
    {
        var error = await ValidationRunner.RunAsync(_validators, command, ct);
        return error is not null
            ? Result.Failure(error)
            : await _inner.HandleAsync(command, ct);
    }
}

internal sealed class QueryValidationDecorator<TQuery, TResult> : IQueryHandler<TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    private readonly IQueryHandler<TQuery, TResult> _inner;
    private readonly IEnumerable<IValidator<TQuery>> _validators;

    public QueryValidationDecorator(IQueryHandler<TQuery, TResult> inner, IEnumerable<IValidator<TQuery>> validators)
    {
        _inner = inner;
        _validators = validators;
    }

    public async Task<Result<TResult>> HandleAsync(TQuery query, CancellationToken ct = default)
    {
        var error = await ValidationRunner.RunAsync(_validators, query, ct);
        return error is not null
            ? Result<TResult>.Failure(error)
            : await _inner.HandleAsync(query, ct);
    }
}

internal static class ValidationRunner
{
    /// <summary>
    /// Returns the aggregated validation error, or <c>null</c> when the message is valid.
    /// All validators run — the caller gets every problem at once rather than discovering
    /// them one round-trip at a time.
    /// </summary>
    public static async Task<Error?> RunAsync<TMessage>(
        IEnumerable<IValidator<TMessage>> validators,
        TMessage message,
        CancellationToken ct)
    {
        // The common case is one validator, and materialising an empty enumerable to
        // discover there are none is not worth a context switch.
        var applicable = validators as IReadOnlyCollection<IValidator<TMessage>> ?? [.. validators];
        if (applicable.Count == 0)
        {
            return null;
        }

        var context = new ValidationContext<TMessage>(message);
        List<ValidationFailure>? failures = null;

        foreach (var validator in applicable)
        {
            var result = await validator.ValidateAsync(context, ct);
            if (!result.IsValid)
            {
                (failures ??= []).AddRange(result.Errors);
            }
        }

        if (failures is null)
        {
            return null;
        }

        var byProperty = failures
            .GroupBy(f => f.PropertyName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).ToArray(), StringComparer.OrdinalIgnoreCase);

        return Error.Validation(
            "Validation.Failed",
            "One or more validation errors occurred.",
            byProperty);
    }
}
