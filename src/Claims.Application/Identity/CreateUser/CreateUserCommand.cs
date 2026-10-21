using Claims.Domain.Common;
using FluentValidation;
using MediatR;

namespace Claims.Application.Identity.CreateUser;

/// <summary>ClaimManager creates a ClaimStaff or ClaimManager account. The only runtime path to a staff account.</summary>
public sealed record CreateUserCommand(string Email, string Password, string DisplayName, string Role) : IRequest<UserResponse>;

public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(c => c.Email).ValidEmail();
        RuleFor(c => c.Password).ValidPassword();
        RuleFor(c => c.DisplayName).ValidDisplayName();
        RuleFor(c => c.Role)
            .Must(role => Roles.Staff.Contains(role))
            .WithMessage($"Role must be one of: {string.Join(", ", Roles.Staff)}.");
    }
}

public sealed class CreateUserCommandHandler(IIdentityService identity) : IRequestHandler<CreateUserCommand, UserResponse>
{
    public Task<UserResponse> Handle(CreateUserCommand request, CancellationToken cancellationToken) =>
        identity.CreateUserAsync(
            new NewUser(EmailAddress.Normalize(request.Email), request.Password, request.DisplayName.Trim(), request.Role),
            cancellationToken);
}
