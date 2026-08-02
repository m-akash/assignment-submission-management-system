using System;
using AssignmentSystem.Domain.Assignments;
using AssignmentSystem.Domain.Common;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Domain.Submissions;
using Moq;
using Xunit;
using FluentAssertions;

namespace AssignmentSystem.Application.Tests.DomainTests;

public class SubmissionTests
{
    private readonly Mock<IClock> _clockMock;
    private readonly Assignment _assignment;

    public SubmissionTests()
    {
        _clockMock = new Mock<IClock>();
        _clockMock.Setup(c => c.UtcNow).Returns(new DateTime(2026, 8, 2, 12, 0, 0, DateTimeKind.Utc));

        var assignmentClock = new Mock<IClock>();
        assignmentClock.Setup(c => c.UtcNow).Returns(_clockMock.Object.UtcNow.AddDays(-2));

        _assignment = Assignment.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Title",
            "Description",
            _clockMock.Object.UtcNow.AddDays(2),
            100m,
            true,
            assignmentClock.Object);
        _assignment.Publish();
    }

    [Fact]
    public void Create_WhenAssignmentIsDraft_ShouldThrowDomainException()
    {
        // Arrange
        var draftAssignment = Assignment.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Title",
            "Description",
            _clockMock.Object.UtcNow.AddDays(7),
            100m,
            true,
            _clockMock.Object);

        // Act
        Action act = () => Submission.Create(
            draftAssignment.Id,
            Guid.NewGuid(),
            "Answer",
            false,
            draftAssignment,
            _clockMock.Object);

        // Assert
        act.Should().Throw<DomainException>().WithMessage("Cannot submit to an unpublished assignment.");
    }

    [Fact]
    public void Create_WithoutAnswerOrFile_ShouldThrowDomainException()
    {
        // Act
        Action act = () => Submission.Create(
            _assignment.Id,
            Guid.NewGuid(),
            null,
            false,
            _assignment,
            _clockMock.Object);

        // Assert
        act.Should().Throw<DomainException>().WithMessage("A submission must include a text answer or a file.");
    }

    [Fact]
    public void Create_AfterDeadline_ShouldMarkAsLate()
    {
        // Arrange
        _clockMock.Setup(c => c.UtcNow).Returns(_assignment.DeadlineUtc.AddHours(1));

        // Act
        var submission = Submission.Create(
            _assignment.Id,
            Guid.NewGuid(),
            "Late Answer",
            false,
            _assignment,
            _clockMock.Object);

        // Assert
        submission.Status.Should().Be(SubmissionStatus.Late);
    }

    [Fact]
    public void Grade_WithMarksExceedingMax_ShouldThrowDomainException()
    {
        // Arrange
        var submission = Submission.Create(
            _assignment.Id,
            Guid.NewGuid(),
            "My work",
            false,
            _assignment,
            _clockMock.Object);

        // Act
        Action act = () => submission.Grade(105m, "Feedback", Guid.NewGuid(), _assignment, _clockMock.Object);

        // Assert
        act.Should().Throw<DomainException>().WithMessage("Marks (105) cannot exceed the maximum (100).");
    }

    [Fact]
    public void Grade_WithValidMarks_ShouldSetStatusToGraded()
    {
        // Arrange
        var submission = Submission.Create(
            _assignment.Id,
            Guid.NewGuid(),
            "My work",
            false,
            _assignment,
            _clockMock.Object);

        // Act
        submission.Grade(85m, "Good job!", Guid.NewGuid(), _assignment, _clockMock.Object);

        // Assert
        submission.Status.Should().Be(SubmissionStatus.Graded);
        submission.Marks.Should().Be(85m);
        submission.Feedback.Should().Be("Good job!");
    }
}
