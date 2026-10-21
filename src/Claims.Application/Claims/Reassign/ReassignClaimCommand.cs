using Claims.Application.Claims.Common;
using Claims.Application.Claims.Workflow;
using Claims.Application.Identity;
using FluentValidation;
using MediatR;

namespace Claims.Application.Claims.Reassign;

/// <summary>Manager moves the claim to another staff member. Not a status change; a reason is required.</summary>
public sealed record ReassignClaimCommand : ClaimCommand, IRequest<ClaimDetailResult>
{
    public Guid AssigneeUserId { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public sealed class ReassignClaimCommandValidator : AbstractValidator<ReassignClaimCommand>
{
    public ReassignClaimCommandValidator()
    {
        RuleFor(c => c.AssigneeUserId).NotEmpty();
        RuleFor(c => c.Reason).ValidText(ClaimRules.ReasonMaxLength);
    }
}

public sealed class ReassignClaimCommandHandler(ClaimWorkflow workflow, IIdentityService identity)
    : IRequestHandler<ReassignClaimCommand, ClaimDetailResult>
{
    public Task<ClaimDetailResult> Handle(ReassignClaimCommand request, CancellationToken cancellationToken) =>
        workflow.RunAsync(request, ClaimAccess.ManagerOnly, async (claim, actor, now) =>
        {
            await ClaimAccess.EnsureAssignableAsync(identity, request.AssigneeUserId, cancellationToken);
            claim.Reassign(request.AssigneeUserId, request.Reason, actor, now);
        }, cancellationToken);
}
