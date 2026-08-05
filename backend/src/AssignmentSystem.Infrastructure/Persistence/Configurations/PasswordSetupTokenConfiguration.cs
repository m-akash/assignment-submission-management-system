using AssignmentSystem.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssignmentSystem.Infrastructure.Persistence.Configurations;

internal sealed class PasswordSetupTokenConfiguration : IEntityTypeConfiguration<PasswordSetupToken>
{
    public void Configure(EntityTypeBuilder<PasswordSetupToken> builder)
    {
        builder.ToTable("password_setup_tokens");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(t => t.UserId).IsRequired();
        builder.Property(t => t.TokenHash).HasMaxLength(128).IsRequired();
        builder.Property(t => t.ExpiresAtUtc).IsRequired();
        builder.Property(t => t.ConsumedAtUtc);

        builder.Property(t => t.CreatedAtUtc).IsRequired();
        builder.Property(t => t.UpdatedAtUtc).IsRequired();
        builder.Property(t => t.CreatedBy);
        builder.Property(t => t.UpdatedBy);

        builder.Property(t => t.RowVersion).IsRowVersion();

        // The hash is the lookup key for every redemption — unique, as for refresh tokens.
        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => t.UserId);

        // Cascade: a deleted user's unspent setup links must not outlive them, since
        // redeeming one is a way in. Unlike the notification outbox there is nothing worth
        // keeping here — the row is a capability, not a record of something that happened.
        builder.HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
