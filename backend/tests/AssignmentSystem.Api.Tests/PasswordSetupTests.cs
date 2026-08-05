using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AssignmentSystem.Api.Controllers;
using AssignmentSystem.Application.Features.Auth;
using AssignmentSystem.Domain.Enums;
using Xunit;

namespace AssignmentSystem.Api.Tests;

/// <summary>
/// Account creation as it reaches the account's owner: a welcome email carrying a single-use
/// link, and the endpoint that link leads to.
///
/// The tests read the token out of the queued notification body — the same way the recipient
/// would, out of the mail — because that round trip is the feature. A test that reached into
/// the database for the plaintext could not, since only the hash is stored.
/// </summary>
public sealed class PasswordSetupTests : IntegrationTestBase
{
    public PasswordSetupTests(ApiFactory api) : base(api) { }

    /// <summary>
    /// The welcome mail must carry a link and must not carry the password the admin typed.
    /// This is the whole reason the token flow exists, so it is asserted directly rather than
    /// left implied by the link working.
    /// </summary>
    [Fact]
    public async Task CreatingUser_QueuesWelcomeMailWithALinkAndNoPassword()
    {
        using var admin = await SignInAsAdminAsync();
        var (userId, _) = await CreateTeacherAsync(admin, "setup-mail");

        var welcome = await WelcomeMailAsync(admin, userId);

        welcome.Type.Should().Be(NotificationType.AccountCreated);
        welcome.Body.Should().Contain("/set-password?token=");
        welcome.Body.Should().NotContain(TestPassword,
            "a password must never travel by email — the link is what proves the mailbox");
        welcome.Body.Should().Contain("teacher", "the mail tells the recipient what kind of account it is");
    }

