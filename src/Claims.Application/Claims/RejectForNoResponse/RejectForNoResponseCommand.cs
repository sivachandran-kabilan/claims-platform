using Claims.Application.Claims.Common;
using Claims.Application.Claims.Workflow;
using MediatR;

namespace Claims.Application.Claims.RejectForNoResponse;

/// <summary>InfoRequested → Rejected once the open request is 15+ days old. Fixed reason; no body needed.</summary>
public sealed record RejectForNoResponseCommand : ClaimCommand, IRequest<ClaimDetailResult>;

public sealed class RejectForNoResponseCommandHandler(ClaimWorkflow workflow)
    : IRequestHandler<RejectForNoResponseCommand, ClaimDetailResult>
{
    public Task<ClaimDetailResult> Handle(RejectForNoResponseCommand request, CancellationToken cancellationToken) =>
        workflow.RunAsync(request, ClaimAccess.AssignedStaffOrManager,
            (claim, actor, now) => claim.RejectForNoResponse(actor, now), cancellationToken);
}
