using System;
using System.Threading;
using System.Threading.Tasks;
using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Application.Features.Assignments;
using AssignmentSystem.Domain.Assignments;
using AssignmentSystem.Domain.ClassCourses;
using AssignmentSystem.Domain.Common;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Domain.TeacherAssignments;
using AssignmentSystem.Shared.Common;
using Moq;
using Xunit;
using FluentAssertions;

namespace AssignmentSystem.Application.Tests.HandlerTests;

public class AssignmentHandlerTests
{
    private readonly Mock<IRepository<Assignment>> _assignmentRepoMock;
    private readonly Mock<IRepository<TeacherAssignment>> _teacherAssignmentRepoMock;
    private readonly Mock<IRepository<ClassCourse>> _classCourseRepoMock;
    private readonly Mock<ICurrentUser> _currentUserMock;
    private readonly Mock<IClock> _clockMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly CreateAssignmentHandler _createHandler;

    private static readonly Guid OfferingId = Guid.NewGuid();
    private static readonly Guid TeacherId = Guid.NewGuid();

    public AssignmentHandlerTests()
    {
        _assignmentRepoMock = new Mock<IRepository<Assignment>>();
        _teacherAssignmentRepoMock = new Mock<IRepository<TeacherAssignment>>();
        _classCourseRepoMock = new Mock<IRepository<ClassCourse>>();
        _currentUserMock = new Mock<ICurrentUser>();
        _clockMock = new Mock<IClock>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _clockMock.Setup(c => c.UtcNow).Returns(new DateTime(2026, 8, 2, 12, 0, 0, DateTimeKind.Utc));

        _createHandler = new CreateAssignmentHandler(
            _assignmentRepoMock.Object,
            _teacherAssignmentRepoMock.Object,
            _classCourseRepoMock.Object,
            _currentUserMock.Object,
            _clockMock.Object,
            _unitOfWorkMock.Object);
    }

    private void GivenOfferingExists() =>
        _classCourseRepoMock
            .Setup(r => r.GetByIdAsync(OfferingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ClassCourse.Create(Guid.NewGuid(), Guid.NewGuid()));

    private void GivenTeacherIsMappedToOffering(bool mapped) =>
        _teacherAssignmentRepoMock
            .Setup(r => r.AnyAsync(It.IsAny<ISpecification<TeacherAssignment>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mapped);

    private static CreateAssignmentCommand Command(Guid? classCourseId = null, Guid? teacherId = null) =>
        new(classCourseId ?? OfferingId, "Title", "Desc",
            new DateTime(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc), 100m, true, teacherId);

    [Fact]
    public async Task Handle_CreateAssignment_WhenUserIsStudent_ShouldReturnForbidden()
    {
        _currentUserMock.Setup(u => u.Role).Returns(Role.Student);

        var result = await _createHandler.HandleAsync(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task Handle_CreateAssignment_WhenOfferingNotFound_ShouldReturnNotFound()
    {
        _currentUserMock.Setup(u => u.Role).Returns(Role.Teacher);
        _currentUserMock.Setup(u => u.UserId).Returns(TeacherId);
        _classCourseRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClassCourse)null!);

        var result = await _createHandler.HandleAsync(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    /// <summary>
    /// Rule B3 at its most important: the offering exists, but this teacher was never
    /// assigned to teach it. Without this check a teacher could set work for any class in
    /// the school by guessing an offering id.
    /// </summary>
    [Fact]
    public async Task Handle_CreateAssignment_WhenTeacherIsNotMappedToOffering_ShouldReturnForbidden()
    {
        _currentUserMock.Setup(u => u.Role).Returns(Role.Teacher);
        _currentUserMock.Setup(u => u.UserId).Returns(TeacherId);
        GivenOfferingExists();
        GivenTeacherIsMappedToOffering(false);

        var result = await _createHandler.HandleAsync(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        _assignmentRepoMock.Verify(
            r => r.AddAsync(It.IsAny<Assignment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// An admin has to say who the work belongs to — there is no "current teacher" to fall
    /// back on, and inventing one would produce an assignment nobody can publish or grade.
    /// </summary>
    [Fact]
    public async Task Handle_CreateAssignment_WhenAdminOmitsTeacher_ShouldReturnValidationError()
    {
        _currentUserMock.Setup(u => u.Role).Returns(Role.Admin);
        _currentUserMock.Setup(u => u.UserId).Returns(Guid.NewGuid());
        GivenOfferingExists();

        var result = await _createHandler.HandleAsync(Command(teacherId: null), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    /// <summary>
    /// A teacher's own id is taken from the token, so a request naming a colleague still
    /// creates the assignment under the caller — authorship cannot be spoofed through the body.
    /// </summary>
    [Fact]
    public async Task Handle_CreateAssignment_WhenTeacherNamesAnother_ShouldUseCallerAsAuthor()
    {
        var someoneElse = Guid.NewGuid();
        _currentUserMock.Setup(u => u.Role).Returns(Role.Teacher);
        _currentUserMock.Setup(u => u.UserId).Returns(TeacherId);
        GivenOfferingExists();
        GivenTeacherIsMappedToOffering(true);

        Assignment? added = null;
        _assignmentRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Assignment>(), It.IsAny<CancellationToken>()))
            .Callback<Assignment, CancellationToken>((a, _) => added = a);

        // Asserted at the write boundary on purpose. The handler's return value is a DTO
        // projected from a re-read that carries the class, course and teacher names through
        // navigation properties — only a real database populates those, so the round trip is
        // covered by the integration suite. What matters here is which teacher id was persisted.
        await _createHandler.HandleAsync(Command(teacherId: someoneElse), CancellationToken.None);

        added.Should().NotBeNull();
        added!.TeacherId.Should().Be(TeacherId);
        added.ClassCourseId.Should().Be(OfferingId);
        added.Status.Should().Be(AssignmentStatus.Draft);
    }
}
