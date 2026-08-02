using AssignmentSystem.Application.Common.Handlers;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace AssignmentSystem.Application;

/// <summary>
/// Application layer DI: registers FluentValidation validators and command/query
/// handlers from this assembly. Application stays free of Infrastructure concerns.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        // FluentValidation — register every AbstractValidator<> in this assembly.
        services.AddValidatorsFromAssembly(assembly);

        // CQRS handlers — wire all ICommandHandler<,>, ICommandHandler<> and IQueryHandler<,>
        // implementations to their interfaces.
        services.Scan(scan => scan
            .FromAssemblies(assembly)
            .AddClasses(classes => classes.AssignableTo(typeof(ICommandHandler<,>)))
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        services.Scan(scan => scan
            .FromAssemblies(assembly)
            .AddClasses(classes => classes.AssignableTo(typeof(ICommandHandler<>)))
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        services.Scan(scan => scan
            .FromAssemblies(assembly)
            .AddClasses(classes => classes.AssignableTo(typeof(IQueryHandler<,>)))
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        return services;
    }
}
