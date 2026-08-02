using FluentValidation;

namespace AssignmentSystem.Api.Controllers;

/// <summary>
/// Request-body validators (run via the global ValidationFilter). These guard the
/// request shape at the API boundary; semantic rules still run in the Application layer.
/// Kept here because they validate the request DTOs that live alongside the controller.
/// </summary>
public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email is required.")
            .MaximumLength(256);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}
