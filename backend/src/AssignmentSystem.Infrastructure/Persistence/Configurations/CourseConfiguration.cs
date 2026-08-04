using AssignmentSystem.Domain.Courses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssignmentSystem.Infrastructure.Persistence.Configurations;

internal sealed class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> builder)
    {
        builder.ToTable("courses");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(s => s.Name).HasMaxLength(150).IsRequired();
        builder.Property(s => s.Code).HasMaxLength(30).IsRequired();

        builder.Property(s => s.CreatedAtUtc).IsRequired();
        builder.Property(s => s.UpdatedAtUtc).IsRequired();
        builder.Property(s => s.CreatedBy);
        builder.Property(s => s.UpdatedBy);

        builder.Property(s => s.RowVersion).IsRowVersion();

        builder.HasIndex(s => s.Code).IsUnique();

        // Restrict: a department that still owns courses cannot be deleted — the admin
        // must move or remove them first, rather than silently orphaning the structure.
        builder.Property(s => s.DepartmentId).IsRequired();
        builder.HasOne(s => s.Department)
            .WithMany()
            .HasForeignKey(s => s.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.DepartmentId);

        // Restrict, not SetNull: silently opening a Science-only course to everyone the
        // moment the Science group is deleted would be a correctness change no one asked
        // for. The admin has to move the course to a different group (or clear it) first.
        builder.Property(s => s.GroupId);
        builder.HasOne(s => s.Group)
            .WithMany()
            .HasForeignKey(s => s.GroupId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.GroupId);
    }
}
