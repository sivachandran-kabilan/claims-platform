using Claims.Application.Common.Exceptions;
using Claims.Application.Common.Security;
using MediatR;

namespace Claims.Application.Identity.GetCurrentUser;

public sealed record GetCurrentUserQuery : IRequest<UserResponse>;

public sealed class GetCurrentUserQueryHandler(IIdentityService identity, ICurrentUser currentUser)
    : IRequestHandler<GetCurrentUserQuery, UserResponse>
{
    public async Task<UserResponse> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken) =>
        await identity.GetUserAsync(currentUser.UserId, cancellationToken)
        ?? throw new NotFoundException("User not found.");
}
