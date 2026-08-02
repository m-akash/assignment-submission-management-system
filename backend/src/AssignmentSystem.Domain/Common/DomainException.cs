namespace AssignmentSystem.Domain.Common;

/// <summary>
/// Thrown when a domain invariant is violated in a way the caller should not
/// silently ignore. Expected, rule-shaped failures are returned as <c>Result</c>;
/// <see cref="DomainException"/> is reserved for programmer errors (e.g. an
/// invalid state transition that should never be reachable).
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string message) : base(message) { }

    public DomainException(string message, Exception inner) : base(message, inner) { }
}
