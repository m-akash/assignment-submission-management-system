using AssignmentSystem.Domain.Classes;
using AssignmentSystem.Domain.Common;
using FluentAssertions;
using Xunit;

namespace AssignmentSystem.Application.Tests.DomainTests;

/// <summary>
/// A class is a grade and a section, held apart. Nothing composes the two into a stored name,
/// so these cover the two values themselves, the invariants that guard them, and the one
/// derived label that exists for email prose.
/// </summary>
public class ClassTests
{
    [Fact]
    public void Creating_KeepsTheGradeAndSectionApart()
    {
        var klass = Class.Create(9, "A");

        klass.Level.Should().Be(9);
        klass.Section.Should().Be("A");
    }

    [Fact]
    public void Creating_TrimsTheSection()
    {
        var klass = Class.Create(1, "  B  ");

        klass.Section.Should().Be("B");
    }

    [Fact]
    public void Updating_MovesBothTheGradeAndTheSection()
    {
        var klass = Class.Create(9, "A");

        klass.Update(12, "C");

        klass.Level.Should().Be(12);
        klass.Section.Should().Be("C");
    }

    /// <summary>
    /// The one place the two are joined — for email subjects, which cannot hold two fields.
    /// The grade is the number, never a numeral.
    /// </summary>
    [Fact]
    public void DisplayName_ReadsAsTheGradeNumberAndTheSection()
    {
        Class.Create(9, "A").DisplayName.Should().Be("Class 9 - Section A");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Creating_WithoutASection_IsRejected(string? section)
    {
        var act = () => Class.Create(9, section!);

        act.Should().Throw<DomainException>().WithMessage("Section is required.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(-1)]
    public void Creating_OutsideGradesOneToTwelve_IsRejected(int level)
    {
        var act = () => Class.Create(level, "A");

        act.Should().Throw<DomainException>().WithMessage("*between 1 and 12*");
    }

    [Fact]
    public void Creating_WithAnOverlongSection_IsRejected()
    {
        var act = () => Class.Create(9, new string('A', 51));

        act.Should().Throw<DomainException>().WithMessage("*cannot exceed 50*");
    }
}
