using FluentValidation;

namespace AssignmentSystem.Application.Features.Auth;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email is required.")
            .MaximumLength(256);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(1).WithMessage("Password is required.");
    }
}

public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("Refresh token is required.");
    }
}

public sealed class RevokeTokenCommandValidator : AbstractValidator<RevokeTokenCommand>
{
    public RevokeTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("Refresh token is required.");
    }
}

public sealed class SetPasswordCommandValidator : AbstractValidator<SetPasswordCommand>
{
    public SetPasswordCommandValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("A password setup token is required.")
            // Bounded so a megabyte of junk is rejected before it reaches a SHA-256 and a
            // database round trip. The tokens this issues are 43 characters.
            .MaximumLength(200).WithMessage("That is not a valid password setup token.");

        // The same floor an admin faces in CreateUserRequestValidator — a user choosing
        // their own password should not be held to a weaker rule than one chosen for them.
        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("A password is required.")
            .MinimumLength(6).WithMessage("Password must be at least 6 characters.")
            .MaximumLength(128).WithMessage("Password cannot exceed 128 characters.");
    }
}

public sealed class GetPasswordSetupStatusQueryValidator : AbstractValidator<GetPasswordSetupStatusQuery>
{
    public GetPasswordSetupStatusQueryValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("A password setup token is required.")
            .MaximumLength(200).WithMessage("That is not a valid password setup token.");
    }
}
