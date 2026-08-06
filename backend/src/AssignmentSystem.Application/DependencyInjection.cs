using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Application.Common.Authorization;
using AssignmentSystem.Application.Common.Behaviors;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Application.Features.Notifications;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace AssignmentSystem.Application;

/// <summary>
/// Application layer DI: validators, command/query handlers, the cross-cutting decorator
/// pipeline, and the dispatcher that fronts it. Application stays free of Infrastructure
/// concerns.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        // Fail fast: refuse to build the container if any command or query has not declared
        // who is allowed to send it. A forgotten annotation stops the app at startup, where
        // it is unmissable, rather than becoming an unguarded endpoint in production.
        AuthorizationPolicy.ValidateAllMessagesAreAnnotated(assembly);

        // FluentValidation — every AbstractValidator<> in this assembly. These validate
        // commands and queries; the decorators below are what actually run them.
        services.AddValidatorsFromAssembly(assembly);

        // CQRS handlers. The decorators in Common/Behaviors implement the same interfaces,
        // so they are excluded here — they are wrapped around these registrations further
        // down, not registered as handlers in their own right.
        var behaviorsNamespace = typeof(AuthorizationDecorator<,>).Namespace!;
        bool IsNotABehavior(Type type) => type.Namespace != behaviorsNamespace;

        services.Scan(scan => scan
            .FromAssemblies(assembly)
            .AddClasses(classes => classes.AssignableTo(typeof(ICommandHandler<,>)).Where(IsNotABehavior))
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        services.Scan(scan => scan
            .FromAssemblies(assembly)
            .AddClasses(classes => classes.AssignableTo(typeof(ICommandHandler<>)).Where(IsNotABehavior))
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        services.Scan(scan => scan
            .FromAssemblies(assembly)
            .AddClasses(classes => classes.AssignableTo(typeof(IQueryHandler<,>)).Where(IsNotABehavior))
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        // ── Cross-cutting pipeline ────────────────────────────────────────────────
        // Decoration is applied innermost-first, so the registration order here is the
        // reverse of the execution order. Authorization is registered last and therefore
        // runs first: a caller who may not make the request never reaches validation, and
        // so learns nothing about the shape of a request that is not theirs to make.
        //
        //   Authorization → Validation → Handler
        services.Decorate(typeof(ICommandHandler<,>), typeof(ValidationDecorator<,>));
        services.Decorate(typeof(ICommandHandler<>), typeof(ValidationDecorator<>));
        services.Decorate(typeof(IQueryHandler<,>), typeof(QueryValidationDecorator<,>));

        services.Decorate(typeof(ICommandHandler<,>), typeof(AuthorizationDecorator<,>));
        services.Decorate(typeof(ICommandHandler<>), typeof(AuthorizationDecorator<>));
        services.Decorate(typeof(IQueryHandler<,>), typeof(QueryAuthorizationDecorator<,>));

        // The single entry point controllers use. Resolving through it is what guarantees a
        // handler is always reached through the decorators above.
        services.AddScoped<IDispatcher, Dispatcher>();

        // Resource-level authorization — the half the decorators cannot do, because it needs
        // the row loaded. Handlers call these instead of re-deriving "is this mine?" inline.
        services.AddScoped<IAssignmentAccess, AssignmentAccess>();
        services.AddScoped<ISubmissionAccess, SubmissionAccess>();

        // Registered explicitly: the scans above only pick up command/query handlers, and
        // the notification outbox writer is neither — it is a collaborator several handlers
        // share, enlisted in their transaction.
        services.AddScoped<INotificationOutbox, NotificationOutbox>();

        return services;
    }
}
