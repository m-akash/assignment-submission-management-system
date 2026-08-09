using AssignmentSystem.Domain.AcademicYears;
using AssignmentSystem.Domain.Assignments;
using AssignmentSystem.Domain.ClassCourses;
using AssignmentSystem.Domain.Classes;
using AssignmentSystem.Domain.Common;
using AssignmentSystem.Domain.Courses;
using AssignmentSystem.Domain.Enrollments;
using AssignmentSystem.Domain.Notifications;
using AssignmentSystem.Domain.Submissions;
using AssignmentSystem.Domain.TeacherAssignments;
using AssignmentSystem.Domain.Users;
using AssignmentSystem.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;

namespace AssignmentSystem.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext. Applies IEntityTypeConfiguration&lt;T&gt; from this assembly,
/// snake_case naming, and the audit/soft-delete interceptor. Concurrency token
/// (RowVersion) is mapped to Postgres xmin. Domain events are collected and
/// dispatched by the UnitOfWork after SaveChanges.
/// </summary>
public sealed class AppDbContext : DbContext
{
    private readonly AuditSaveChangesInterceptor _auditInterceptor;

    public AppDbContext(DbContextOptions<AppDbContext> options, AuditSaveChangesInterceptor auditInterceptor)
        : base(options)
    {
        _auditInterceptor = auditInterceptor;
    }

    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<AcademicYear> AcademicYears => Set<AcademicYear>();
    public DbSet<Class> Classes => Set<Class>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<ClassCourse> ClassCourses => Set<ClassCourse>();
    public DbSet<StudentEnrollment> StudentEnrollments => Set<StudentEnrollment>();
    public DbSet<TeacherAssignment> TeacherAssignments => Set<TeacherAssignment>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<SubmissionFile> SubmissionFiles => Set<SubmissionFile>();
    public DbSet<AssignmentFile> AssignmentFiles => Set<AssignmentFile>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordSetupToken> PasswordSetupTokens => Set<PasswordSetupToken>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        SnakeCaseNamingPolicy.Apply(modelBuilder);

        // Optimistic concurrency: map every BaseEntity.RowVersion (uint) to Postgres's
        // read-only xmin system column. Postgres sets xmin automatically on insert/update;
        // Npgsql excludes a rowversion-typed property mapped to xmin from INSERT/UPDATE.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var rowVersion = entityType.FindProperty(nameof(BaseEntity.RowVersion));
            if (rowVersion is not null)
            {
                rowVersion.SetColumnName("xmin");
            }
        }

        base.OnModelCreating(modelBuilder);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(_auditInterceptor);
    }
}
