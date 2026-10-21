using Claims.Domain.Claims;
using Claims.Domain.Claims.Events;
using Claims.Domain.Common;
using FluentAssertions;
using static Claims.Domain.Tests.Claims.ClaimTestData;

namespace Claims.Domain.Tests.Claims;

public sealed class ClaimLifecycleTests
{
    [Fact]
    public void FullPath_SubmitToSettled_TracksOutstandingAndHistory()
    {
        var claim = NewClaim();
        claim.OutstandingReserve.Should().Be(0);

        claim.Assign(Staff.UserId, Staff, Now);
        claim.RequestInformation("Police report?", Staff, Now);
        claim.RespondToInformation("Attached", Claimant, Now);
        claim.SetReserve(1200m, Staff, Now);
        claim.OutstandingReserve.Should().Be(1200m);
        claim.Approve(1000m, Staff, Now);
        claim.OutstandingReserve.Should().Be(1000m);
        claim.Settle(950m, Staff, Now);

        claim.Status.Should().Be(ClaimStatus.Settled);
        claim.OutstandingReserve.Should().Be(0);
        claim.History.Select(h => h.Action).Should().Equal(
            ClaimAction.Submit, ClaimAction.Assign, ClaimAction.RequestInformation, ClaimAction.RespondToInformation,
            ClaimAction.SetReserve, ClaimAction.Approve, ClaimAction.Settle);
        claim.DomainEvents.Select(e => e.GetType()).Should().Equal(
            typeof(ClaimSubmitted), typeof(InformationRequested), typeof(ClaimDecided), typeof(ClaimSettled));
    }

    [Fact]
    public void RejectPath_FromUnderReview_IsTerminal()
    {
        var claim = InStatus(ClaimStatus.UnderReview);

        claim.Reject("Fraud suspected", Staff, Now);

        ClaimTransitions.IsTerminal(claim.Status).Should().BeTrue();
    }
}

public sealed class ClaimMoneyRuleTests
{
    [Fact]
    public void Approve_WithoutReserve_ThrowsBusinessRule()
    {
        var claim = InStatus(ClaimStatus.UnderReview);

        var act = () => claim.Approve(100m, Staff, Now);

        act.Should().Throw<BusinessRuleException>().WithMessage("*reserve*");
        claim.Status.Should().Be(ClaimStatus.UnderReview);
    }

    [Theory]
    [InlineData(1000.01)]
    [InlineData(0)]
    [InlineData(-5)]
    public void Approve_AmountAboveReserveOrNotPositive_ThrowsBusinessRule(decimal amount)
    {
        var claim = InStatus(ClaimStatus.UnderReview);
        claim.SetReserve(ReserveAmount, Staff, Now);

        var act = () => claim.Approve(amount, Staff, Now);

        act.Should().Throw<BusinessRuleException>();
        claim.Status.Should().Be(ClaimStatus.UnderReview);
        claim.ApprovedAmount.Should().BeNull();
    }

    [Fact]
    public void Approve_AmountEqualToReserve_IsAllowed()
    {
        var claim = InStatus(ClaimStatus.UnderReview);
        claim.SetReserve(ReserveAmount, Staff, Now);

        claim.Approve(ReserveAmount, Staff, Now);

        claim.Status.Should().Be(ClaimStatus.Approved);
    }

    [Theory]
    [InlineData(800.01)]
    [InlineData(0)]
    public void Settle_AmountAboveApprovedOrNotPositive_ThrowsBusinessRule(decimal amount)
    {
        var claim = InStatus(ClaimStatus.Approved);

        var act = () => claim.Settle(amount, Staff, Now);

        act.Should().Throw<BusinessRuleException>();
        claim.Status.Should().Be(ClaimStatus.Approved);
        claim.OutstandingReserve.Should().Be(ApprovedAmount);
    }

    [Fact]
    public void Settle_AmountEqualToApproved_IsAllowed()
    {
        var claim = InStatus(ClaimStatus.Approved);

        claim.Settle(ApprovedAmount, Staff, Now);

        claim.Status.Should().Be(ClaimStatus.Settled);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10.005)]
    public void SetReserve_NotPositiveOrThreeDecimals_ThrowsBusinessRule(decimal amount)
    {
        var claim = InStatus(ClaimStatus.UnderReview);

        var act = () => claim.SetReserve(amount, Staff, Now);

        act.Should().Throw<BusinessRuleException>();
        claim.Reserve.Should().BeNull();
    }

    [Fact]
    public void SetReserve_Twice_KeepsLatestAndRecordsBoth()
    {
        var claim = InStatus(ClaimStatus.UnderReview);

        claim.SetReserve(500m, Staff, Now);
        claim.SetReserve(700m, Staff, Now);

        claim.Reserve.Should().Be(700m);
        claim.OutstandingReserve.Should().Be(700m);
        claim.Status.Should().Be(ClaimStatus.UnderReview);
        claim.History.Where(h => h.Action == ClaimAction.SetReserve).Select(h => h.Amount).Should().Equal(500m, 700m);
        claim.History.Last().FromStatus.Should().BeNull();
    }
}

