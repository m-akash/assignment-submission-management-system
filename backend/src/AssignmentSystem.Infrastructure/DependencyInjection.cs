using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.Common;
using AssignmentSystem.Infrastructure.Authentication;
using AssignmentSystem.Infrastructure.Common;
using AssignmentSystem.Infrastructure.Identity;
using AssignmentSystem.Infrastructure.Notifications;
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

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Open generic repository registered as concrete generic via factory.
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IClassRosterRepository, ClassRosterRepository>();
        services.AddScoped<ITeacherRosterRepository, TeacherRosterRepository>();
        services.AddScoped<IClassCourseUsageReader, ClassCourseUsageReader>();

        // ── Clock ──────────────────────────────────────────────────────────────
        services.AddSingleton<IClock, SystemClock>();

        // ── Identity / Auth ────────────────────────────────────────────────────
        services.AddScoped<IPasswordHasher, PasswordHasherAdapter>();
        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.SectionName));
        services.Configure<FileStorageOptions>(configuration.GetSection("FileStorage"));
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IPasswordSetupTokenService, PasswordSetupTokenService>();
        services.AddSingleton<ILoginThrottleSettings, LoginThrottleSettings>();

        // ── File storage (local disk / Docker volume) + upload rules ───────────
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddSingleton<IFileUploadPolicy, FileUploadPolicy>();

        // ── Email notifications (outbox) ───────────────────────────────────────
        // The sender is a singleton (it holds only configuration and builds a fresh
        // SmtpClient per message); the dispatcher is scoped because it works through the
        // DbContext, so the hosted service opens a scope per sweep.
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.AddSingleton<INotificationSettings, NotificationSettings>();
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddScoped<INotificationDispatcher, NotificationDispatcher>();

        // Database seeder (demo accounts + sample data).
        services.AddScoped<DbSeeder>();

        return services;
    }
}
