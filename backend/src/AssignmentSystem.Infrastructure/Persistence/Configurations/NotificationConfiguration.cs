using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssignmentSystem.Infrastructure.Persistence.Configurations;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");

        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(n => n.RecipientId).IsRequired();

        builder.Property(n => n.RecipientEmail)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(n => n.Type)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(n => n.Subject)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(n => n.Body).IsRequired();

        builder.Property(n => n.Status)
            .HasConversion<int>()
            .HasDefaultValue(NotificationStatus.Pending)
            .IsRequired();

        builder.Property(n => n.AttemptCount).IsRequired().HasDefaultValue(0);
        builder.Property(n => n.LastAttemptAtUtc);
        builder.Property(n => n.SentAtUtc);
        builder.Property(n => n.NextAttemptAtUtc);
        builder.Property(n => n.ClaimedAtUtc);
        builder.Property(n => n.LastError).HasMaxLength(2000);

        // Context ids only, with no FK — see Notification's remarks: the outbox is a record
        // of mail sent and must outlive the assignment or submission it refers to.
        builder.Property(n => n.AssignmentId);
        builder.Property(n => n.SubmissionId);

        builder.Property(n => n.CreatedAtUtc).IsRequired();
        builder.Property(n => n.UpdatedAtUtc).IsRequired();
        builder.Property(n => n.CreatedBy);
        builder.Property(n => n.UpdatedBy);

        builder.Property(n => n.RowVersion).IsRowVersion();

        builder.HasOne(n => n.Recipient)
            .WithMany()
            .HasForeignKey(n => n.RecipientId)
            .OnDelete(DeleteBehavior.Cascade);

        // The dispatcher's claim query: oldest eligible first. The filter keeps the index
        // proportional to the backlog rather than to the whole (ever-growing) outbox, and
        // covers Processing (3) as well as Pending (0) because a sweep also reclaims rows
        // stranded by a dispatcher that died mid-batch.
        builder.HasIndex(n => new { n.Status, n.CreatedAtUtc })
            .HasFilter("status IN (0, 3)");

        builder.HasIndex(n => n.RecipientId);
    }
}
