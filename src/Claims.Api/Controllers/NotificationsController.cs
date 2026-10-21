using Claims.Api.Auth;
using Claims.Application.Notifications;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Claims.Api.Controllers;

[ApiController]
[Route("api/v1/notifications")]
[Produces("application/json")]
public sealed class NotificationsController(ISender sender) : ControllerBase
{
    /// <summary>The claimant's latest 50 notifications, newest first.</summary>
    [HttpGet("mine", Name = "ListMyNotifications")]
    [Authorize(Policy = Policies.Claimant)]
    [ProducesResponseType<IReadOnlyList<NotificationResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Mine(CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetMyNotificationsQuery(), cancellationToken));
}
