using Claims.Domain.Claims;
using Claims.Domain.Common;
using FluentAssertions;
using static Claims.Domain.Tests.Claims.ClaimTestData;

namespace Claims.Domain.Tests.Claims;

/// <summary>
/// Every (action × status) pair, checked against an expected table written out independently of
/// ClaimTransitions (from docs/knowledge/domain.md), so a wrong production table cannot pass (AC-X-2).
/// </summary>
public sealed class TransitionMatrixTests
{
    private static readonly Dictionary<ClaimAction, ClaimStatus[]> Expected = new()
    {
        [ClaimAction.Assign] = [ClaimStatus.Submitted],
        [ClaimAction.RequestInformation] = [ClaimStatus.UnderReview],
        [ClaimAction.RespondToInformation] = [ClaimStatus.InfoRequested],
        [ClaimAction.Approve] = [ClaimStatus.UnderReview],
        [ClaimAction.Reject] = [ClaimStatus.UnderReview],
        [ClaimAction.RejectForNoResponse] = [ClaimStatus.InfoRequested],
        [ClaimAction.Settle] = [ClaimStatus.Approved],
        [ClaimAction.SetReserve] = [ClaimStatus.UnderReview],
        [ClaimAction.Reassign] = [ClaimStatus.UnderReview, ClaimStatus.InfoRequested, ClaimStatus.Approved],
        [ClaimAction.AddDocument] = [ClaimStatus.Submitted, ClaimStatus.UnderReview, ClaimStatus.InfoRequested, ClaimStatus.Approved],
    };

    private static IEnumerable<(ClaimAction Action, ClaimStatus Status)> AllPairs() =>
        from action in Expected.Keys
        from status in Enum.GetValues<ClaimStatus>()
        select (action, status);

    public static TheoryData<ClaimAction, ClaimStatus> DisallowedPairs()
    {
        var data = new TheoryData<ClaimAction, ClaimStatus>();
        foreach (var (action, status) in AllPairs().Where(p => !Expected[p.Action].Contains(p.Status)))
        {
            data.Add(action, status);
        }

        return data;
    }

    public static TheoryData<ClaimAction, ClaimStatus> AllowedPairs()
    {
        var data = new TheoryData<ClaimAction, ClaimStatus>();
        foreach (var (action, status) in AllPairs().Where(p => Expected[p.Action].Contains(p.Status)))
        {
            data.Add(action, status);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(DisallowedPairs))]
    public void Action_InDisallowedStatus_ThrowsInvalidTransitionAndChangesNothing(ClaimAction action, ClaimStatus status)
    {
        var claim = InStatus(status);
        var historyBefore = claim.History.Count;
        var outstandingBefore = claim.OutstandingReserve;
        var assigneeBefore = claim.AssignedToUserId;

        var act = () => Perform(claim, action);

        act.Should().Throw<InvalidTransitionException>();
        claim.Status.Should().Be(status);
        claim.History.Should().HaveCount(historyBefore);
        claim.DomainEvents.Should().BeEmpty();
        claim.OutstandingReserve.Should().Be(outstandingBefore);
        claim.AssignedToUserId.Should().Be(assigneeBefore);
    }

    [Theory]
    [MemberData(nameof(AllowedPairs))]
    public void Action_InAllowedStatus_IsNotRejectedAsInvalidTransition(ClaimAction action, ClaimStatus status)
    {
        var claim = InStatus(status);

        var act = () => Perform(claim, action);

        // Approve from a fresh UnderReview claim has no reserve: a business rule (422), not a transition error.
        act.Should().NotThrow<InvalidTransitionException>();
    }

    [Fact]
    public void ExpectedTable_CoversEveryActionExceptSubmit()
    {
        Enum.GetValues<ClaimAction>().Except([ClaimAction.Submit]).Should().BeEquivalentTo(Expected.Keys);
    }

    [Fact]
    public void ProductionTable_MatchesExpectedTable()
    {
        foreach (var (action, status) in AllPairs())
        {
            ClaimTransitions.IsAllowed(action, status).Should().Be(
                Expected[action].Contains(status), $"{action} in {status}");
        }
    }

    [Theory]
    [InlineData(ClaimStatus.Rejected)]
    [InlineData(ClaimStatus.Settled)]
    public void TerminalStatus_AllowsNoAction(ClaimStatus status)
    {
        Expected.Keys.Should().OnlyContain(action => !ClaimTransitions.IsAllowed(action, status));
        ClaimTransitions.IsTerminal(status).Should().BeTrue();
    }
}