public sealed class NoResponseRuleTests
{
    [Fact]
    public void RejectForNoResponse_OneMinuteBefore15Days_ThrowsBusinessRule()
    {
        var claim = InStatus(ClaimStatus.InfoRequested);

        var act = () => claim.RejectForNoResponse(Staff, Now.AddDays(Claim.NoResponseDays).AddMinutes(-1));

        act.Should().Throw<BusinessRuleException>();
        claim.Status.Should().Be(ClaimStatus.InfoRequested);
    }

    [Fact]
    public void RejectForNoResponse_Exactly15Days_IsAllowed()
    {
        var claim = InStatus(ClaimStatus.InfoRequested);

        claim.RejectForNoResponse(Staff, Now.AddDays(Claim.NoResponseDays));

        claim.Status.Should().Be(ClaimStatus.Rejected);
    }

    [Fact]
    public void Reject_FromInfoRequested_IsInvalidTransition()
    {
        // The bypass an edge-keyed table would allow: free-text reject before the 15 days are up.
        var claim = InStatus(ClaimStatus.InfoRequested);

        var act = () => claim.Reject("Impatient", Staff, Now);

        act.Should().Throw<InvalidTransitionException>();
    }

    [Fact]
    public void Assign_FromInfoRequested_IsInvalidTransition()
    {
        // The other edge-keyed bypass: skipping the claimant's response.
        var claim = InStatus(ClaimStatus.InfoRequested);

        var act = () => claim.Assign(Staff.UserId, Staff, Now);

        act.Should().Throw<InvalidTransitionException>();
    }
}

public sealed class ReassignTests
{
    [Fact]
    public void Reassign_WithReason_ChangesAssigneeNotStatus()
    {
        var claim = InStatus(ClaimStatus.InfoRequested);
        var newStaff = Guid.NewGuid();

        claim.Reassign(newStaff, "Original handler on leave", Manager, Now);

        claim.AssignedToUserId.Should().Be(newStaff);
        claim.Status.Should().Be(ClaimStatus.InfoRequested);
        var entry = claim.History.Last();
        entry.Action.Should().Be(ClaimAction.Reassign);
        entry.Reason.Should().Be("Original handler on leave");
        entry.TargetUserId.Should().Be(newStaff);
        entry.FromStatus.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Reassign_WithoutReason_ThrowsBusinessRule(string reason)
    {
        var claim = InStatus(ClaimStatus.UnderReview);

        var act = () => claim.Reassign(Guid.NewGuid(), reason, Manager, Now);

        act.Should().Throw<BusinessRuleException>();
        claim.AssignedToUserId.Should().Be(Staff.UserId);
    }

    [Fact]
    public void Reassign_ToCurrentAssignee_ThrowsBusinessRule()
    {
        var claim = InStatus(ClaimStatus.UnderReview);

        var act = () => claim.Reassign(Staff.UserId, "No change", Manager, Now);

        act.Should().Throw<BusinessRuleException>();
    }
}

public sealed class DocumentTests
{
    [Fact]
    public void AddDocument_ByClaimantWhileInfoRequested_LinksToOpenRequest()
    {
        var claim = InStatus(ClaimStatus.InfoRequested);

        var document = claim.AddDocument("photo.jpg", "image/jpeg", 2048, "ref-42", Claimant, Now);

        document.InformationRequestId.Should().Be(claim.OpenInformationRequest!.Id);
        claim.History.Last().Action.Should().Be(ClaimAction.AddDocument);
        claim.Status.Should().Be(ClaimStatus.InfoRequested);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(Claim.MaxDocumentBytes + 1)]
    public void AddDocument_InvalidSize_ThrowsBusinessRule(long size)
    {
        var claim = InStatus(ClaimStatus.Submitted);

        var act = () => claim.AddDocument("a.pdf", "application/pdf", size, "ref", Claimant, Now);

        act.Should().Throw<BusinessRuleException>();
        claim.Documents.Should().BeEmpty();
    }
}

public sealed class SubmitTests
{
    [Theory]
    [InlineData(Market.Singapore, "SGD")]
    [InlineData(Market.Australia, "AUD")]
    [InlineData(Market.HongKong, "HKD")]
    [InlineData(Market.Malaysia, "MYR")]
    [InlineData(Market.Thailand, "THB")]
    [InlineData(Market.Philippines, "PHP")]
    public void Submit_AnyMarket_TakesCurrencyFromMarket(Market market, string currency)
    {
        NewClaim(market).Currency.Should().Be(currency);
    }

