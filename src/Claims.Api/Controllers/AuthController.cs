using Claims.Api.Auth;
using Claims.Application.Identity;
using Claims.Application.Identity.GetCurrentUser;
using Claims.Application.Identity.Login;
using Claims.Application.Identity.Register;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Claims.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
public sealed class AuthController(ISender sender) : ControllerBase
{
    /// <summary>Registers a Claimant. Any role in the body is ignored. No token is returned.</summary>
    [HttpPost("register", Name = "Register")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<UserResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Register(RegisterCommand command, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await sender.Send(command, cancellationToken));

    [HttpPost("login", Name = "Login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Login(LoginCommand command, CancellationToken cancellationToken) =>
        Ok(await sender.Send(command, cancellationToken));

    [HttpGet("me", Name = "GetCurrentUser")]
    [Authorize(Policy = Policies.AnyUser)]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetCurrentUserQuery(), cancellationToken));
}
