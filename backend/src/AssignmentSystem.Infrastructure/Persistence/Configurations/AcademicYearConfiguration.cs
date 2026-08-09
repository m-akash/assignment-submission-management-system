using AssignmentSystem.Domain.AcademicYears;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssignmentSystem.Infrastructure.Persistence.Configurations;

internal sealed class AcademicYearConfiguration : IEntityTypeConfiguration<AcademicYear>
{
    public void Configure(EntityTypeBuilder<AcademicYear> builder)
    {
        builder.ToTable("academic_years");

        builder.HasKey(y => y.Id);
        builder.Property(y => y.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(y => y.Name).HasMaxLength(50).IsRequired();
        builder.Property(y => y.StartDate).IsRequired();
        builder.Property(y => y.EndDate).IsRequired();
        builder.Property(y => y.IsCurrent).IsRequired();

        builder.Property(y => y.CreatedAtUtc).IsRequired();
        builder.Property(y => y.UpdatedAtUtc).IsRequired();
        builder.Property(y => y.CreatedBy);
        builder.Property(y => y.UpdatedBy);

        builder.Property(y => y.RowVersion).IsRowVersion();

        builder.HasIndex(y => y.Name).IsUnique();

        // At most one current session. The handlers clear the previous holder before
        // setting a new one; this partial unique index is what makes that a guarantee
        // rather than a convention — two concurrent "make this current" requests cannot
        // both land, which a read-then-write check alone could not prevent.
        builder.HasIndex(y => y.IsCurrent)
            .IsUnique()
            .HasFilter("is_current")
            .HasDatabaseName("ix_academic_years_is_current_unique");

        // The list's natural order: newest session first.
        builder.HasIndex(y => y.StartDate);
    }
}
