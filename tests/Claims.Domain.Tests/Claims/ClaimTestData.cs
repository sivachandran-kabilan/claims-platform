using Claims.Domain.Claims;
using Claims.Domain.Common;

namespace Claims.Domain.Tests.Claims;

/// <summary>Builds claims in a given status through the public methods only (no shortcuts into state).</summary>
internal static class ClaimTestData
{
    public static readonly DateTimeOffset Now = new(2026, 10, 21, 2, 0, 0, TimeSpan.Zero);
    public static readonly Actor Claimant = new(Guid.NewGuid(), Roles.Claimant);
    public static readonly Actor Staff = new(Guid.NewGuid(), Roles.ClaimStaff);
    public static readonly Actor Manager = new(Guid.NewGuid(), Roles.ClaimManager);

    public const decimal ReserveAmount = 1000m;
    public const decimal ApprovedAmount = 800m;

    public static Claim NewClaim(Market market = Market.Singapore) => Claim.Submit(
        ClaimNumber.Create(2026, 1), Claimant, ClaimType.Motor, market, "POL-123",
        new DateOnly(2026, 10, 1), "Rear-ended at traffic lights", "+6590000000", 1500m, Now);

    /// <summary>A claim in the given status with its domain events cleared.</summary>
    public static Claim InStatus(ClaimStatus status)
    {
        var claim = NewClaim();
        switch (status)
        {
            case ClaimStatus.Submitted:
                break;
            case ClaimStatus.UnderReview:
                claim.Assign(Staff.UserId, Staff, Now);
                break;
            case ClaimStatus.InfoRequested:
                claim.Assign(Staff.UserId, Staff, Now);
                claim.RequestInformation("Please send photos", Staff, Now);
                break;
            case ClaimStatus.Approved:
                Approve(claim);
                break;
            case ClaimStatus.Rejected:
                claim.Assign(Staff.UserId, Staff, Now);
                claim.Reject("Not covered", Staff, Now);
                break;
            case ClaimStatus.Settled:
                Approve(claim);
                claim.Settle(ApprovedAmount, Staff, Now);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status));
        }

        claim.ClearDomainEvents();
        return claim;
    }

    /// <summary>Performs an action with valid arguments, so only the status decides whether it is allowed.</summary>
    public static void Perform(Claim claim, ClaimAction action)
    {
        var later = Now.AddDays(Claim.NoResponseDays + 1);
        switch (action)
        {
            case ClaimAction.Assign: claim.Assign(Staff.UserId, Staff, Now); break;
            case ClaimAction.RequestInformation: claim.RequestInformation("Question?", Staff, Now); break;
            case ClaimAction.RespondToInformation: claim.RespondToInformation("Answer", Claimant, Now); break;
            case ClaimAction.Approve: claim.Approve(1m, Staff, Now); break;
            case ClaimAction.Reject: claim.Reject("Reason", Staff, Now); break;
            case ClaimAction.RejectForNoResponse: claim.RejectForNoResponse(Staff, later); break;
            case ClaimAction.Settle: claim.Settle(1m, Staff, Now); break;
            case ClaimAction.SetReserve: claim.SetReserve(500m, Staff, Now); break;
            case ClaimAction.Reassign: claim.Reassign(Guid.NewGuid(), "Workload", Manager, Now); break;
            case ClaimAction.AddDocument: claim.AddDocument("photo.jpg", "image/jpeg", 1024, "ref-1", Claimant, Now); break;
            default: throw new ArgumentOutOfRangeException(nameof(action));
        }
    }

    private static void Approve(Claim claim)
    {
        claim.Assign(Staff.UserId, Staff, Now);
        claim.SetReserve(ReserveAmount, Staff, Now);
        claim.Approve(ApprovedAmount, Staff, Now);
    }
}
