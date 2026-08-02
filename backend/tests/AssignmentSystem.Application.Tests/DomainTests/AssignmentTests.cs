using System;
using AssignmentSystem.Domain.Assignments;
using AssignmentSystem.Domain.Common;
using AssignmentSystem.Domain.Enums;
using Moq;
using Xunit;
using FluentAssertions;

namespace AssignmentSystem.Application.Tests.DomainTests;

public class AssignmentTests
{
    private readonly Mock<IClock> _clockMock;

    public AssignmentTests()
    {
        _clockMock = new Mock<IClock>();
        _clockMock.Setup(c => c.UtcNow).Returns(new DateTime(2026, 8, 2, 12, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Create_WithValidData_ShouldCreateAssignment()
    {
        // Arrange
        var deadline = _clockMock.Object.UtcNow.AddDays(7);

        // Act
        var assignment = Assignment.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Title",
            "Description",
            deadline,
            100m,
            true,
            _clockMock.Object);

        // Assert
        assignment.Should().NotBeNull();
        assignment.Title.Should().Be("Title");
        assignment.Status.Should().Be(AssignmentStatus.Draft);
    }

    [Fact]
    public void Create_WithDeadlineLessThanOneHour_ShouldThrowDomainException()
    {
        // Arrange
        var deadline = _clockMock.Object.UtcNow.AddMinutes(30);

        // Act
        Action act = () => Assignment.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Title",
            "Description",
            deadline,
            100m,
            true,
            _clockMock.Object);

        // Assert
        act.Should().Throw<DomainException>().WithMessage("Assignment deadline must be at least 1 hour from now.");
    }

    [Fact]
    public void Publish_DraftAssignment_ShouldChangeStatusToPublished()
    {
        // Arrange
        var deadline = _clockMock.Object.UtcNow.AddDays(7);
        var assignment = Assignment.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Title",
            "Description",
            deadline,
            100m,
            true,
            _clockMock.Object);

        // Act
        assignment.Publish();

        // Assert
        assignment.Status.Should().Be(AssignmentStatus.Published);
    }

    [Fact]
    public void Update_WhenPublishedWithSubmissions_ShouldOnlyAllowDescriptionChange()
    {
        // Arrange
        var deadline = _clockMock.Object.UtcNow.AddDays(7);
        var assignment = Assignment.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Title",
            "Description",
            deadline,
            100m,
            true,
            _clockMock.Object);
        assignment.Publish();

        // Act & Assert
        Action act = () => assignment.Update(
            "New Title",
            "New Description",
            deadline,
            100m,
            true,
            _clockMock.Object,
            hasSubmissions: true);

        act.Should().Throw<DomainException>();

        // Valid update (description only)
        assignment.Update(
            "Title",
            "New Description",
            deadline,
            100m,
            true,
            _clockMock.Object,
            hasSubmissions: true);

        assignment.Description.Should().Be("New Description");
    }
}
