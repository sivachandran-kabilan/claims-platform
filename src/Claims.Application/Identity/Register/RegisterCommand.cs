using Claims.Domain.Common;
using FluentValidation;
using MediatR;

namespace Claims.Application.Identity.Register;

/// <summary>Public self-registration. There is no role field: every registered user is a Claimant.</summary>
public sealed record RegisterCommand(string Email, string Password, string DisplayName) : IRequest<UserResponse>;

public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(c => c.Email).ValidEmail();
        RuleFor(c => c.Password).ValidPassword();
        RuleFor(c => c.DisplayName).ValidDisplayName();
    }
}

public sealed class RegisterCommandHandler(IIdentityService identity) : IRequestHandler<RegisterCommand, UserResponse>
{
    public Task<UserResponse> Handle(RegisterCommand request, CancellationToken cancellationToken) =>
        identity.CreateUserAsync(
            new NewUser(EmailAddress.Normalize(request.Email), request.Password, request.DisplayName.Trim(), Roles.Claimant),
            cancellationToken);
}
