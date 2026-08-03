using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.Common;
using AssignmentSystem.Infrastructure.Authentication;
using AssignmentSystem.Infrastructure.Common;
using AssignmentSystem.Infrastructure.Identity;
using AssignmentSystem.Infrastructure.Persistence;
using AssignmentSystem.Infrastructure.Persistence.Interceptors;
using AssignmentSystem.Infrastructure.Persistence.Repositories;
using AssignmentSystem.Infrastructure.Persistence.Seed;
using AssignmentSystem.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AssignmentSystem.Infrastructure;

/// <summary>
/// Infrastructure DI registration: persistence (EF Core + Postgres), repositories,
/// UnitOfWork, audit interceptor, clock, identity (password hashing), JWT auth,
/// and local file storage.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // ── Persistence ────────────────────────────────────────────────────────
        services.AddScoped<AuditSaveChangesInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString("Default"),
                npgsql => npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));
        });

        services.AddScoped<IUnitOfWork>(sp =>
            new UnitOfWork(
                sp.GetRequiredService<AppDbContext>(),
                sp.GetService<IDomainEventDispatcher>()));

        // Open generic repository registered as concrete generic via factory.
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IClassRosterRepository, ClassRosterRepository>();
        services.AddScoped<ITeacherRosterRepository, TeacherRosterRepository>();

        // ── Clock ──────────────────────────────────────────────────────────────
        services.AddSingleton<IClock, SystemClock>();

        // ── Identity / Auth ────────────────────────────────────────────────────
        services.AddScoped<IPasswordHasher, PasswordHasherAdapter>();
        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
        services.Configure<FileStorageOptions>(configuration.GetSection("FileStorage"));
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        // ── File storage (local disk / Docker volume) + upload rules ───────────
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddSingleton<IFileUploadPolicy, FileUploadPolicy>();

        // No-op domain event dispatcher until concrete handlers are added.
        services.AddScoped<IDomainEventDispatcher, NullDomainEventDispatcher>();

        // Database seeder (demo accounts + sample data).
        services.AddScoped<DbSeeder>();

        return services;
    }
}
