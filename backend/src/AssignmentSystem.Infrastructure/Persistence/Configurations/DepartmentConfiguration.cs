using AssignmentSystem.Domain.Departments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssignmentSystem.Infrastructure.Persistence.Configurations;

internal sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("departments");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(d => d.Name).HasMaxLength(150).IsRequired();
        // Short on purpose: teacher ids ("INS-SCI-01") are built from this.
        builder.Property(d => d.Code).HasMaxLength(10).IsRequired();

        builder.Property(d => d.CreatedAtUtc).IsRequired();
        builder.Property(d => d.UpdatedAtUtc).IsRequired();
        builder.Property(d => d.CreatedBy);
        builder.Property(d => d.UpdatedBy);

        builder.Property(d => d.RowVersion).IsRowVersion();

        builder.HasIndex(d => d.Code).IsUnique();
    }
}
