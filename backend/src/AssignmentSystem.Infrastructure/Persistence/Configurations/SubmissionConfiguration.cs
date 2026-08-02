using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Domain.Submissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssignmentSystem.Infrastructure.Persistence.Configurations;

internal sealed class SubmissionConfiguration : IEntityTypeConfiguration<Submission>
{
    public void Configure(EntityTypeBuilder<Submission> builder)
    {
        builder.ToTable("submissions");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(s => s.AssignmentId).IsRequired();
        builder.Property(s => s.StudentId).IsRequired();

        builder.Property(s => s.Content); // nullable: file-only submissions allowed

        builder.Property(s => s.Status)
            .HasConversion<int>()
            .HasDefaultValue(SubmissionStatus.Pending)
            .IsRequired();

        builder.Property(s => s.SubmittedAtUtc);

        builder.Property(s => s.Marks).HasPrecision(5, 2);
        builder.Property(s => s.MarksOutOf).HasPrecision(5, 2);
        builder.Property(s => s.Feedback);
        builder.Property(s => s.ReviewedById);
        builder.Property(s => s.ReviewedAtUtc);

        builder.Property(s => s.CreatedAtUtc).IsRequired();
        builder.Property(s => s.UpdatedAtUtc).IsRequired();
        builder.Property(s => s.CreatedBy);
        builder.Property(s => s.UpdatedBy);

        builder.Property(s => s.RowVersion).IsRowVersion();

        builder.HasOne(s => s.Assignment)
            .WithMany(a => a.Submissions)
            .HasForeignKey(s => s.AssignmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Student)
            .WithMany()
            .HasForeignKey(s => s.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.ReviewedBy)
            .WithMany()
            .HasForeignKey(s => s.ReviewedById)
            .OnDelete(DeleteBehavior.SetNull);

        // Rule X4: one submission per student per assignment.
        builder.HasIndex(s => new { s.AssignmentId, s.StudentId }).IsUnique();
        builder.HasIndex(s => s.AssignmentId);
        builder.HasIndex(s => s.StudentId);

        builder.HasMany(s => s.Files)
            .WithOne(f => f.Submission)
            .HasForeignKey(f => f.SubmissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