    /// <summary>
    /// The end-to-end path: read the link out of the mail, set a password, sign in with it.
    /// The admin-typed password must stop working — otherwise setting one would only add a
    /// second way in rather than taking ownership of the account.
    /// </summary>
    [Fact]
    public async Task SettingPasswordFromLink_LetsUserSignInAndRetiresTheAdminsPassword()
    {
        using var admin = await SignInAsAdminAsync();
        var (userId, email) = await CreateTeacherAsync(admin, "setup-use");
        var token = await SetupTokenAsync(admin, userId);

        const string chosen = "ChosenByMe123!";
        var set = await Client.PostAsJsonAsync("/api/v1/auth/set-password", new SetPasswordRequest(token, chosen));
        set.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var withChosen = await Client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, chosen));
        withChosen.StatusCode.Should().Be(HttpStatusCode.OK);

        var withAdmins = await Client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, TestPassword));
        withAdmins.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Single use. A link sitting in an inbox forever must not stay a way to seize the
    /// account, so the second redemption fails even with the correct token.
    /// </summary>
    [Fact]
    public async Task SetPassword_WithAnAlreadyUsedToken_IsRejected()
    {
        using var admin = await SignInAsAdminAsync();
        var (userId, email) = await CreateTeacherAsync(admin, "setup-once");
        var token = await SetupTokenAsync(admin, userId);

        var first = await Client.PostAsJsonAsync("/api/v1/auth/set-password", new SetPasswordRequest(token, "FirstChoice123!"));
        first.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var second = await Client.PostAsJsonAsync("/api/v1/auth/set-password", new SetPasswordRequest(token, "SecondChoice123!"));
        second.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // And the second password never took effect.
        var login = await Client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "SecondChoice123!"));
        login.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SetPassword_WithAnUnknownToken_IsRejected()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/v1/auth/set-password",
            new SetPasswordRequest("this-is-not-a-real-token", "Whatever123!"));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    /// <summary>
    /// Setting a password is the point at which the account changes hands, so anything
    /// already signed in on the admin's password must not survive it.
    /// </summary>
    [Fact]
    public async Task SettingPassword_RevokesSessionsOpenedWithTheOldPassword()
    {
        using var admin = await SignInAsAdminAsync();
        var (userId, email) = await CreateTeacherAsync(admin, "setup-sess");
        var token = await SetupTokenAsync(admin, userId);

        // A session established the old way — its refresh cookie is what must stop working.
        using var session = Api.CreateClient();
        var login = await session.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, TestPassword));
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        (await session.PostAsync("/api/v1/auth/refresh", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var set = await Client.PostAsJsonAsync("/api/v1/auth/set-password", new SetPasswordRequest(token, "NowMine123!"));
        set.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterwards = await session.PostAsync("/api/v1/auth/refresh", null);
        afterwards.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// The pre-flight check the set-password page makes. Usable before, dead after — and the
    /// dead answer carries no name, so a spent token cannot be used to find out whose it was.
    /// </summary>
    [Fact]
    public async Task PasswordSetupStatus_ReportsUsableBeforeRedemptionAndNothingAfter()
    {
        using var admin = await SignInAsAdminAsync();
        var (userId, _) = await CreateTeacherAsync(admin, "setup-stat");
        var token = await SetupTokenAsync(admin, userId);

        var before = await StatusAsync(token);
        before.IsUsable.Should().BeTrue();
        before.FullName.Should().NotBeNullOrEmpty();
        before.ExpiresAtUtc.Should().NotBeNull();
        before.ExpiresAtUtc!.Value.Should().BeAfter(DateTime.UtcNow);

        var set = await Client.PostAsJsonAsync("/api/v1/auth/set-password", new SetPasswordRequest(token, "AllSet123!"));
        set.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await StatusAsync(token);
        after.IsUsable.Should().BeFalse();
        after.FullName.Should().BeNull();
        after.ExpiresAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task PasswordSetupStatus_ForAnUnknownToken_IsUnusableAndAnonymous()
    {
        var status = await StatusAsync("definitely-not-issued");

        status.IsUsable.Should().BeFalse();
        status.FullName.Should().BeNull();
    }

    /// <summary>
    /// The setup endpoints are the one part of auth that must work with no credentials at
    /// all — a user who cannot sign in yet is exactly who they are for.
    /// </summary>
    [Fact]
    public async Task PasswordSetupEndpoints_AreReachableAnonymously()
    {
        using var anonymous = Api.CreateClient();

        var status = await anonymous.GetAsync("/api/v1/auth/set-password?token=whatever");
        status.StatusCode.Should().Be(HttpStatusCode.OK);

        // Rejected on the token, not on authentication — 400, never 401.
        var post = await anonymous.PostAsJsonAsync(
            "/api/v1/auth/set-password", new SetPasswordRequest("whatever", "Password123!"));
        post.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    /// <summary>
    /// A student is created and enrolled in one call, and the two are separate events, so both
    /// mails are queued. Asserted so the pairing is deliberate rather than incidental.
    /// </summary>
    [Fact]
    public async Task CreatingStudentWithAClass_QueuesBothTheWelcomeAndEnrollmentMails()
    {
        var world = await ProvisionWorldAsync("setup-both");
        using var admin = await SignInAsAdminAsync();

        var mail = await NotificationsForRecipientAsync(admin, world.StudentId);

        mail.Should().ContainSingle(n => n.Type == NotificationType.AccountCreated);
        mail.Should().ContainSingle(n => n.Type == NotificationType.StudentEnrolled);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task<(Guid Id, string Email)> CreateTeacherAsync(HttpClient admin, string label)
    {
        var tag = $"{label}-{Guid.NewGuid():N}"[..(label.Length + 9)];
        var email = $"{tag}@test.local";

        var response = await admin.PostAsJsonAsync("/api/v1/users",
            new CreateUserRequest(email, $"Teacher {tag}", TestPassword, Role.Teacher, null));
        response.EnsureSuccessStatusCode();

        var created = await ReadAsync<CreatedUser>(response);
        return (created.Id, email);
    }

    /// <summary>
    /// The plaintext token, pulled out of the emailed link exactly as a recipient would.
    /// Only its hash is stored, so this is the only way to obtain it after the fact.
    /// </summary>
    private static async Task<string> SetupTokenAsync(HttpClient admin, Guid userId)
    {
        var welcome = await WelcomeMailAsync(admin, userId);

        var match = Regex.Match(welcome.Body, @"/set-password\?token=([A-Za-z0-9_\-]+)");
        match.Success.Should().BeTrue("the welcome mail must contain a set-password link");

        return match.Groups[1].Value;
    }

    private static async Task<NotificationRow> WelcomeMailAsync(HttpClient admin, Guid userId)
    {
        var mail = await NotificationsForRecipientAsync(admin, userId);
        var welcome = mail.Where(n => n.Type == NotificationType.AccountCreated).ToList();

        welcome.Should().ContainSingle("creating an account queues exactly one welcome mail");
        return welcome[0];
    }

    private static async Task<List<NotificationRow>> NotificationsForRecipientAsync(HttpClient admin, Guid recipientId)
    {
        var response = await admin.GetAsync($"/api/v1/notifications?recipientId={recipientId}&pageSize=200");
        response.EnsureSuccessStatusCode();

        var (rows, _) = await ReadPageAsync<NotificationRow>(response);
        return rows;
    }

    private async Task<PasswordSetupStatusDto> StatusAsync(string token)
    {
        var response = await Client.GetAsync($"/api/v1/auth/set-password?token={Uri.EscapeDataString(token)}");
        response.EnsureSuccessStatusCode();
        return await ReadAsync<PasswordSetupStatusDto>(response);
    }

    private sealed record CreatedUser(Guid Id);

    private sealed record NotificationRow(
        Guid Id,
        Guid RecipientId,
        NotificationType Type,
        string Subject,
        string Body,
        NotificationStatus Status);
}
