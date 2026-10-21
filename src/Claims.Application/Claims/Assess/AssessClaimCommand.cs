using Claims.Application.Claims.Common;
using Claims.Application.Claims.Workflow;
using FluentValidation;
using MediatR;

namespace Claims.Application.Claims.Assess;

/// <summary>Sets the reserve (UnderReview only). Not a status change.</summary>
public sealed record AssessClaimCommand : ClaimCommand, IRequest<ClaimDetailResult>
{
    public decimal Reserve { get; init; }
}

public sealed class AssessClaimCommandValidator : AbstractValidator<AssessClaimCommand>
{
    public AssessClaimCommandValidator() => RuleFor(c => c.Reserve).ValidAmount();
}

public sealed class AssessClaimCommandHandler(ClaimWorkflow workflow) : IRequestHandler<AssessClaimCommand, ClaimDetailResult>
{
    public Task<ClaimDetailResult> Handle(AssessClaimCommand request, CancellationToken cancellationToken) =>
        workflow.RunAsync(request, ClaimAccess.AssignedStaffOrManager,
            (claim, actor, now) => claim.SetReserve(request.Reserve, actor, now), cancellationToken);
}
