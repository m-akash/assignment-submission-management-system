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

        builder.Property(a => a.ClassCourseId).IsRequired();
        builder.Property(a => a.TeacherId).IsRequired();

        builder.Property(a => a.Title).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Description).IsRequired();

        // A stored generated column rather than a value the write path maintains. The
        // description is HTML, and searching it directly matches tag names — "li" would find
        // every assignment written as a list. Stripping the tags in the database instead of in
        // a handler means the two columns cannot drift, and means rows written before the
        // editor existed are stripped on the next read rather than needing a backfill.
        //
        // Tags become a space so adjacent list items do not run together into one word, and
        // the four entities the sanitizer can emit are decoded so searching for "&" or "<"
        // finds what the author actually typed. Every function here is IMMUTABLE, which
        // PostgreSQL requires of a generated column.
        builder.Property(a => a.DescriptionText)
            .HasComputedColumnSql(
                "replace(replace(replace(replace(regexp_replace(description, '<[^>]*>', ' ', 'g'), " +
                "'&nbsp;', ' '), '&lt;', '<'), '&gt;', '>'), '&amp;', '&')",
                stored: true);

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

        // Restrict, not Cascade: an offering cannot be dropped while assignments (and so
        // student submissions) still hang off it. DeleteClassCourseHandler turns that into a
        // 409 with an explanation rather than letting it surface as a constraint violation.
        builder.HasOne(a => a.ClassCourse)
            .WithMany()
            .HasForeignKey(a => a.ClassCourseId)
            .OnDelete(DeleteBehavior.Restrict);

        // Restrict on the author too — a teacher account is soft-deleted, never removed, so
        // this only guards against a hard delete taking the authorship of live work with it.
        builder.HasOne(a => a.Teacher)
            .WithMany()
            .HasForeignKey(a => a.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);

        // Covers the two ways the list is read: by offering (a student's or admin's class
        // view) and by author (a teacher's own work).
        builder.HasIndex(a => new { a.ClassCourseId, a.Status });
        builder.HasIndex(a => a.TeacherId);

        builder.HasMany(a => a.Files)
            .WithOne(f => f.Assignment)
            .HasForeignKey(f => f.AssignmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(a => !a.IsDeleted);
    }
}
