using AssignmentSystem.Domain.Subjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssignmentSystem.Infrastructure.Persistence.Configurations;

internal sealed class SubjectConfiguration : IEntityTypeConfiguration<Subject>
{
    public void Configure(EntityTypeBuilder<Subject> builder)
    {
        builder.ToTable("subjects");

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
    }
}
