using Claims.Application.Common.Persistence;
using Claims.Application.Common.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Claims.Application.Notifications;

public sealed record NotificationResponse(
    Guid Id, Guid ClaimId, string ClaimNumber, string Type, string Message, DateTimeOffset CreatedAt);

/// <summary>The caller's latest notifications, newest first. Bounded (role matrix: latest 50), so not paged.</summary>
public sealed record GetMyNotificationsQuery : IRequest<IReadOnlyList<NotificationResponse>>
{
    public const int Limit = 50;
}

public sealed class GetMyNotificationsQueryHandler(IClaimsDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetMyNotificationsQuery, IReadOnlyList<NotificationResponse>>
{
    public async Task<IReadOnlyList<NotificationResponse>> Handle(GetMyNotificationsQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        return await db.Notifications.AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id)
            .Take(GetMyNotificationsQuery.Limit)
            .Select(n => new NotificationResponse(n.Id, n.ClaimId, n.ClaimNumber, n.Type, n.Message, n.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
