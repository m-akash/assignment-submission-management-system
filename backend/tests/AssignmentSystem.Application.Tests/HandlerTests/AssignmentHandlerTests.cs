using System;
using System.Threading;
using System.Threading.Tasks;
using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Application.Features.Assignments;
using AssignmentSystem.Domain.Assignments;
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
    private readonly Mock<ICurrentUser> _currentUserMock;
    private readonly Mock<IClock> _clockMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly CreateAssignmentHandler _createHandler;

    public AssignmentHandlerTests()
    {
        _assignmentRepoMock = new Mock<IRepository<Assignment>>();
        _teacherAssignmentRepoMock = new Mock<IRepository<TeacherAssignment>>();
        _currentUserMock = new Mock<ICurrentUser>();
        _clockMock = new Mock<IClock>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _clockMock.Setup(c => c.UtcNow).Returns(new DateTime(2026, 8, 2, 12, 0, 0, DateTimeKind.Utc));

        _createHandler = new CreateAssignmentHandler(
            _assignmentRepoMock.Object,
            _teacherAssignmentRepoMock.Object,
            _currentUserMock.Object,
            _clockMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_CreateAssignment_WhenUserIsStudent_ShouldReturnForbidden()
    {
        // Arrange
        _currentUserMock.Setup(u => u.Role).Returns(Role.Student);
        var command = new CreateAssignmentCommand(Guid.NewGuid(), "Title", "Desc", DateTime.UtcNow.AddDays(1), 100m, true);

        // Act
        var result = await _createHandler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task Handle_CreateAssignment_WhenTeacherAssignmentNotFound_ShouldReturnNotFound()
    {
        // Arrange
        _currentUserMock.Setup(u => u.Role).Returns(Role.Teacher);
        _teacherAssignmentRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TeacherAssignment)null!);

        var command = new CreateAssignmentCommand(Guid.NewGuid(), "Title", "Desc", DateTime.UtcNow.AddDays(1), 100m, true);

        // Act
        var result = await _createHandler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }
}
