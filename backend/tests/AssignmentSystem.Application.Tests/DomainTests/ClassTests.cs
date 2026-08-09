using AssignmentSystem.Domain.Classes;
using AssignmentSystem.Domain.Common;
using FluentAssertions;
using Xunit;

namespace AssignmentSystem.Application.Tests.DomainTests;

/// <summary>
/// The class name is derived, not entered — an admin supplies only the grade and the section.
/// These cover the composition and the invariants that make it possible.
/// </summary>
public class ClassTests
{
    [Fact]
    public void Creating_ComposesTheNameFromGradeAndSection()
    {
        var klass = Class.Create(9, "A");

        klass.Name.Should().Be("Class IX - Section A");
        klass.Level.Should().Be(9);
        klass.Section.Should().Be("A");
        klass.GradeLabel.Should().Be("IX");
    }

    [Fact]
    public void Creating_TrimsTheSectionBeforeComposing()
    {
        var klass = Class.Create(1, "  B  ");

        klass.Section.Should().Be("B");
        klass.Name.Should().Be("Class I - Section B");
    }

    /// <summary>
    /// Renaming is not a separate operation: moving the class to another grade or section
    /// recomposes the name, so the two can never disagree.
    /// </summary>
    [Fact]
    public void Updating_RecomposesTheName()
    {
        var klass = Class.Create(9, "A");

        klass.Update(12, "C");

        klass.Name.Should().Be("Class XII - Section C");
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
