using Claims.Application.Claims.Common;
using Claims.Application.Claims.Workflow;
using FluentValidation;
using MediatR;

namespace Claims.Application.Claims.RequestInformation;

/// <summary>UnderReview → InfoRequested. Raises InformationRequested so the claimant is notified.</summary>
public sealed record RequestInformationCommand : ClaimCommand, IRequest<ClaimDetailResult>
{
    public string Question { get; init; } = string.Empty;
}

public sealed class RequestInformationCommandValidator : AbstractValidator<RequestInformationCommand>
{
    public RequestInformationCommandValidator() => RuleFor(c => c.Question).ValidText(ClaimRules.TextMaxLength);
}

public sealed class RequestInformationCommandHandler(ClaimWorkflow workflow)
    : IRequestHandler<RequestInformationCommand, ClaimDetailResult>
{
    public Task<ClaimDetailResult> Handle(RequestInformationCommand request, CancellationToken cancellationToken) =>
        workflow.RunAsync(request, ClaimAccess.AssignedStaffOrManager,
            (claim, actor, now) => claim.RequestInformation(request.Question, actor, now), cancellationToken);
}
