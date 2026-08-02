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

        builder.Property(c => c.Name).HasMaxLength(150).IsRequired();
        builder.Property(c => c.Grade).HasMaxLength(50);
        builder.Property(c => c.Section).HasMaxLength(50);

        builder.Property(c => c.CreatedAtUtc).IsRequired();
        builder.Property(c => c.UpdatedAtUtc).IsRequired();
        builder.Property(c => c.CreatedBy);
        builder.Property(c => c.UpdatedBy);

        builder.Property(c => c.RowVersion).IsRowVersion();

        builder.HasIndex(c => c.Name);
    }
}
