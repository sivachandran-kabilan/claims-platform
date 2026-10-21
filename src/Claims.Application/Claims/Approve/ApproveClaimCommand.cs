using Claims.Application.Claims.Common;
using Claims.Application.Claims.Workflow;
using FluentValidation;
using MediatR;

namespace Claims.Application.Claims.Approve;

/// <summary>UnderReview → Approved. Needs a reserve; the approved amount may not exceed it (domain, 422).</summary>
public sealed record ApproveClaimCommand : ClaimCommand, IRequest<ClaimDetailResult>
{
    public decimal ApprovedAmount { get; init; }
}

public sealed class ApproveClaimCommandValidator : AbstractValidator<ApproveClaimCommand>
{
    public ApproveClaimCommandValidator() => RuleFor(c => c.ApprovedAmount).ValidAmount();
}

public sealed class ApproveClaimCommandHandler(ClaimWorkflow workflow) : IRequestHandler<ApproveClaimCommand, ClaimDetailResult>
{
    public Task<ClaimDetailResult> Handle(ApproveClaimCommand request, CancellationToken cancellationToken) =>
        workflow.RunAsync(request, ClaimAccess.AssignedStaffOrManager,
            (claim, actor, now) => claim.Approve(request.ApprovedAmount, actor, now), cancellationToken);
}
