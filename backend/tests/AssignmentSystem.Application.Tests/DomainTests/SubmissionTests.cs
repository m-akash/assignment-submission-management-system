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
            hasFile: true,
            draftAssignment,
            _clockMock.Object);

        // Assert
        act.Should().Throw<DomainException>().WithMessage("Cannot submit to an unpublished assignment.");
    }

    [Fact]
    public void Create_WithoutFile_ShouldThrowDomainException()
    {
        // Act
        Action act = () => Submission.Create(
            _assignment.Id,
            Guid.NewGuid(),
            hasFile: false,
            _assignment,
            _clockMock.Object);

        // Assert
        act.Should().Throw<DomainException>().WithMessage("A submission must include at least one file.");
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
            hasFile: true,
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
            hasFile: true,
            _assignment,
            _clockMock.Object);

        // Act
        Action act = () => submission.Grade(105m, "Feedback", Guid.NewGuid(), _assignment, _clockMock.Object);

        // Assert
        act.Should().Throw<DomainException>().WithMessage("Marks (105) cannot exceed the maximum (100).");
    }

    /// <summary>
    /// Rule X1. Not reachable over HTTP — publishing is one-way, so a submission can never
    /// belong to a draft — which is exactly why the invariant belongs on the entity.
    /// </summary>
    [Fact]
    public void Grade_WhenAssignmentIsNotPublished_ShouldThrowDomainException()
    {
        // Arrange: a submission on a published assignment, then graded against a draft.
        var submission = Submission.Create(
            _assignment.Id, Guid.NewGuid(), hasFile: true, _assignment, _clockMock.Object);

        var draft = Assignment.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            "Draft", "Description", _clockMock.Object.UtcNow.AddDays(7), 100m, true, _clockMock.Object);

        // Act
        Action act = () => submission.Grade(50m, "Too early", Guid.NewGuid(), draft, _clockMock.Object);

        // Assert
        act.Should().Throw<DomainException>().WithMessage("Cannot grade an unpublished assignment.");
    }

    [Fact]
    public void Grade_WithNegativeMarks_ShouldThrowDomainException()
    {
        var submission = Submission.Create(
            _assignment.Id, Guid.NewGuid(), hasFile: true, _assignment, _clockMock.Object);

        Action act = () => submission.Grade(-1m, null, Guid.NewGuid(), _assignment, _clockMock.Object);

        act.Should().Throw<DomainException>();
    }

    /// <summary>Rule B2 — the deadline closes editing when resubmission is not allowed.</summary>
    [Fact]
    public void MarkSubmitted_AfterDeadline_WhenResubmissionDisallowed_ShouldThrowDomainException()
    {
        var submission = Submission.Create(
            _assignment.Id, Guid.NewGuid(), hasFile: true, _assignment, _clockMock.Object);

        _clockMock.Setup(c => c.UtcNow).Returns(_assignment.DeadlineUtc.AddMinutes(1));

        Action act = () => submission.MarkSubmitted(
            hasFile: true, allowResubmission: false, _assignment.DeadlineUtc, _clockMock.Object);

        act.Should().Throw<DomainException>().WithMessage("Cannot update a submission after the deadline.");
    }

    /// <summary>Rule X2 — a permitted post-deadline edit is recorded as Late.</summary>
    [Fact]
    public void MarkSubmitted_AfterDeadline_WhenResubmissionAllowed_ShouldMarkAsLate()
    {
        var submission = Submission.Create(
            _assignment.Id, Guid.NewGuid(), hasFile: true, _assignment, _clockMock.Object);

        _clockMock.Setup(c => c.UtcNow).Returns(_assignment.DeadlineUtc.AddMinutes(1));

        submission.MarkSubmitted(
            hasFile: true, allowResubmission: true, _assignment.DeadlineUtc, _clockMock.Object);

        submission.Status.Should().Be(SubmissionStatus.Late);
    }

    [Fact]
    public void MarkSubmitted_WhenAlreadyGraded_ShouldThrowDomainException()
    {
        var submission = Submission.Create(
            _assignment.Id, Guid.NewGuid(), hasFile: true, _assignment, _clockMock.Object);
        submission.Grade(80m, "Done", Guid.NewGuid(), _assignment, _clockMock.Object);

        Action act = () => submission.MarkSubmitted(
            hasFile: true, allowResubmission: true, _assignment.DeadlineUtc, _clockMock.Object);

        act.Should().Throw<DomainException>().WithMessage("Cannot edit a submission that has already been graded.");
    }

    /// <summary>
    /// The attachments are what a submission is, so handing in with the last one removed is
    /// refused — the check is on what is stored, which is why the caller passes it in.
    /// </summary>
    [Fact]
    public void MarkSubmitted_WithoutFile_ShouldThrowDomainException()
    {
        var submission = Submission.Create(
            _assignment.Id, Guid.NewGuid(), hasFile: true, _assignment, _clockMock.Object);

        Action act = () => submission.MarkSubmitted(
            hasFile: false, allowResubmission: true, _assignment.DeadlineUtc, _clockMock.Object);

        act.Should().Throw<DomainException>().WithMessage("A submission must include at least one file.");
    }

    [Fact]
    public void Grade_WithValidMarks_ShouldSetStatusToGraded()
    {
        // Arrange
        var submission = Submission.Create(
            _assignment.Id,
            Guid.NewGuid(),
            hasFile: true,
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
