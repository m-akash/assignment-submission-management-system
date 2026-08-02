using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AssignmentSystem.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Populates audit fields (CreatedAt/UpdatedAt/CreatedBy/UpdatedBy) and sets
/// soft-delete timestamps on every save. Reads the actor from <see cref="ICurrentUser"/>
/// — audit values are never trusted from the client.
/// </summary>
public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public AuditSaveChangesInterceptor(ICurrentUser currentUser, IClock clock)
    {
        _currentUser = currentUser;
        _clock = clock;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        UpdateAuditFields(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdateAuditFields(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void UpdateAuditFields(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = _clock.UtcNow;
        var actor = _currentUser.UserId;

        foreach (EntityEntry<BaseEntity> entry in context.ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAtUtc = now;
                    entry.Entity.UpdatedAtUtc = now;
                    if (actor is not null)
                    {
                        entry.Entity.CreatedBy = actor;
                        entry.Entity.UpdatedBy = actor;
                    }
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAtUtc = now;
                    if (actor is not null)
                    {
                        entry.Entity.UpdatedBy = actor;
                    }
                    // Prevent Created* from being overwritten on edits.
                    entry.Property(nameof(BaseEntity.CreatedAtUtc)).IsModified = false;
                    entry.Property(nameof(BaseEntity.CreatedBy)).IsModified = false;
                    break;
            }

            // Soft delete: convert Remove → Modified + set deleted flag.
            if (entry.Entity is ISoftDeletable softDeletable && entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                softDeletable.IsDeleted = true;
                softDeletable.DeletedAtUtc = now;
            }
        }
    }
}
