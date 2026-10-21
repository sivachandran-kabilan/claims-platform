using Claims.Api.Auth;
using Claims.Application.Claims.AddDocument;
using Claims.Application.Claims.Approve;
using Claims.Application.Claims.Assess;
using Claims.Application.Claims.Assign;
using Claims.Application.Claims.Common;
using Claims.Application.Claims.Reassign;
using Claims.Application.Claims.Reject;
using Claims.Application.Claims.RejectForNoResponse;
using Claims.Application.Claims.RequestInformation;
using Claims.Application.Claims.RespondToInformation;
using Claims.Application.Claims.Settle;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Claims.Api.Controllers;

/// <summary>
/// One explicit endpoint per claim action (no generic "set status"). Every action needs If-Match
/// (428 if missing, 412 if stale) and returns the updated claim with its new ETag.
/// "Who may act" is checked in the application layer (ClaimAccess), not only by these policies.
/// </summary>
[ApiController]
[Route("api/v1/claims/{id:guid}")]
[Produces("application/json")]
public sealed class ClaimActionsController(ISender sender) : ControllerBase
{
    private const string IfMatch = "If-Match";

    [HttpPost("assign", Name = "AssignClaim")]
    [Authorize(Policy = Policies.Staff)]
    [ProducesResponseType<ClaimDetailResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> Assign(Guid id, [FromHeader(Name = IfMatch)] string? ifMatch,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] AssignClaimCommand? command, CancellationToken cancellationToken) =>
        Run((command ?? new AssignClaimCommand()) with { ClaimId = id, IfMatch = ifMatch }, cancellationToken);

    [HttpPost("reassign", Name = "ReassignClaim")]
    [Authorize(Policy = Policies.Manager)]
    [ProducesResponseType<ClaimDetailResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> Reassign(Guid id, [FromHeader(Name = IfMatch)] string? ifMatch,
        ReassignClaimCommand command, CancellationToken cancellationToken) =>
        Run(command with { ClaimId = id, IfMatch = ifMatch }, cancellationToken);

    [HttpPost("request-info", Name = "RequestClaimInformation")]
    [Authorize(Policy = Policies.Staff)]
    [ProducesResponseType<ClaimDetailResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> RequestInformation(Guid id, [FromHeader(Name = IfMatch)] string? ifMatch,
        RequestInformationCommand command, CancellationToken cancellationToken) =>
        Run(command with { ClaimId = id, IfMatch = ifMatch }, cancellationToken);

    [HttpPost("information", Name = "RespondToClaimInformation")]
    [Authorize(Policy = Policies.Claimant)]
    [ProducesResponseType<ClaimDetailResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> RespondToInformation(Guid id, [FromHeader(Name = IfMatch)] string? ifMatch,
        RespondToInformationCommand command, CancellationToken cancellationToken) =>
        Run(command with { ClaimId = id, IfMatch = ifMatch }, cancellationToken);

    [HttpPost("assess", Name = "AssessClaim")]
    [Authorize(Policy = Policies.Staff)]
    [ProducesResponseType<ClaimDetailResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> Assess(Guid id, [FromHeader(Name = IfMatch)] string? ifMatch,
        AssessClaimCommand command, CancellationToken cancellationToken) =>
        Run(command with { ClaimId = id, IfMatch = ifMatch }, cancellationToken);

    [HttpPost("approve", Name = "ApproveClaim")]
    [Authorize(Policy = Policies.Staff)]
    [ProducesResponseType<ClaimDetailResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> Approve(Guid id, [FromHeader(Name = IfMatch)] string? ifMatch,
        ApproveClaimCommand command, CancellationToken cancellationToken) =>
        Run(command with { ClaimId = id, IfMatch = ifMatch }, cancellationToken);

    [HttpPost("reject", Name = "RejectClaim")]
    [Authorize(Policy = Policies.Staff)]
    [ProducesResponseType<ClaimDetailResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> Reject(Guid id, [FromHeader(Name = IfMatch)] string? ifMatch,
        RejectClaimCommand command, CancellationToken cancellationToken) =>
        Run(command with { ClaimId = id, IfMatch = ifMatch }, cancellationToken);

    [HttpPost("reject-no-response", Name = "RejectClaimForNoResponse")]
    [Authorize(Policy = Policies.Staff)]
    [ProducesResponseType<ClaimDetailResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> RejectForNoResponse(Guid id, [FromHeader(Name = IfMatch)] string? ifMatch,
        CancellationToken cancellationToken) =>
        Run(new RejectForNoResponseCommand { ClaimId = id, IfMatch = ifMatch }, cancellationToken);

    [HttpPost("settle", Name = "SettleClaim")]
    [Authorize(Policy = Policies.Staff)]
    [ProducesResponseType<ClaimDetailResponse>(StatusCodes.Status200OK)]
    public Task<IActionResult> Settle(Guid id, [FromHeader(Name = IfMatch)] string? ifMatch,
        SettleClaimCommand command, CancellationToken cancellationToken) =>
        Run(command with { ClaimId = id, IfMatch = ifMatch }, cancellationToken);

    /// <summary>Adds document metadata. 201 with Location of the claim. If-Match (not Idempotency-Key) prevents duplicates on retry.</summary>
    [HttpPost("documents", Name = "AddClaimDocument")]
    [Authorize(Policy = Policies.AnyUser)]
    [ProducesResponseType<ClaimDetailResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> AddDocument(Guid id, [FromHeader(Name = IfMatch)] string? ifMatch,
        AddDocumentCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command with { ClaimId = id, IfMatch = ifMatch }, cancellationToken);
        Response.Headers.ETag = result.ETag;
        return CreatedAtRoute("GetClaim", new { id }, result.Claim);
    }

    private async Task<IActionResult> Run(IRequest<ClaimDetailResult> command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        Response.Headers.ETag = result.ETag;
        return Ok(result.Claim);
    }
}
