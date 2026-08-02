using AssignmentSystem.Domain.Common;

namespace AssignmentSystem.Domain.Submissions;

/// <summary>
/// Marks value object. Encodes the grading invariant centrally: marks must be
/// non-negative and cannot exceed the assignment's maximum (rules B4, X5).
/// The <c>OutOf</c> bound travels with the value so the rule can never be bypassed
/// by a handler passing an unrelated max.
/// </summary>
public sealed class Marks : ValueObject
{
    /// <summary>0 .. <see cref="OutOf"/>.</summary>
    public decimal Value { get; }

    /// <summary>The assignment maximum this grade is bound by.</summary>
    public decimal OutOf { get; }

    private Marks(decimal value, decimal outOf)
    {
        Value = value;
        OutOf = outOf;
    }

    /// <summary>
    /// Creates marks, throwing <see cref="DomainException"/> if the value is negative
    /// or exceeds the maximum.
    /// </summary>
    public static Marks Create(decimal value, decimal outOf)
    {
        if (outOf <= 0)
        {
            throw new DomainException("Maximum marks must be greater than zero.");
        }

        if (value < 0)
        {
            throw new DomainException("Marks cannot be negative.");
        }

        if (value > outOf)
        {
            throw new DomainException($"Marks ({value}) cannot exceed the maximum ({outOf}).");
        }

        // round to 2 decimal places to match numeric(5,2)
        return new Marks(Math.Round(value, 2), Math.Round(outOf, 2));
    }

    public double Percentage => OutOf == 0 ? 0 : (double)(Value / OutOf) * 100;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
        yield return OutOf;
    }

    public override string ToString() => $"{Value}/{OutOf}";
}
