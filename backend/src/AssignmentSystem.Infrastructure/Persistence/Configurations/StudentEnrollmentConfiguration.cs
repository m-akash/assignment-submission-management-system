using AssignmentSystem.Domain.Enrollments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssignmentSystem.Infrastructure.Persistence.Configurations;

internal sealed class StudentEnrollmentConfiguration : IEntityTypeConfiguration<StudentEnrollment>
{
    public void Configure(EntityTypeBuilder<StudentEnrollment> builder)
    {
        builder.ToTable("student_enrollments");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.StudentId).IsRequired();
        builder.Property(e => e.ClassId).IsRequired();
        builder.Property(e => e.EnrolledAtUtc).IsRequired();

        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.UpdatedAtUtc).IsRequired();
        builder.Property(e => e.CreatedBy);
        builder.Property(e => e.UpdatedBy);

        builder.Property(e => e.RowVersion).IsRowVersion();

        // Cascade on both sides: an enrollment is a pure link, meaningless once either end
        // is gone. Note the student side is a soft delete in practice — ApplicationUser rows
        // are flagged rather than removed, so enrollments survive alongside them and the
        // roster queries filter on the student's IsDeleted instead.
        builder.HasOne(e => e.Student)
            .WithMany(u => u.Enrollments)
            .HasForeignKey(e => e.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Class)
            .WithMany(c => c.Enrollments)
            .HasForeignKey(e => e.ClassId)
            .OnDelete(DeleteBehavior.Cascade);

        // A student sits in a class once.
        builder.HasIndex(e => new { e.StudentId, e.ClassId }).IsUnique();

        // The hot path: "which classes is this student in?" on every student request (rule B1).
        builder.HasIndex(e => e.StudentId);

        // The roster path: counting a class, and addressing an assignment-published email.
        builder.HasIndex(e => e.ClassId);
    }
}
