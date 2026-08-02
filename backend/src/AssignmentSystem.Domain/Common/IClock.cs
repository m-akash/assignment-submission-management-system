namespace AssignmentSystem.Domain.Common;

/// <summary>
/// Abstraction over the clock so domain deadline logic is deterministic under test.
/// Implemented in Infrastructure by <c>SystemClock</c> (wraps <see cref="DateTimeOffset.UtcNow"/>).
/// Kept in Domain because entities reference it directly for invariant checks.
/// </summary>
public interface IClock
{
    DateTime UtcNow { get; }
}
