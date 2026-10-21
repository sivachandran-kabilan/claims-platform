using Claims.Application.Claims.Common;
using Claims.Application.Claims.Workflow;
using Claims.Application.Common.Security;
using Claims.Application.Identity;
using MediatR;

namespace Claims.Application.Claims.Assign;

/// <summary>Submitted → UnderReview. No assignee means "assign to me"; staff may only assign themselves.</summary>
public sealed record AssignClaimCommand : ClaimCommand, IRequest<ClaimDetailResult>
{
    public Guid? AssigneeUserId { get; init; }
}

public sealed class AssignClaimCommandHandler(ClaimWorkflow workflow, ICurrentUser currentUser, IIdentityService identity)
    : IRequestHandler<AssignClaimCommand, ClaimDetailResult>
{
    public Task<ClaimDetailResult> Handle(AssignClaimCommand request, CancellationToken cancellationToken)
    {
        var assignee = request.AssigneeUserId ?? currentUser.UserId;
        return workflow.RunAsync(request, ClaimAccess.CanAssign(assignee), async (claim, actor, now) =>
        {
            await ClaimAccess.EnsureAssignableAsync(identity, assignee, cancellationToken);
            claim.Assign(assignee, actor, now);
        }, cancellationToken);
    }
}
