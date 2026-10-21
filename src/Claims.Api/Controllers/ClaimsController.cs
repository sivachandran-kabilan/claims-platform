using Claims.Api.Auth;
using Claims.Application.Claims.Common;
using Claims.Application.Claims.GetClaim;
using Claims.Application.Claims.ListClaims;
using Claims.Application.Claims.Submit;
using Claims.Application.Common.Paging;
using Claims.Domain.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Claims.Api.Controllers;

[ApiController]
[Route("api/v1/claims")]
[Produces("application/json")]
public sealed class ClaimsController(ISender sender) : ControllerBase
{
    /// <summary>Claimant reports a claim. Requires an Idempotency-Key header; a retry returns the same claim.</summary>
    [HttpPost(Name = "SubmitClaim")]
    [Authorize(Policy = Policies.Claimant)]
    [ProducesResponseType<ClaimDetailResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Submit(
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        SubmitClaimRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(SubmitClaimCommand.From(idempotencyKey, request), cancellationToken);
        Response.Headers.ETag = result.ETag;
        return CreatedAtRoute("GetClaim", new { id = result.Claim.Id }, result.Claim);
    }

    /// <summary>Claim detail with history. A claimant asking for another claimant's claim gets 404.</summary>
    [HttpGet("{id:guid}", Name = "GetClaim")]
    [Authorize(Policy = Policies.AnyUser)]
    [ProducesResponseType<ClaimDetailResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetClaimQuery(id), cancellationToken);
        Response.Headers.ETag = result.ETag;
        return Ok(result.Claim);
    }

    [HttpGet("mine", Name = "ListMyClaims")]
    [Authorize(Policy = Policies.Claimant)]
    [ProducesResponseType<PagedResult<ClaimSummaryResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Mine(
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = Paging.DefaultPageSize,
        [FromQuery] ClaimStatus? status = null) =>
        Ok(await sender.Send(new ListMyClaimsQuery(page, pageSize, status), cancellationToken));

    [HttpGet(Name = "ListClaims")]
    [Authorize(Policy = Policies.Staff)]
    [ProducesResponseType<PagedResult<ClaimSummaryResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = Paging.DefaultPageSize,
        [FromQuery] ClaimStatus? status = null,
        [FromQuery] Market? market = null,
        [FromQuery] ClaimType? type = null,
        [FromQuery] Guid? assignedTo = null,
        [FromQuery] bool unassigned = false,
        [FromQuery] string sort = ListClaimsQuery.SortNewestFirst) =>
        Ok(await sender.Send(
            new ListClaimsQuery(page, pageSize, status, market, type, assignedTo, unassigned, sort), cancellationToken));

    /// <summary>Unassigned Submitted claims, oldest first.</summary>
    [HttpGet("queue", Name = "GetClaimQueue")]
    [Authorize(Policy = Policies.Staff)]
    [ProducesResponseType<PagedResult<ClaimSummaryResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Queue(
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = Paging.DefaultPageSize) =>
        Ok(await sender.Send(new ClaimQueueQuery(page, pageSize), cancellationToken));
}
