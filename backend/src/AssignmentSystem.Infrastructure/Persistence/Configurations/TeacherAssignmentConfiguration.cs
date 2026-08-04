using AssignmentSystem.Domain.TeacherAssignments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssignmentSystem.Infrastructure.Persistence.Configurations;

internal sealed class TeacherAssignmentConfiguration : IEntityTypeConfiguration<TeacherAssignment>
{
    public void Configure(EntityTypeBuilder<TeacherAssignment> builder)
    {
        builder.ToTable("teacher_assignments");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(t => t.TeacherId).IsRequired();
        builder.Property(t => t.ClassCourseId).IsRequired();

        builder.Property(t => t.CreatedAtUtc).IsRequired();
        builder.Property(t => t.UpdatedAtUtc).IsRequired();
        builder.Property(t => t.CreatedBy);
        builder.Property(t => t.UpdatedBy);

        builder.Property(t => t.RowVersion).IsRowVersion();

        builder.HasOne(t => t.Teacher)
            .WithMany()
            .HasForeignKey(t => t.TeacherId)
            .OnDelete(DeleteBehavior.Cascade);

        // Cascade: a mapping is a pure link. Dropping an offering that still has mappings is
        // refused a level up (DeleteClassCourseHandler), so this only fires when the admin
        // has already unwound them or when a class is being removed outright.
        builder.HasOne(t => t.ClassCourse)
            .WithMany()
            .HasForeignKey(t => t.ClassCourseId)
            .OnDelete(DeleteBehavior.Cascade);

        // Authorization backbone uniqueness: a teacher can be linked to an offering once.
        builder.HasIndex(t => new { t.TeacherId, t.ClassCourseId }).IsUnique();

        // "Who teaches this offering?" — read when validating an assignment's author.
        builder.HasIndex(t => t.ClassCourseId);
    }
}
