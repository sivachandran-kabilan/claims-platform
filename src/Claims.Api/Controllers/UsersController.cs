using Claims.Api.Auth;
using Claims.Application.Common.Paging;
using Claims.Application.Identity;
using Claims.Application.Identity.CreateUser;
using Claims.Application.Identity.ListUsers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Claims.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
[Produces("application/json")]
public sealed class UsersController(ISender sender) : ControllerBase
{
    /// <summary>Creates a ClaimStaff or ClaimManager account.</summary>
    [HttpPost(Name = "CreateUser")]
    [Authorize(Policy = Policies.Manager)]
    [ProducesResponseType<UserResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateUserCommand command, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await sender.Send(command, cancellationToken));

    [HttpGet(Name = "ListUsers")]
    [Authorize(Policy = Policies.Manager)]
    [ProducesResponseType<PagedResult<UserResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = Paging.DefaultPageSize,
        [FromQuery] string? role = null) =>
        Ok(await sender.Send(new ListUsersQuery(page, pageSize, role), cancellationToken));
}
