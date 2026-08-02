using AssignmentSystem.Domain.Assignments;
using AssignmentSystem.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssignmentSystem.Infrastructure.Persistence.Configurations;

internal sealed class AssignmentConfiguration : IEntityTypeConfiguration<Assignment>
{
    public void Configure(EntityTypeBuilder<Assignment> builder)
    {
        builder.ToTable("assignments");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(a => a.TeacherAssignmentId).IsRequired();
        builder.Property(a => a.TeacherId).IsRequired();
        builder.Property(a => a.SubjectId).IsRequired();
        builder.Property(a => a.ClassId).IsRequired();

        builder.Property(a => a.Title).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Description).IsRequired();
        builder.Property(a => a.DeadlineUtc).IsRequired();

        builder.Property(a => a.MaxMarks)
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(a => a.Status)
            .HasConversion<int>()
            .HasDefaultValue(AssignmentStatus.Draft)
            .IsRequired();

        builder.Property(a => a.AllowResubmission).IsRequired().HasDefaultValue(true);
        builder.Property(a => a.SubmissionCount).IsRequired().HasDefaultValue(0);

        builder.Property(a => a.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.Property(a => a.DeletedAtUtc);

        builder.Property(a => a.CreatedAtUtc).IsRequired();
        builder.Property(a => a.UpdatedAtUtc).IsRequired();
        builder.Property(a => a.CreatedBy);
        builder.Property(a => a.UpdatedBy);

        builder.Property(a => a.RowVersion).IsRowVersion();

        builder.HasOne(a => a.TeacherAssignment)
            .WithMany()
            .HasForeignKey(a => a.TeacherAssignmentId)
            .OnDelete(DeleteBehavior.Restrict); // don't lose the scope chain

        builder.HasOne(a => a.Subject)
            .WithMany()
            .HasForeignKey(a => a.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Class)
            .WithMany()
            .HasForeignKey(a => a.ClassId)
            .OnDelete(DeleteBehavior.Cascade);

        // Composite index for the common "assignments for my class/subject" query.
        builder.HasIndex(a => new { a.ClassId, a.SubjectId, a.Status });
        builder.HasIndex(a => a.TeacherId);

        builder.HasQueryFilter(a => !a.IsDeleted);
    }
}
