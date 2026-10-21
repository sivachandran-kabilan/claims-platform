using Claims.Application.Claims.Common;
using Claims.Application.Claims.Workflow;
using FluentValidation;
using MediatR;

namespace Claims.Application.Claims.RespondToInformation;

/// <summary>InfoRequested → UnderReview. Only the claimant who owns the claim.</summary>
public sealed record RespondToInformationCommand : ClaimCommand, IRequest<ClaimDetailResult>
{
    public string Response { get; init; } = string.Empty;
}

public sealed class RespondToInformationCommandValidator : AbstractValidator<RespondToInformationCommand>
{
    public RespondToInformationCommandValidator() => RuleFor(c => c.Response).ValidText(ClaimRules.TextMaxLength);
}

public sealed class RespondToInformationCommandHandler(ClaimWorkflow workflow)
    : IRequestHandler<RespondToInformationCommand, ClaimDetailResult>
{
    public Task<ClaimDetailResult> Handle(RespondToInformationCommand request, CancellationToken cancellationToken) =>
        workflow.RunAsync(request, ClaimAccess.Owner,
            (claim, actor, now) => claim.RespondToInformation(request.Response, actor, now), cancellationToken);
}
