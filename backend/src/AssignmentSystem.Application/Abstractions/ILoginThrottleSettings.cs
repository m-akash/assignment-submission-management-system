namespace AssignmentSystem.Application.Abstractions;

/// <summary>
/// Login throttle knobs, surfaced to the Application layer without it having to know they
/// come from <c>AuthOptions</c> — the same shape <see cref="INotificationSettings"/> uses.
/// </summary>
public interface ILoginThrottleSettings
{
    /// <summary>Consecutive wrong passwords before the account locks.</summary>
    int MaxFailedLoginAttempts { get; }

    /// <summary>How long the lock holds once it trips.</summary>
    TimeSpan LockoutDuration { get; }
}