    [Fact]
    public void Submit_ValidInput_StartsSubmittedWithHistoryAndEvent()
    {
        var claim = NewClaim();

        claim.Status.Should().Be(ClaimStatus.Submitted);
        claim.ClaimantUserId.Should().Be(Claimant.UserId);
        claim.AssignedToUserId.Should().BeNull();
        claim.Reserve.Should().BeNull();
        claim.History.Should().ContainSingle().Which.ToStatus.Should().Be(ClaimStatus.Submitted);
        claim.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ClaimSubmitted>()
            .Which.ClaimNumber.Should().Be("CLM-2026-000001");
    }

    [Fact]
    public void Submit_IncidentDateTwoDaysAhead_ThrowsBusinessRule()
    {
        var act = () => Claim.Submit(ClaimNumber.Create(2026, 2), Claimant, ClaimType.Property, Market.Australia,
            "POL-1", DateOnly.FromDateTime(Now.UtcDateTime).AddDays(2), "Flood", "+61400000000", 100m, Now);

        act.Should().Throw<BusinessRuleException>().WithMessage("*future*");
    }

    [Fact]
    public void Submit_IncidentDateTomorrowUtc_IsAllowedForMarketsAheadOfUtc()
    {
        var claim = Claim.Submit(ClaimNumber.Create(2026, 3), Claimant, ClaimType.Property, Market.Australia,
            "POL-1", DateOnly.FromDateTime(Now.UtcDateTime).AddDays(1), "Storm", "+61400000000", 100m, Now);

        claim.Status.Should().Be(ClaimStatus.Submitted);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(1.001)]
    public void Submit_InvalidClaimedAmount_ThrowsBusinessRule(decimal amount)
    {
        var act = () => Claim.Submit(ClaimNumber.Create(2026, 4), Claimant, ClaimType.Motor, Market.Thailand,
            "POL-1", new DateOnly(2026, 10, 1), "Scratch", "+66800000000", amount, Now);

        act.Should().Throw<BusinessRuleException>();
    }

    [Fact]
    public void Submit_ByStaff_ThrowsBusinessRule()
    {
        var act = () => Claim.Submit(ClaimNumber.Create(2026, 5), Staff, ClaimType.Motor, Market.Malaysia,
            "POL-1", new DateOnly(2026, 10, 1), "Dent", "+60100000000", 10m, Now);

        act.Should().Throw<BusinessRuleException>();
    }
}

public sealed class EventPayloadTests
{
    private static readonly string[] AllowedFields =
    [
        "EventId", "OccurredAt", "ClaimId", "ClaimNumber", "ClaimantUserId", "Market", "Type", "Status",
        "Outcome", "ApprovedAmount", "SettledAmount",
    ];

    [Theory]
    [InlineData(typeof(ClaimSubmitted))]
    [InlineData(typeof(InformationRequested))]
    [InlineData(typeof(ClaimDecided))]
    [InlineData(typeof(ClaimSettled))]
    public void Event_Fields_ContainNoPiiOrFreeText(Type eventType)
    {
        // AC-EVT-4: no description, contact phone, policy number, reasons or names.
        eventType.GetProperties().Select(p => p.Name).Should().BeSubsetOf(AllowedFields);
    }
}

public sealed class ValueObjectTests
{
    [Fact]
    public void ClaimNumber_Create_FormatsYearAndPaddedSequence()
    {
        ClaimNumber.Create(2026, 123).Value.Should().Be("CLM-2026-000123");
    }

    [Theory]
    [InlineData("sgd")]
    [InlineData("SG")]
    [InlineData("SGDX")]
    public void Money_InvalidCurrency_Throws(string currency)
    {
        var act = () => new Money(10m, currency);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Money_ThreeDecimals_ThrowsBusinessRule()
    {
        var act = () => new Money(1.234m, "SGD");

        act.Should().Throw<BusinessRuleException>();
    }

    [Fact]
    public void Actor_UnknownRole_Throws()
    {
        var act = () => new Actor(Guid.NewGuid(), "Admin");

        act.Should().Throw<ArgumentException>();
    }
}
