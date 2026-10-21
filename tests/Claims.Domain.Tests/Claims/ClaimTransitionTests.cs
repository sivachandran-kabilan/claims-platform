using Claims.Domain.Claims;
using Claims.Domain.Claims.Events;
using Claims.Domain.Common;
using FluentAssertions;
using static Claims.Domain.Tests.Claims.ClaimTestData;

namespace Claims.Domain.Tests.Claims;

/// <summary>Each valid status change: new status, one history row, the right event (or none).</summary>
public sealed class ClaimTransitionTests
{
    private static void ShouldHaveRecorded(Claim claim, ClaimAction action, ClaimStatus? from, ClaimStatus? to, Actor actor)
    {
        var entry = claim.History.Last();
        entry.Action.Should().Be(action);
        entry.FromStatus.Should().Be(from);
        entry.ToStatus.Should().Be(to);
        entry.ActorUserId.Should().Be(actor.UserId);
        entry.ActorRole.Should().Be(actor.Role);
        entry.OccurredAt.Should().Be(Now);
    }

    [Fact]
    public void Assign_Submitted_MovesToUnderReviewAndSetsAssignee()
    {
        var claim = InStatus(ClaimStatus.Submitted);

        claim.Assign(Staff.UserId, Staff, Now);

        claim.Status.Should().Be(ClaimStatus.UnderReview);
        claim.AssignedToUserId.Should().Be(Staff.UserId);
        ShouldHaveRecorded(claim, ClaimAction.Assign, ClaimStatus.Submitted, ClaimStatus.UnderReview, Staff);
        claim.History.Last().TargetUserId.Should().Be(Staff.UserId);
        claim.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void RequestInformation_UnderReview_OpensRequestAndRaisesEvent()
    {
        var claim = InStatus(ClaimStatus.UnderReview);

        claim.RequestInformation("Please send photos", Staff, Now);

        claim.Status.Should().Be(ClaimStatus.InfoRequested);
        claim.OpenInformationRequest!.Question.Should().Be("Please send photos");
        ShouldHaveRecorded(claim, ClaimAction.RequestInformation, ClaimStatus.UnderReview, ClaimStatus.InfoRequested, Staff);
        claim.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<InformationRequested>()
            .Which.Status.Should().Be(ClaimStatus.InfoRequested);
    }

    [Fact]
    public void RespondToInformation_InfoRequested_ClosesRequestAndReturnsToUnderReview()
    {
        var claim = InStatus(ClaimStatus.InfoRequested);

        claim.RespondToInformation("Photos attached", Claimant, Now);

        claim.Status.Should().Be(ClaimStatus.UnderReview);
        claim.OpenInformationRequest.Should().BeNull();
        claim.InformationRequests.Single().Response.Should().Be("Photos attached");
        ShouldHaveRecorded(claim, ClaimAction.RespondToInformation, ClaimStatus.InfoRequested, ClaimStatus.UnderReview, Claimant);
        claim.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Approve_UnderReviewWithReserve_MovesToApprovedAndRaisesDecided()
    {
        var claim = InStatus(ClaimStatus.UnderReview);
        claim.SetReserve(ReserveAmount, Staff, Now);

        claim.Approve(ApprovedAmount, Manager, Now);

        claim.Status.Should().Be(ClaimStatus.Approved);
        claim.ApprovedAmount.Should().Be(ApprovedAmount);
        claim.OutstandingReserve.Should().Be(ApprovedAmount);
        claim.DecidedAt.Should().Be(Now);
        ShouldHaveRecorded(claim, ClaimAction.Approve, ClaimStatus.UnderReview, ClaimStatus.Approved, Manager);
        var decided = claim.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ClaimDecided>().Subject;
        decided.Outcome.Should().Be(DecisionOutcome.Approved);
        decided.ApprovedAmount.Should().Be(new Money(ApprovedAmount, "SGD"));
    }

    [Fact]
    public void Reject_UnderReview_MovesToRejectedWithReasonAndZeroOutstanding()
    {
        var claim = InStatus(ClaimStatus.UnderReview);
        claim.SetReserve(ReserveAmount, Staff, Now);

        claim.Reject("Not covered by policy", Staff, Now);

        claim.Status.Should().Be(ClaimStatus.Rejected);
        claim.RejectionReason.Should().Be("Not covered by policy");
        claim.OutstandingReserve.Should().Be(0);
        ShouldHaveRecorded(claim, ClaimAction.Reject, ClaimStatus.UnderReview, ClaimStatus.Rejected, Staff);
        claim.History.Last().Reason.Should().Be("Not covered by policy");
        claim.DomainEvents.OfType<ClaimDecided>().Single().Outcome.Should().Be(DecisionOutcome.Rejected);
    }

    [Fact]
    public void RejectForNoResponse_After15Days_MovesToRejectedWithFixedReason()
    {
        var claim = InStatus(ClaimStatus.InfoRequested);

        claim.RejectForNoResponse(Staff, Now.AddDays(Claim.NoResponseDays));

        claim.Status.Should().Be(ClaimStatus.Rejected);
        claim.RejectionReason.Should().Be(Claim.NoResponseReason);
        claim.OutstandingReserve.Should().Be(0);
        claim.History.Last().FromStatus.Should().Be(ClaimStatus.InfoRequested);
        claim.DomainEvents.OfType<ClaimDecided>().Single().Outcome.Should().Be(DecisionOutcome.RejectedNoResponse);
    }

    [Fact]
    public void Settle_Approved_MovesToSettledAndRaisesSettled()
    {
        var claim = InStatus(ClaimStatus.Approved);

        claim.Settle(ApprovedAmount, Staff, Now);

        claim.Status.Should().Be(ClaimStatus.Settled);
        claim.SettledAmount.Should().Be(ApprovedAmount);
        claim.SettledAt.Should().Be(Now);
        claim.OutstandingReserve.Should().Be(0);
        ShouldHaveRecorded(claim, ClaimAction.Settle, ClaimStatus.Approved, ClaimStatus.Settled, Staff);
        claim.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ClaimSettled>()
            .Which.SettledAmount.Should().Be(new Money(ApprovedAmount, "SGD"));
    }

    [Fact]
    public void OnlySettle_ReachesSettled()
    {
        // Settled appears as a target exactly once in the table: (Settle, Approved).
        var routesToSettled =
            from action in Enum.GetValues<ClaimAction>()
            from status in Enum.GetValues<ClaimStatus>()
            where ClaimTransitions.TryGetTarget(action, status, out var to) && to == ClaimStatus.Settled
            select (action, status);

        routesToSettled.Should().Equal((ClaimAction.Settle, ClaimStatus.Approved));
    }
}
