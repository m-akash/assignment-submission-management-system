using AssignmentSystem.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AssignmentSystem.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef migrations add</c> build the model without a running DI container
/// or live database. The connection string is only needed to scaffold; no connection
/// is opened at design time for Npgsql.
/// </summary>
public sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5432;Database=assignment_system;Username=assignments;Password=assignments",
                npgsql => npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .Options;

        // The audit interceptor is not needed at design time (no saving happens).
        return new AppDbContext(options, new AuditSaveChangesInterceptor(
            new UnauthenticatedCurrentUser(),
            new SystemClockWrapper()));
    }

    // Design-time stubs — never used to save, only to satisfy the constructor.
    private sealed class UnauthenticatedCurrentUser : AssignmentSystem.Application.Abstractions.ICurrentUser
    {
        public Guid? UserId => null;
        public string? Email => null;
        public string? FullName => null;
        public AssignmentSystem.Domain.Enums.Role? Role => null;
        public Guid? ClassId => null;
        public Guid? GroupId => null;
        public bool IsAuthenticated => false;
        public bool IsInRole(AssignmentSystem.Domain.Enums.Role role) => false;
    }

    private sealed class SystemClockWrapper : AssignmentSystem.Domain.Common.IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }
}
