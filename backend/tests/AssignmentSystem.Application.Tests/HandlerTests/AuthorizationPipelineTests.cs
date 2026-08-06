using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Application.Common.Authorization;
using AssignmentSystem.Application.Common.Behaviors;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Application.Features.Assignments;
using AssignmentSystem.Application.Features.Auth;
using AssignmentSystem.Application.Features.Users;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Shared.Common;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AssignmentSystem.Application.Tests.HandlerTests;

/// <summary>
/// The role gate used to be a hand-written <c>if</c> at the top of every handler. It is now
/// one decorator applied to all of them, which means these few tests cover what forty
/// scattered assertions used to — and, more usefully, cover the handlers nobody thought to
/// write an assertion for.
/// </summary>
public class AuthorizationPipelineTests
{
    private static readonly CreateAssignmentCommand TeacherOnlyCommand =
        new(Guid.NewGuid(), "Title", "Desc", DateTime.UtcNow.AddDays(7), 100m, true);

    private static AuthorizationDecorator<CreateAssignmentCommand, AssignmentDto> Decorate(
        ICommandHandler<CreateAssignmentCommand, AssignmentDto> inner, ICurrentUser currentUser) =>
        new(inner, currentUser, NullLogger<AuthorizationDecorator<CreateAssignmentCommand, AssignmentDto>>.Instance);

    private static ICurrentUser Caller(Role? role, bool authenticated = true)
    {
        var mock = new Mock<ICurrentUser>();
        mock.Setup(u => u.Role).Returns(role);
        mock.Setup(u => u.UserId).Returns(Guid.NewGuid());
        mock.Setup(u => u.IsAuthenticated).Returns(authenticated);
        return mock.Object;
    }

    [Theory]
    [InlineData(Role.Student)]
    [InlineData(Role.Admin)]
    public async Task Command_SentByADisallowedRole_IsForbiddenAndNeverReachesTheHandler(Role role)
    {
        var inner = new Mock<ICommandHandler<CreateAssignmentCommand, AssignmentDto>>();

        var result = await Decorate(inner.Object, Caller(role))
            .HandleAsync(TeacherOnlyCommand, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Forbidden);

        // The point of a gate is that the work behind it does not happen.
        inner.Verify(
            h => h.HandleAsync(It.IsAny<CreateAssignmentCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Command_SentByTheAllowedRole_ReachesTheHandler()
    {
        var inner = new Mock<ICommandHandler<CreateAssignmentCommand, AssignmentDto>>();
        inner.Setup(h => h.HandleAsync(It.IsAny<CreateAssignmentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AssignmentDto>.Failure(Error.NotFound("Reached", "reached the handler")));

        var result = await Decorate(inner.Object, Caller(Role.Teacher))
            .HandleAsync(TeacherOnlyCommand, CancellationToken.None);

        result.Error.Code.Should().Be("Reached");
    }

    /// <summary>
    /// 401, not 403: "sign in" and "you cannot do this" are different instructions, and a
    /// client that conflates them logs a signed-in user out over a permissions error.
    /// </summary>
    [Fact]
    public async Task Command_SentByAnUnauthenticatedCaller_IsUnauthorized()
    {
        var inner = new Mock<ICommandHandler<CreateAssignmentCommand, AssignmentDto>>();

        var result = await Decorate(inner.Object, Caller(role: null, authenticated: false))
            .HandleAsync(TeacherOnlyCommand, CancellationToken.None);

        result.Error.Type.Should().Be(ErrorType.Unauthorized);
    }

    [Theory]
    [InlineData(typeof(CreateAssignmentCommand), Role.Student, false)]
    [InlineData(typeof(CreateAssignmentCommand), Role.Teacher, true)]
    [InlineData(typeof(GetUsersQuery), Role.Admin, true)]
    [InlineData(typeof(GetUsersQuery), Role.Teacher, false)]
    [InlineData(typeof(GetCurrentUserQuery), Role.Student, true)]
    [InlineData(typeof(GetAssignmentsQuery), Role.Student, true)]
    public void Policy_ResolvesTheDeclaredRolesForAMessage(Type messageType, Role role, bool allowed)
    {
        AuthorizationPolicy.Check(messageType, Caller(role))
            .Should().Match(e => (e == null) == allowed);
    }

    [Fact]
    public void AnonymousMessages_DoNotRequireAnIdentity()
    {
        AuthorizationPolicy.Check(typeof(LoginCommand), Caller(role: null, authenticated: false))
            .Should().BeNull();
    }

    /// <summary>
    /// The guarantee the whole design rests on: a command or query that forgot to declare who
    /// may send it cannot exist. <c>AddApplication</c> runs this at startup and refuses to
    /// build the container; running it here means the failure shows up in a red test rather
    /// than a failed deploy.
    /// </summary>
    [Fact]
    public void EveryCommandAndQuery_DeclaresAnAuthorizationStance()
    {
        var act = () => AuthorizationPolicy.ValidateAllMessagesAreAnnotated(
            typeof(CreateAssignmentCommand).Assembly);

        act.Should().NotThrow();
    }

    /// <summary>
    /// And the guard itself works — otherwise the test above passes for the wrong reason.
    /// </summary>
    [Fact]
    public void TheAnnotationGuard_RejectsAnUnannotatedMessage()
    {
        var act = () => AuthorizationPolicy.ValidateAllMessagesAreAnnotated(
            typeof(UnannotatedProbeCommand).Assembly);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*UnannotatedProbeCommand*");
    }

    /// <summary>Deliberately carries no authorization attribute. Exists only for the test above.</summary>
    internal sealed record UnannotatedProbeCommand : ICommand;
}
