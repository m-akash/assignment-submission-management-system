using AssignmentSystem.Domain.Groups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssignmentSystem.Infrastructure.Persistence.Configurations;

internal sealed class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> builder)
    {
        builder.ToTable("groups");

        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(g => g.Name).HasMaxLength(150).IsRequired();
        builder.Property(g => g.Code).HasMaxLength(10).IsRequired();

        builder.Property(g => g.CreatedAtUtc).IsRequired();
        builder.Property(g => g.UpdatedAtUtc).IsRequired();
        builder.Property(g => g.CreatedBy);
        builder.Property(g => g.UpdatedBy);

        builder.Property(g => g.RowVersion).IsRowVersion();

        builder.HasIndex(g => g.Code).IsUnique();
    }
}
