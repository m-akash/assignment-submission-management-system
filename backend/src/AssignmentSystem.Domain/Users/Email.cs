using System.Text.RegularExpressions;
using AssignmentSystem.Domain.Common;

namespace AssignmentSystem.Domain.Users;

/// <summary>
/// Email value object. Centralises format validation and normalization (lowercase, trimmed)
/// so a bad email can never enter the domain. Persisted as an EF Core owned type.
/// </summary>
public sealed partial class Email : ValueObject
{
    private static readonly Regex EmailPattern = CreateEmailPattern();

    public string Value { get; }

    private Email(string value) => Value = value;

    /// <summary>Creates an email, throwing <see cref="DomainException"/> if invalid.</summary>
    public static Email Create(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new DomainException("Email cannot be empty.");
        }

        var normalized = raw.Trim().ToLowerInvariant();

        if (normalized.Length > 256 || !EmailPattern.IsMatch(normalized))
        {
            throw new DomainException($"'{raw}' is not a valid email address.");
        }

        return new Email(normalized);
    }

    /// <summary>Used by EF Core to materialize from the stored (already normalized) value.</summary>
    internal static Email FromStored(string stored) => new(stored);

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex CreateEmailPattern();
}
