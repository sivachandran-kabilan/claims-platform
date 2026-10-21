using Claims.Application.Claims.Common;
using Claims.Application.Common.Paging;
using Claims.Application.Common.Persistence;
using Claims.Application.Common.Security;
using Claims.Domain.Claims;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Claims.Application.Claims.ListClaims;

// ---- GET /claims (staff and managers) -------------------------------------------------------------

public sealed record ListClaimsQuery(
    int Page, int PageSize, ClaimStatus? Status, Market? Market, ClaimType? Type,
    Guid? AssignedTo, bool Unassigned, string Sort) : IRequest<PagedResult<ClaimSummaryResponse>>
{
    public const string SortOldestFirst = "submittedAt";
    public const string SortNewestFirst = "-submittedAt";
}

public sealed class ListClaimsQueryValidator : AbstractValidator<ListClaimsQuery>
{
    public ListClaimsQueryValidator()
    {
        this.ValidPaging(q => q.Page, q => q.PageSize);
        RuleFor(q => q.Status).IsInEnum();
        RuleFor(q => q.Market).IsInEnum();
        RuleFor(q => q.Type).IsInEnum();
        RuleFor(q => q.Sort).Must(s => s is ListClaimsQuery.SortOldestFirst or ListClaimsQuery.SortNewestFirst)
            .WithMessage($"Sort must be '{ListClaimsQuery.SortOldestFirst}' or '{ListClaimsQuery.SortNewestFirst}'.");
        RuleFor(q => q).Must(q => !(q.Unassigned && q.AssignedTo is not null))
            .OverridePropertyName("Unassigned").WithMessage("Use either assignedTo or unassigned, not both.");
    }
}

public sealed class ListClaimsQueryHandler(IClaimsDbContext db)
    : IRequestHandler<ListClaimsQuery, PagedResult<ClaimSummaryResponse>>
{
    public Task<PagedResult<ClaimSummaryResponse>> Handle(ListClaimsQuery request, CancellationToken cancellationToken)
    {
        var claims = db.Claims.AsNoTracking();
        if (request.Status is { } status) claims = claims.Where(c => c.Status == status);
        if (request.Market is { } market) claims = claims.Where(c => c.Market == market);
        if (request.Type is { } type) claims = claims.Where(c => c.Type == type);
        if (request.AssignedTo is { } assignee) claims = claims.Where(c => c.AssignedToUserId == assignee);
        if (request.Unassigned) claims = claims.Where(c => c.AssignedToUserId == null);

        // Id breaks ties so pages are stable.
        claims = request.Sort == ListClaimsQuery.SortOldestFirst
            ? claims.OrderBy(c => c.SubmittedAt).ThenBy(c => c.Id)
            : claims.OrderByDescending(c => c.SubmittedAt).ThenByDescending(c => c.Id);

        return claims.ToSummaries().ToPagedResultAsync(request.Page, request.PageSize, cancellationToken);
    }
}

// ---- GET /claims/queue (staff and managers) -------------------------------------------------------

/// <summary>Unassigned Submitted claims, oldest first: what staff pick up next.</summary>
public sealed record ClaimQueueQuery(int Page, int PageSize) : IRequest<PagedResult<ClaimSummaryResponse>>;

public sealed class ClaimQueueQueryValidator : AbstractValidator<ClaimQueueQuery>
{
    public ClaimQueueQueryValidator() => this.ValidPaging(q => q.Page, q => q.PageSize);
}

public sealed class ClaimQueueQueryHandler(IClaimsDbContext db)
    : IRequestHandler<ClaimQueueQuery, PagedResult<ClaimSummaryResponse>>
{
    public Task<PagedResult<ClaimSummaryResponse>> Handle(ClaimQueueQuery request, CancellationToken cancellationToken) =>
        db.Claims.AsNoTracking()
            .Where(c => c.Status == ClaimStatus.Submitted && c.AssignedToUserId == null)
            .OrderBy(c => c.SubmittedAt).ThenBy(c => c.Id)
            .ToSummaries()
            .ToPagedResultAsync(request.Page, request.PageSize, cancellationToken);
}

// ---- GET /claims/mine (claimant) ------------------------------------------------------------------

public sealed record ListMyClaimsQuery(int Page, int PageSize, ClaimStatus? Status) : IRequest<PagedResult<ClaimSummaryResponse>>;

public sealed class ListMyClaimsQueryValidator : AbstractValidator<ListMyClaimsQuery>
{
    public ListMyClaimsQueryValidator()
    {
        this.ValidPaging(q => q.Page, q => q.PageSize);
        RuleFor(q => q.Status).IsInEnum();
    }
}

public sealed class ListMyClaimsQueryHandler(IClaimsDbContext db, ICurrentUser currentUser)
    : IRequestHandler<ListMyClaimsQuery, PagedResult<ClaimSummaryResponse>>
{
    public async Task<PagedResult<ClaimSummaryResponse>> Handle(ListMyClaimsQuery request, CancellationToken cancellationToken)
    {
        var claims = db.Claims.AsNoTracking().VisibleTo(currentUser);
        if (request.Status is { } status) claims = claims.Where(c => c.Status == status);

        var page = await claims
            .OrderByDescending(c => c.SubmittedAt).ThenByDescending(c => c.Id)
            .ToSummaries()
            .ToPagedResultAsync(request.Page, request.PageSize, cancellationToken);

        return page with { Items = page.Items.Select(ClaimReadModel.ForClaimant).ToList() };
    }
}
