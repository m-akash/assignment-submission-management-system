using System;
using AssignmentSystem.Domain.AcademicYears;
using AssignmentSystem.Domain.Common;
using FluentAssertions;
using Xunit;

namespace AssignmentSystem.Application.Tests.DomainTests;

public class AcademicYearTests
{
    private static readonly DateOnly Start = new(2026, 7, 1);
    private static readonly DateOnly End = new(2027, 6, 30);

    [Fact]
    public void Create_WithValidData_ShouldCreateAcademicYear()
    {
        var year = AcademicYear.Create("2026-2027", Start, End, isCurrent: false);

        year.Name.Should().Be("2026-2027");
        year.StartDate.Should().Be(Start);
        year.EndDate.Should().Be(End);
        year.IsCurrent.Should().BeFalse();
    }

    [Fact]
    public void Create_ShouldTrimTheName()
    {
        var year = AcademicYear.Create("  2026-2027  ", Start, End, isCurrent: false);

        year.Name.Should().Be("2026-2027");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankName_ShouldThrowDomainException(string name)
    {
        Action act = () => AcademicYear.Create(name, Start, End, isCurrent: false);

        act.Should().Throw<DomainException>().WithMessage("*name is required*");
    }

    [Fact]
    public void Create_WithNameOverFiftyCharacters_ShouldThrowDomainException()
    {
        Action act = () => AcademicYear.Create(new string('x', 51), Start, End, isCurrent: false);

        act.Should().Throw<DomainException>().WithMessage("*cannot exceed 50 characters*");
    }

    [Fact]
    public void Create_WithEndBeforeStart_ShouldThrowDomainException()
    {
        Action act = () => AcademicYear.Create("2026-2027", End, Start, isCurrent: false);

        act.Should().Throw<DomainException>().WithMessage("*end date must be after the start date*");
    }

    /// <summary>
    /// A session cannot be a single day. The rule is strictly "after", not "not before", so
    /// the boundary belongs in a test of its own rather than being implied by the one above.
    /// </summary>
    [Fact]
    public void Create_WithEndEqualToStart_ShouldThrowDomainException()
    {
        Action act = () => AcademicYear.Create("2026-2027", Start, Start, isCurrent: false);

        act.Should().Throw<DomainException>().WithMessage("*end date must be after the start date*");
    }

    [Fact]
    public void Update_ShouldChangeNameAndDates()
    {
        var year = AcademicYear.Create("2026-2027", Start, End, isCurrent: false);
        var newStart = new DateOnly(2026, 8, 1);
        var newEnd = new DateOnly(2027, 7, 31);

        year.Update("2026-27", newStart, newEnd);

        year.Name.Should().Be("2026-27");
        year.StartDate.Should().Be(newStart);
        year.EndDate.Should().Be(newEnd);
    }

    /// <summary>
    /// Update deliberately does not touch the flag: clearing whoever else holds it needs a
    /// view of the other rows, which only the handler has. Editing a session's dates must
    /// not quietly move "current" off it as a side effect.
    /// </summary>
    [Fact]
    public void Update_ShouldLeaveTheCurrentFlagAlone()
    {
        var year = AcademicYear.Create("2026-2027", Start, End, isCurrent: true);

        year.Update("2026-2027", Start, End);

        year.IsCurrent.Should().BeTrue();
    }

    [Fact]
    public void Update_WithInvalidDates_ShouldThrowAndLeaveTheYearUnchanged()
    {
        var year = AcademicYear.Create("2026-2027", Start, End, isCurrent: false);

        Action act = () => year.Update("2026-2027", End, Start);

        act.Should().Throw<DomainException>();
        year.StartDate.Should().Be(Start);
        year.EndDate.Should().Be(End);
    }

    [Fact]
    public void MarkAsCurrent_AndClearCurrent_ShouldToggleTheFlag()
    {
        var year = AcademicYear.Create("2026-2027", Start, End, isCurrent: false);

        year.MarkAsCurrent();
        year.IsCurrent.Should().BeTrue();

        year.ClearCurrent();
        year.IsCurrent.Should().BeFalse();
    }
}
