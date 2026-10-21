using Claims.Application.Common.Exceptions;
using Claims.Application.Identity;
using Claims.Application.Identity.CreateUser;
using Claims.Application.Identity.ListUsers;
using Claims.Application.Identity.Login;
using Claims.Application.Identity.Register;
using Claims.Domain.Common;
using FluentAssertions;
using NSubstitute;

namespace Claims.Application.Tests.Identity;

public sealed class RegisterCommandHandlerTests
{
    [Fact]
    public async Task Handle_AnyRequest_CreatesClaimantWithNormalizedEmail()
    {
        var identity = Substitute.For<IIdentityService>();
        var handler = new RegisterCommandHandler(identity);

        await handler.Handle(new RegisterCommand("  Alice@Example.COM ", "a-long-enough-pass", " Alice "), CancellationToken.None);

        await identity.Received(1).CreateUserAsync(
            new NewUser("alice@example.com", "a-long-enough-pass", "Alice", Roles.Claimant), Arg.Any<CancellationToken>());
    }
}

public sealed class LoginCommandHandlerTests
{
    private readonly IIdentityService _identity = Substitute.For<IIdentityService>();
    private readonly ITokenGenerator _tokens = Substitute.For<ITokenGenerator>();

    [Fact]
    public async Task Handle_InvalidCredentials_ThrowsAuthenticationFailed()
    {
        _identity.CheckCredentialsAsync(default!, default!, default).ReturnsForAnyArgs((UserResponse?)null);
        var handler = new LoginCommandHandler(_identity, _tokens);

        var act = () => handler.Handle(new LoginCommand("a@b.com", "wrong-password"), CancellationToken.None);

        await act.Should().ThrowAsync<AuthenticationFailedException>();
        _tokens.DidNotReceiveWithAnyArgs().Create(default!);
    }

    [Fact]
    public async Task Handle_ValidCredentials_ReturnsBearerToken()
    {
        var user = new UserResponse(Guid.NewGuid(), "a@b.com", "A", Roles.Claimant);
        var expires = DateTimeOffset.UtcNow.AddMinutes(15);
        _identity.CheckCredentialsAsync("a@b.com", "right-password", Arg.Any<CancellationToken>()).Returns(user);
        _tokens.Create(user).Returns(new AccessToken("jwt", expires));
        var handler = new LoginCommandHandler(_identity, _tokens);

        var result = await handler.Handle(new LoginCommand("A@B.com", "right-password"), CancellationToken.None);

        result.Should().Be(new LoginResponse("jwt", expires, "Bearer"));
    }
}

public sealed class IdentityValidatorTests
{
    [Theory]
    [InlineData(Roles.ClaimStaff, true)]
    [InlineData(Roles.ClaimManager, true)]
    [InlineData(Roles.Claimant, false)]
    [InlineData("Admin", false)]
    public void CreateUserValidator_Role_OnlyStaffRolesAllowed(string role, bool valid)
    {
        var result = new CreateUserCommandValidator().Validate(
            new CreateUserCommand("staff@example.com", "a-long-enough-pass", "Staff", role));

        result.IsValid.Should().Be(valid);
    }

    [Theory]
    [InlineData(1, 100, true)]
    [InlineData(1, 101, false)]
    [InlineData(0, 20, false)]
    public void ListUsersValidator_Paging_EnforcesBounds(int page, int pageSize, bool valid)
    {
        new ListUsersQueryValidator().Validate(new ListUsersQuery(page, pageSize, null)).IsValid.Should().Be(valid);
    }

    [Fact]
    public void RegisterValidator_NineCharacterPassword_IsInvalid()
    {
        new RegisterCommandValidator().Validate(new RegisterCommand("a@b.com", "123456789", "A"))
            .Errors.Should().ContainSingle(e => e.PropertyName == nameof(RegisterCommand.Password));
    }
}
