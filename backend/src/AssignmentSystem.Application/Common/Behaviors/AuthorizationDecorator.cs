using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Application.Common.Authorization;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Shared.Common;
using Microsoft.Extensions.Logging;

namespace AssignmentSystem.Application.Common.Behaviors;

// The outermost decorator in the pipeline: authorization runs before validation, so an
// unauthorized caller learns nothing about the shape of a request they may not make.
//
// This exists because role checks used to be hand-written in every handler — the same
// `if (_currentUser.Role != Role.Teacher)` copied across create, update, delete, publish,
// grade and download. One handler forgetting it was an open door with nothing to catch it.
// The check now happens once, for every message, and the requirement is declared on the
// message type (see RequiresRoleAttribute).
//
// Resource-level rules — "is this *your* assignment", "are you enrolled in that class" —
// deliberately stay out of here: they need the entity loaded, which is the handler's job.
// Those live in the IResourceAuthorizer implementations instead.

internal sealed class AuthorizationDecorator<TCommand, TResult> : ICommandHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    private readonly ICommandHandler<TCommand, TResult> _inner;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<AuthorizationDecorator<TCommand, TResult>> _logger;

    public AuthorizationDecorator(
        ICommandHandler<TCommand, TResult> inner,
        ICurrentUser currentUser,
        ILogger<AuthorizationDecorator<TCommand, TResult>> logger)
    {
        _inner = inner;
        _currentUser = currentUser;
        _logger = logger;
    }

    public Task<Result<TResult>> HandleAsync(TCommand command, CancellationToken ct = default)
    {
        if (AuthorizationPolicy.Check(typeof(TCommand), _currentUser) is { } error)
        {
            AuthorizationLog.Denied(_logger, typeof(TCommand), _currentUser, error);
            return Task.FromResult(Result<TResult>.Failure(error));
        }

        return _inner.HandleAsync(command, ct);
    }
}

internal sealed class AuthorizationDecorator<TCommand> : ICommandHandler<TCommand>
    where TCommand : ICommand
{
    private readonly ICommandHandler<TCommand> _inner;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<AuthorizationDecorator<TCommand>> _logger;

    public AuthorizationDecorator(
        ICommandHandler<TCommand> inner,
        ICurrentUser currentUser,
        ILogger<AuthorizationDecorator<TCommand>> logger)
    {
        _inner = inner;
        _currentUser = currentUser;
        _logger = logger;
    }

    public Task<Result> HandleAsync(TCommand command, CancellationToken ct = default)
    {
        if (AuthorizationPolicy.Check(typeof(TCommand), _currentUser) is { } error)
        {
            AuthorizationLog.Denied(_logger, typeof(TCommand), _currentUser, error);
            return Task.FromResult(Result.Failure(error));
        }

        return _inner.HandleAsync(command, ct);
    }
}

internal sealed class QueryAuthorizationDecorator<TQuery, TResult> : IQueryHandler<TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    private readonly IQueryHandler<TQuery, TResult> _inner;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<QueryAuthorizationDecorator<TQuery, TResult>> _logger;

    public QueryAuthorizationDecorator(
        IQueryHandler<TQuery, TResult> inner,
        ICurrentUser currentUser,
        ILogger<QueryAuthorizationDecorator<TQuery, TResult>> logger)
    {
        _inner = inner;
        _currentUser = currentUser;
        _logger = logger;
    }

    public Task<Result<TResult>> HandleAsync(TQuery query, CancellationToken ct = default)
    {
        if (AuthorizationPolicy.Check(typeof(TQuery), _currentUser) is { } error)
        {
            AuthorizationLog.Denied(_logger, typeof(TQuery), _currentUser, error);
            return Task.FromResult(Result<TResult>.Failure(error));
        }

        return _inner.HandleAsync(query, ct);
    }
}

/// <summary>
/// One place to shape the denial log line, so every refusal is greppable the same way
/// regardless of which decorator produced it.
/// </summary>
internal static class AuthorizationLog
{
    public static void Denied(ILogger logger, Type messageType, ICurrentUser currentUser, Error error)
    {
        logger.LogWarning(
            "Authorization denied for {MessageType}: {ErrorCode}. Caller {UserId} has role {Role}.",
            messageType.Name,
            error.Code,
            currentUser.UserId,
            currentUser.Role);
    }
}
