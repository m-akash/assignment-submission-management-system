using AssignmentSystem.Domain.Assignments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssignmentSystem.Infrastructure.Persistence.Configurations;

internal sealed class AssignmentFileConfiguration : IEntityTypeConfiguration<AssignmentFile>
{
    public void Configure(EntityTypeBuilder<AssignmentFile> builder)
    {
        builder.ToTable("assignment_files");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(f => f.AssignmentId).IsRequired();
        builder.Property(f => f.UploadedById).IsRequired();
        builder.Property(f => f.StoredFileName).HasMaxLength(255).IsRequired();
        builder.Property(f => f.OriginalFileName).HasMaxLength(255).IsRequired();
        builder.Property(f => f.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(f => f.FileSizeBytes).IsRequired();
        builder.Property(f => f.RelativePath).HasMaxLength(500).IsRequired();
        builder.Property(f => f.UploadedAtUtc).IsRequired();

        builder.Property(f => f.CreatedAtUtc).IsRequired();
        builder.Property(f => f.UpdatedAtUtc).IsRequired();
        builder.Property(f => f.CreatedBy);
        builder.Property(f => f.UpdatedBy);

        builder.Property(f => f.RowVersion).IsRowVersion();

        builder.HasOne(f => f.UploadedBy)
            .WithMany()
            .HasForeignKey(f => f.UploadedById)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(f => f.AssignmentId);
    }
}
