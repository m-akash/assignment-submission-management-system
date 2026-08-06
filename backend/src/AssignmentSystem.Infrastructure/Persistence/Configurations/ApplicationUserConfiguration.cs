using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssignmentSystem.Infrastructure.Persistence.Configurations;

/// <summary>
/// Fluent API config for <see cref="ApplicationUser"/>. No data annotations in the
/// domain. UUID PK, unique email (owned value object), soft-delete query filter.
/// </summary>
internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(u => u.FullName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(u => u.PasswordHash)
            .IsRequired();

        builder.Property(u => u.Role)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(u => u.IsActive).IsRequired().HasDefaultValue(true);

        // Login throttling. Both default so the migration can add them to existing rows.
        builder.Property(u => u.FailedLoginAttempts).IsRequired().HasDefaultValue(0);
        builder.Property(u => u.LockoutEndUtc);

        builder.Property(u => u.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.Property(u => u.DeletedAtUtc);

        builder.Property(u => u.CreatedAtUtc).IsRequired();
        builder.Property(u => u.UpdatedAtUtc).IsRequired();
        builder.Property(u => u.CreatedBy);
        builder.Property(u => u.UpdatedBy);

        // Email value object persisted as a complex property (single column: email).
        // Complex properties have NO identity, NO FK shadow columns, and NO separate
        // entity — exactly what a single-string value object needs. The unique
        // constraint on the email column is created via raw SQL in the migration
        // (EF Core 10 can't express a unique index over a complex nested column in the model).
        builder.ComplexProperty(u => u.Email, email =>
        {
            email.Property(e => e.Value)
                .HasColumnName("email")
                .HasMaxLength(256)
                .IsRequired();
        });

        // Class membership lives in student_enrollments (configured on the other side of
        // the relationship, in StudentEnrollmentConfiguration) — there is no class column here.

        // "IX-A-003" (grade numeral, section, sequence). Null for admin/teacher — a unique
        // index over a nullable column still allows any number of nulls in Postgres.
        builder.Property(u => u.StudentId)
            .HasMaxLength(30);

        builder.HasIndex(u => u.StudentId).IsUnique();

        // "INS-01" (instructor - sequence). Null for admin/student.
        builder.Property(u => u.TeacherId)
            .HasMaxLength(30);

        builder.HasIndex(u => u.TeacherId).IsUnique();

        // Refresh tokens relationship
        builder.HasMany(u => u.RefreshTokens)
            .WithOne(t => t.User)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Optimistic concurrency token (Postgres xmin → uint).
        builder.Property(u => u.RowVersion)
            .IsRowVersion();

        // Soft-delete global filter: hide deleted users by default.
        builder.HasQueryFilter(u => !u.IsDeleted);
    }
}
