using AssignmentSystem.Domain.Classes;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Domain.Departments;
using AssignmentSystem.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssignmentSystem.Infrastructure.Persistence.Configurations;

/// <summary>
/// Fluent API config for <see cref="ApplicationUser"/>. No data annotations in the
/// domain. UUID PK, unique email (owned value object), class FK (set null on delete),
/// soft-delete query filter.
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

        builder.Property(u => u.ClassId);
        builder.HasOne(u => u.Class)
            .WithMany(c => c.Students)
            .HasForeignKey(u => u.ClassId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(u => u.ClassId);

        // "G10-A-003" (G, grade, section, sequence). Null for admin/teacher — a unique
        // index over a nullable column still allows any number of nulls in Postgres.
        builder.Property(u => u.StudentId)
            .HasMaxLength(30);

        builder.HasIndex(u => u.StudentId).IsUnique();

        // A teacher's department. SetNull rather than Restrict: removing a department
        // should not block deleting it outright, it just leaves the teacher unassigned.
        builder.Property(u => u.DepartmentId);
        builder.HasOne(u => u.Department)
            .WithMany()
            .HasForeignKey(u => u.DepartmentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(u => u.DepartmentId);

        // "INS-SCI-01" (instructor - department code - sequence). Null for admin/student.
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
