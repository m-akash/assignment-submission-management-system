using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Shared.Common;

namespace AssignmentSystem.Application.Features.Auth;

/// <summary>
/// Sets a password from a single-use setup link.
///
/// Every rejection returns the same error, whatever the real reason: unknown token, expired
/// token, one already spent, or an account since deactivated. Distinguishing them would turn
/// this endpoint into an oracle — a caller trying random tokens could tell "not a token" from
/// "a real token, just used", and the second answer is worth having.
///
/// A thin shell over <see cref="IPasswordSetupTokenService"/> on purpose: verifying a hash,
/// setting a password and dropping existing sessions have to happen together, and that
/// belongs behind one port rather than being sequenced here.
/// </summary>
public sealed class SetPasswordHandler : ICommandHandler<SetPasswordCommand>
{
    private readonly IPasswordSetupTokenService _passwordSetup;

    public SetPasswordHandler(IPasswordSetupTokenService passwordSetup) => _passwordSetup = passwordSetup;

    public async Task<Result> HandleAsync(SetPasswordCommand command, CancellationToken ct = default)
    {
        var redeemed = await _passwordSetup.RedeemPasswordSetupAsync(command.Token, command.NewPassword, ct);

        return redeemed
            ? Result.Success()
            : Result.Failure(Error.Validation(
                "Auth.InvalidPasswordSetupToken",
                "This link is no longer valid. It may have expired or already been used — ask your administrator to send a new one."));
    }
}

/// <summary>
/// Reports whether a setup link can still be used, without spending it. Always succeeds:
/// "this link is dead" is an answer, not a failure, and the page needs to render either way.
/// </summary>
public sealed class GetPasswordSetupStatusHandler : IQueryHandler<GetPasswordSetupStatusQuery, PasswordSetupStatusDto>
{
    private readonly IPasswordSetupTokenService _passwordSetup;

    public GetPasswordSetupStatusHandler(IPasswordSetupTokenService passwordSetup) => _passwordSetup = passwordSetup;

    public async Task<Result<PasswordSetupStatusDto>> HandleAsync(
        GetPasswordSetupStatusQuery query, CancellationToken ct = default)
    {
        var status = await _passwordSetup.InspectPasswordSetupAsync(query.Token, ct);

        return new PasswordSetupStatusDto(status.IsUsable, status.FullName, status.ExpiresAtUtc);
    }
}
