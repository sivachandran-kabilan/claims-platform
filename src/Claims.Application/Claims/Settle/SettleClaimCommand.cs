using Claims.Application.Claims.Common;
using Claims.Application.Claims.Workflow;
using FluentValidation;
using MediatR;

namespace Claims.Application.Claims.Settle;

/// <summary>Approved → Settled. The only way to reach Settled; amount may not exceed the approved amount (422).</summary>
public sealed record SettleClaimCommand : ClaimCommand, IRequest<ClaimDetailResult>
{
    public decimal Amount { get; init; }
}

public sealed class SettleClaimCommandValidator : AbstractValidator<SettleClaimCommand>
{
    public SettleClaimCommandValidator() => RuleFor(c => c.Amount).ValidAmount();
}

public sealed class SettleClaimCommandHandler(ClaimWorkflow workflow) : IRequestHandler<SettleClaimCommand, ClaimDetailResult>
{
    public Task<ClaimDetailResult> Handle(SettleClaimCommand request, CancellationToken cancellationToken) =>
        workflow.RunAsync(request, ClaimAccess.AssignedStaffOrManager,
            (claim, actor, now) => claim.Settle(request.Amount, actor, now), cancellationToken);
}
