using Claims.Application.Claims.Common;
using Claims.Application.Claims.Workflow;
using Claims.Domain.Claims;
using FluentValidation;
using MediatR;

namespace Claims.Application.Claims.AddDocument;

/// <summary>Records document metadata (file storage is a non-goal; StorageRef is an opaque reference).</summary>
public sealed record AddDocumentCommand : ClaimCommand, IRequest<ClaimDetailResult>
{
    public static readonly string[] AllowedContentTypes = ["application/pdf", "image/jpeg", "image/png"];

    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public string StorageRef { get; init; } = string.Empty;
}

public sealed class AddDocumentCommandValidator : AbstractValidator<AddDocumentCommand>
{
    public AddDocumentCommandValidator()
    {
        RuleFor(c => c.FileName).ValidText(255);
        RuleFor(c => c.ContentType).Must(t => AddDocumentCommand.AllowedContentTypes.Contains(t))
            .WithMessage($"Content type must be one of: {string.Join(", ", AddDocumentCommand.AllowedContentTypes)}.");
        RuleFor(c => c.SizeBytes).InclusiveBetween(1, Claim.MaxDocumentBytes);
        RuleFor(c => c.StorageRef).ValidText(500);
    }
}

public sealed class AddDocumentCommandHandler(ClaimWorkflow workflow) : IRequestHandler<AddDocumentCommand, ClaimDetailResult>
{
    public Task<ClaimDetailResult> Handle(AddDocumentCommand request, CancellationToken cancellationToken) =>
        workflow.RunAsync(request, ClaimAccess.DocumentUploader,
            (claim, actor, now) => claim.AddDocument(request.FileName, request.ContentType, request.SizeBytes, request.StorageRef, actor, now),
            cancellationToken);
}
