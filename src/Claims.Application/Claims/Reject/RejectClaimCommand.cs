using Claims.Application.Claims.Common;
using Claims.Application.Claims.Workflow;
using FluentValidation;
using MediatR;

namespace Claims.Application.Claims.Reject;

/// <summary>UnderReview → Rejected with a reason.</summary>
public sealed record RejectClaimCommand : ClaimCommand, IRequest<ClaimDetailResult>
{
    public string Reason { get; init; } = string.Empty;
}

public sealed class RejectClaimCommandValidator : AbstractValidator<RejectClaimCommand>
{
    public RejectClaimCommandValidator() => RuleFor(c => c.Reason).ValidText(ClaimRules.ReasonMaxLength);
}

public sealed class RejectClaimCommandHandler(ClaimWorkflow workflow) : IRequestHandler<RejectClaimCommand, ClaimDetailResult>
{
    public Task<ClaimDetailResult> Handle(RejectClaimCommand request, CancellationToken cancellationToken) =>
        workflow.RunAsync(request, ClaimAccess.AssignedStaffOrManager,
            (claim, actor, now) => claim.Reject(request.Reason, actor, now), cancellationToken);
}
