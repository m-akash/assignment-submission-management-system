using System.Collections.Concurrent;
using System.Linq.Expressions;
using AssignmentSystem.Shared.Common;
using Microsoft.Extensions.DependencyInjection;

namespace AssignmentSystem.Application.Common.Handlers;

/// <summary>
/// Reflection-based dispatch, compiled once per handler type.
///
/// The static type of a message at the call site is <c>ICommand&lt;TResult&gt;</c>, but the
/// handler is registered against the concrete command type — so the closed handler type can
/// only be built at runtime. The reflection cost is paid once: the first call for a given
/// message type compiles an expression tree that invokes <c>HandleAsync</c> directly, and
/// every later call goes through that delegate.
/// </summary>
internal sealed class Dispatcher : IDispatcher
{
    private static readonly ConcurrentDictionary<Type, Func<object, object, CancellationToken, object>> Invokers = new();

    private readonly IServiceProvider _provider;

    public Dispatcher(IServiceProvider provider) => _provider = provider;

    public Task<Result<TResult>> SendAsync<TResult>(ICommand<TResult> command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var handlerType = typeof(ICommandHandler<,>).MakeGenericType(command.GetType(), typeof(TResult));
        return (Task<Result<TResult>>)Invoke(handlerType, command, ct);
    }

    public Task<Result> SendAsync(ICommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var handlerType = typeof(ICommandHandler<>).MakeGenericType(command.GetType());
        return (Task<Result>)Invoke(handlerType, command, ct);
    }

    public Task<Result<TResult>> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var handlerType = typeof(IQueryHandler<,>).MakeGenericType(query.GetType(), typeof(TResult));
        return (Task<Result<TResult>>)Invoke(handlerType, query, ct);
    }

    private object Invoke(Type handlerType, object message, CancellationToken ct)
    {
        // GetRequiredService, not GetService: a missing handler is a wiring bug that should
        // surface immediately and loudly, not as a null reference three frames later.
        var handler = _provider.GetRequiredService(handlerType);
        var invoker = Invokers.GetOrAdd(handlerType, BuildInvoker);
        return invoker(handler, message, ct);
    }

    private static Func<object, object, CancellationToken, object> BuildInvoker(Type handlerType)
    {
        var method = handlerType.GetMethod(nameof(ICommandHandler<ICommand>.HandleAsync))
            ?? throw new InvalidOperationException($"{handlerType.Name} has no HandleAsync method.");

        var handlerParameter = Expression.Parameter(typeof(object), "handler");
        var messageParameter = Expression.Parameter(typeof(object), "message");
        var tokenParameter = Expression.Parameter(typeof(CancellationToken), "ct");

        var call = Expression.Call(
            Expression.Convert(handlerParameter, handlerType),
            method,
            Expression.Convert(messageParameter, method.GetParameters()[0].ParameterType),
            tokenParameter);

        return Expression
            .Lambda<Func<object, object, CancellationToken, object>>(
                Expression.Convert(call, typeof(object)),
                handlerParameter,
                messageParameter,
                tokenParameter)
            .Compile();
    }
}
