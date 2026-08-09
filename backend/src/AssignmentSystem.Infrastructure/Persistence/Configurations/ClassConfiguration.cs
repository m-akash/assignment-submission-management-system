using AssignmentSystem.Domain.Classes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssignmentSystem.Infrastructure.Persistence.Configurations;

internal sealed class ClassConfiguration : IEntityTypeConfiguration<Class>
{
    public void Configure(EntityTypeBuilder<Class> builder)
    {
        builder.ToTable("classes");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(c => c.Level).IsRequired();
        builder.Property(c => c.Section).HasMaxLength(50);

        // DisplayName is composed from Level and Section for email prose — nothing to persist.
        builder.Ignore(c => c.DisplayName);

        builder.Property(c => c.CreatedAtUtc).IsRequired();
        builder.Property(c => c.UpdatedAtUtc).IsRequired();
        builder.Property(c => c.CreatedBy);
        builder.Property(c => c.UpdatedBy);

        builder.Property(c => c.RowVersion).IsRowVersion();

        // One cohort per (grade, section): a grade may have any number of sections, but not
        // the same one twice. The handlers reject duplicates case-insensitively and with a
        // readable message; this is the backstop against a race between two admins.
        builder.HasIndex(c => new { c.Level, c.Section }).IsUnique();
    }
}
