using AssignmentSystem.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssignmentSystem.Infrastructure.Persistence.Configurations;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(t => t.UserId).IsRequired();
        builder.Property(t => t.TokenHash).HasMaxLength(128).IsRequired();
        builder.Property(t => t.ExpiresAtUtc).IsRequired();
        builder.Property(t => t.RevokedAtUtc);
        builder.Property(t => t.ReplacedByTokenHash).HasMaxLength(128);
        builder.Property(t => t.CreatedByIp);

        builder.Property(t => t.CreatedAtUtc).IsRequired();
        builder.Property(t => t.UpdatedAtUtc).IsRequired();
        builder.Property(t => t.CreatedBy);
        builder.Property(t => t.UpdatedBy);

        builder.Property(t => t.RowVersion).IsRowVersion();

        // Token hash is looked up on every refresh — unique index it.
        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => t.UserId);
    }
}
