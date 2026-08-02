using AssignmentSystem.Domain.Common;

namespace AssignmentSystem.Infrastructure.Common;

/// <summary>
/// Production clock — wraps <see cref="DateTime.UtcNow"/>. Replaced by a fake in
/// tests so deadline/late logic is deterministic.
/// </summary>
public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
