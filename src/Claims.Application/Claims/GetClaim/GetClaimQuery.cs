using Claims.Application.Claims.Common;
using Claims.Application.Common.Exceptions;
using Claims.Application.Common.Persistence;
using Claims.Application.Common.Security;
using MediatR;

namespace Claims.Application.Claims.GetClaim;

public sealed record GetClaimQuery(Guid ClaimId) : IRequest<ClaimDetailResult>;

public sealed class GetClaimQueryHandler(IClaimsDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetClaimQuery, ClaimDetailResult>
{
    // Another claimant's claim is filtered out in SQL, so it is reported exactly like a missing one.
    public async Task<ClaimDetailResult> Handle(GetClaimQuery request, CancellationToken cancellationToken) =>
        await ClaimReadModel.GetDetailAsync(db, request.ClaimId, currentUser, cancellationToken)
        ?? throw new NotFoundException("Claim not found.");
}
