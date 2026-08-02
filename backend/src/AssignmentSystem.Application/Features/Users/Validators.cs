using FluentValidation;
using AssignmentSystem.Domain.Enums;

namespace AssignmentSystem.Application.Features.Users;

public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email is required.")
            .MaximumLength(256);

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MaximumLength(150).WithMessage("Full name cannot exceed 150 characters.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(6).WithMessage("Password must be at least 6 characters.");

        RuleFor(x => x.Role)
            .IsInEnum().WithMessage("A valid role is required.");

        RuleFor(x => x.ClassId)
            .NotEmpty().WithMessage("A student must be assigned to a class.")
            .When(x => x.Role == Role.Student);

        RuleFor(x => x.ClassId)
            .Empty().WithMessage("Only students may be assigned to a class.")
            .When(x => x.Role != Role.Student);
    }
}

public sealed class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("User id is required.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MaximumLength(150).WithMessage("Full name cannot exceed 150 characters.");

        RuleFor(x => x.Password)
            .MinimumLength(6).WithMessage("Password must be at least 6 characters.")
            .When(x => !string.IsNullOrEmpty(x.Password));
    }
}
