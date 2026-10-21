using Claims.Application.Common.Exceptions;
using FluentValidation;
using MediatR;

namespace Claims.Application.Identity.Login;

public sealed record LoginCommand(string Email, string Password) : IRequest<LoginResponse>;

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAtUtc, string TokenType);

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(c => c.Email).NotEmpty().MaximumLength(256);
        RuleFor(c => c.Password).NotEmpty().MaximumLength(128);
    }
}

public sealed class LoginCommandHandler(IIdentityService identity, ITokenGenerator tokens)
    : IRequestHandler<LoginCommand, LoginResponse>
{
    public async Task<LoginResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        // Unknown email, wrong password and lockout are indistinguishable to the caller.
        var user = await identity.CheckCredentialsAsync(EmailAddress.Normalize(request.Email), request.Password, cancellationToken)
            ?? throw new AuthenticationFailedException();

        var token = tokens.Create(user);
        return new LoginResponse(token.Value, token.ExpiresAtUtc, "Bearer");
    }
}
