using System.Net;
using System.Net.Http.Json;
using Claims.Api.IntegrationTests.Infrastructure;
using Claims.Application.Claims.Common;
using Claims.Domain.Claims;
using Claims.Domain.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using static Claims.Api.IntegrationTests.Infrastructure.ClaimActions;

namespace Claims.Api.IntegrationTests.Claims;

/// <summary>HTTP side of the workflow: AC-CLM-3..8, 10..13 and AC-X-1.</summary>
[Collection(ApiCollection.Name)]
public sealed class WorkflowTests(ClaimsApiFactory factory)
{
    private Task<HttpClient> Manager() => factory.CreateClientForRoleAsync(Roles.ClaimManager);

    private Task<List<string>> OutboxTypesAsync(Guid claimId) =>
        factory.WithDbAsync(db => db.OutboxMessages.Where(m => m.ClaimId == claimId).Select(m => m.Type).ToListAsync());

    // ---- AC-CLM-3 Assign --------------------------------------------------------------------------

    [Fact]
    public async Task Assign_StaffSelf_Returns200UnderReviewAndNewETag()
    {
        var c = await factory.ArrangeClaimAsync(ClaimStatus.Submitted);
        var before = await c.Staff.GetETagAsync(c.Id);

        var response = await c.Staff.ActAsync(c.Id, "assign", ifMatch: before);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.ETag!.ToString().Should().NotBe(before);
        var claim = await response.Content.ReadFromJsonAsync<ClaimDetailResponse>(ApiJson.Options);
        claim!.Status.Should().Be(ClaimStatus.UnderReview);
        claim.AssignedToUserId.Should().Be(c.StaffUser.Id);
    }

    [Fact]
    public async Task Assign_StaffToOther_Returns403()
    {
        var c = await factory.ArrangeClaimAsync(ClaimStatus.Submitted);
        var other = await factory.CreateUserAsync(Roles.ClaimStaff);

        var response = await c.Staff.ActAsync(c.Id, "assign", new { assigneeUserId = other.Id });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Assign_ManagerToStaff_Returns200()
    {
        var c = await factory.ArrangeClaimAsync(ClaimStatus.Submitted);
        var manager = await Manager();

        var claim = await manager.ActOkAsync(c.Id, "assign", new { assigneeUserId = c.StaffUser.Id });

        claim.AssignedToUserId.Should().Be(c.StaffUser.Id);
    }

    [Fact]
    public async Task Assign_ToClaimantOrUnknownUser_Returns422()
    {
        var c = await factory.ArrangeClaimAsync(ClaimStatus.Submitted);
        var manager = await Manager();

        var toClaimant = await manager.ActAsync(c.Id, "assign", new { assigneeUserId = c.OwnerUser.Id });
        var toNobody = await manager.ActAsync(c.Id, "assign", new { assigneeUserId = Guid.NewGuid() });

        toClaimant.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        toNobody.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Assign_AlreadyUnderReview_Returns409()
    {
        var c = await factory.ArrangeClaimAsync(ClaimStatus.UnderReview);

        var response = await (await Manager()).ActAsync(c.Id, "assign");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ---- AC-CLM-4 / 5 Request info and respond ----------------------------------------------------

    [Fact]
    public async Task RequestInfo_AssignedStaff_ClaimantSeesOpenRequestAndEventWritten()
    {
        var c = await factory.ArrangeClaimAsync(ClaimStatus.UnderReview);

        await c.Staff.ActOkAsync(c.Id, "request-info", new { question = "Police report number?" });

        var view = await c.Owner.GetFromJsonAsync<ClaimDetailResponse>($"/api/v1/claims/{c.Id}", ApiJson.Options);
        view!.Status.Should().Be(ClaimStatus.InfoRequested);
        view.InformationRequests.Should().ContainSingle(r => r.Question == "Police report number?" && r.State == InformationRequestState.Open);
        (await OutboxTypesAsync(c.Id)).Should().Contain("InformationRequested");
    }

    [Fact]
    public async Task RequestInfo_OtherStaff_Returns403()
    {
        var c = await factory.ArrangeClaimAsync(ClaimStatus.UnderReview);
        var (otherStaff, _) = await factory.CreateNewClientAsync(Roles.ClaimStaff);

        var response = await otherStaff.ActAsync(c.Id, "request-info", new { question = "Mine now?" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RequestInfo_Manager_Returns200()
    {
        var c = await factory.ArrangeClaimAsync(ClaimStatus.UnderReview);

        var claim = await (await Manager()).ActOkAsync(c.Id, "request-info", new { question = "Photos?" });

        claim.Status.Should().Be(ClaimStatus.InfoRequested);
    }

    [Fact]
    public async Task Respond_Owner_ReturnsUnderReviewAndAnswered()
    {
        var c = await factory.ArrangeClaimAsync(ClaimStatus.InfoRequested);

        var claim = await c.Owner.ActOkAsync(c.Id, "information", new { response = "Estimate attached" });

        claim.Status.Should().Be(ClaimStatus.UnderReview);
        claim.InformationRequests.Single().State.Should().Be(InformationRequestState.Answered);
    }

    [Fact]
    public async Task Respond_OtherClaimant_Returns404()
    {
        var c = await factory.ArrangeClaimAsync(ClaimStatus.InfoRequested);
        var (intruder, _) = await factory.CreateNewClaimantClientAsync();

        var response = await intruder.ActAsync(c.Id, "information", new { response = "Hi" }, ifMatch: StaleETag);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Respond_NotInfoRequested_Returns409()
    {
        var c = await factory.ArrangeClaimAsync(ClaimStatus.UnderReview);

        var response = await c.Owner.ActAsync(c.Id, "information", new { response = "Unprompted" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ---- AC-CLM-10 / 6 Assess, approve, reject ----------------------------------------------------

    [Fact]
    public async Task Assess_SetsReserveAndOutstanding()
    {
        var c = await factory.ArrangeClaimAsync(ClaimStatus.UnderReview);

        var claim = await c.Staff.ActOkAsync(c.Id, "assess", new { reserve = 1234.56m });

        claim.Reserve.Should().Be(1234.56m);
        claim.OutstandingReserve.Should().Be(1234.56m);
        claim.Status.Should().Be(ClaimStatus.UnderReview);
    }

    [Fact]
    public async Task Assess_NotUnderReview_Returns409()
    {
        var c = await factory.ArrangeClaimAsync(ClaimStatus.InfoRequested);

        var response = await c.Staff.ActAsync(c.Id, "assess", new { reserve = 100m });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10.001)]
    public async Task Assess_InvalidAmount_Returns400(decimal reserve)
    {
        var c = await factory.ArrangeClaimAsync(ClaimStatus.UnderReview);

        var response = await c.Staff.ActAsync(c.Id, "assess", new { reserve });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Approve_WithoutReserve_Returns422()
    {
        var c = await factory.ArrangeClaimAsync(ClaimStatus.UnderReview);

        var response = await c.Staff.ActAsync(c.Id, "approve", new { approvedAmount = 100m });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Approve_AboveReserve_Returns422()
    {
        var c = await factory.ArrangeClaimAsync(ClaimStatus.UnderReview);
        await c.Staff.ActOkAsync(c.Id, "assess", new { reserve = 500m });

        var response = await c.Staff.ActAsync(c.Id, "approve", new { approvedAmount = 500.01m });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Approve_Valid_ApprovesAndWritesClaimDecided()
    {
        var c = await factory.ArrangeClaimAsync(ClaimStatus.Approved);

        var claim = await c.Staff.GetFromJsonAsync<ClaimDetailResponse>($"/api/v1/claims/{c.Id}", ApiJson.Options);

        claim!.Status.Should().Be(ClaimStatus.Approved);
        claim.ApprovedAmount.Should().Be(ApprovedAmount);
        claim.OutstandingReserve.Should().Be(ApprovedAmount);
        (await OutboxTypesAsync(c.Id)).Should().Contain("ClaimDecided");
    }

    [Fact]
    public async Task Reject_WithoutReason_Returns400()
    {
        var c = await factory.ArrangeClaimAsync(ClaimStatus.UnderReview);

        var response = await c.Staff.ActAsync(c.Id, "reject", new { reason = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Reject_Valid_ZeroOutstandingClaimantSeesReasonAndEventWritten()
    {
        var c = await factory.ArrangeClaimAsync(ClaimStatus.UnderReview);
        await c.Staff.ActOkAsync(c.Id, "assess", new { reserve = 700m });

        await c.Staff.ActOkAsync(c.Id, "reject", new { reason = "Policy lapsed" });

        var staffView = await c.Staff.GetFromJsonAsync<ClaimDetailResponse>($"/api/v1/claims/{c.Id}", ApiJson.Options);
        var ownerView = await c.Owner.GetFromJsonAsync<ClaimDetailResponse>($"/api/v1/claims/{c.Id}", ApiJson.Options);
        staffView!.OutstandingReserve.Should().Be(0);
        ownerView!.Status.Should().Be(ClaimStatus.Rejected);
        ownerView.RejectionReason.Should().Be("Policy lapsed");
        (await OutboxTypesAsync(c.Id)).Should().Contain("ClaimDecided");
    }

    // ---- AC-CLM-12 Reject for no response ---------------------------------------------------------

    [Fact]
    public async Task RejectNoResponse_Before15Days_Returns422()
    {
        var c = await factory.ArrangeClaimAsync(ClaimStatus.InfoRequested);

        var response = await c.Staff.ActAsync(c.Id, "reject-no-response");

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task RejectNoResponse_After15Days_Returns200WithFixedReason()
    {
        var (owner, _) = await factory.CreateNewClaimantClientAsync();
        var (staff, staffUser) = await factory.CreateNewClientAsync(Roles.ClaimStaff);
        var id = (await owner.SubmitOkAsync()).Id;
        // Backdate the request through the domain (16 days ago) instead of faking the server clock.
        var then = DateTimeOffset.UtcNow.AddDays(-16);
        var actor = new Actor(staffUser.Id, Roles.ClaimStaff);
        await factory.WithDbAsync(async db =>
        {
            var claim = await db.Claims.Include(x => x.InformationRequests).Include(x => x.History).SingleAsync(x => x.Id == id);
            claim.Assign(staffUser.Id, actor, then);
            claim.RequestInformation("Estimate?", actor, then);
            await db.SaveChangesAsync();
        });

        var result = await staff.ActOkAsync(id, "reject-no-response");

        result.Status.Should().Be(ClaimStatus.Rejected);
        result.RejectionReason.Should().Be(Claim.NoResponseReason);
        result.InformationRequests.Single().State.Should().Be(InformationRequestState.Unanswered);
        (await OutboxTypesAsync(id)).Should().Contain("ClaimDecided");
    }

    // ---- AC-CLM-7 Settle --------------------------------------------------------------------------

    [Fact]
    public async Task Settle_Valid_SettlesAndWritesClaimSettled()
    {
        var c = await factory.ArrangeClaimAsync(ClaimStatus.Settled);

        var claim = await c.Staff.GetFromJsonAsync<ClaimDetailResponse>($"/api/v1/claims/{c.Id}", ApiJson.Options);

        claim!.Status.Should().Be(ClaimStatus.Settled);
        claim.SettledAmount.Should().Be(ApprovedAmount);
        claim.OutstandingReserve.Should().Be(0);
        (await OutboxTypesAsync(c.Id)).Should().Contain("ClaimSettled");
    }

    [Fact]
    public async Task Settle_AboveApproved_Returns422()
    {
        var c = await factory.ArrangeClaimAsync(ClaimStatus.Approved);

        var response = await c.Staff.ActAsync(c.Id, "settle", new { amount = ApprovedAmount + 0.01m });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Settle_NotApproved_Returns409()
    {
        var c = await factory.ArrangeClaimAsync(ClaimStatus.UnderReview);

        var response = await c.Staff.ActAsync(c.Id, "settle", new { amount = 1m });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ---- AC-CLM-11 Reassign -----------------------------------------------------------------------

    [Fact]
    public async Task Reassign_ManagerWithReason_ChangesAssigneeNotStatus()
    {
        var c = await factory.ArrangeClaimAsync(ClaimStatus.InfoRequested);
        var newStaff = await factory.CreateUserAsync(Roles.ClaimStaff);

        var claim = await (await Manager()).ActOkAsync(c.Id, "reassign", new { assigneeUserId = newStaff.Id, reason = "Leave cover" });

        claim.AssignedToUserId.Should().Be(newStaff.Id);
        claim.Status.Should().Be(ClaimStatus.InfoRequested);
        claim.History.Last().Action.Should().Be(ClaimAction.Reassign);
    }

    [Fact]
    public async Task Reassign_WithoutReason_Returns400()
    {
        var c = await factory.ArrangeClaimAsync(ClaimStatus.UnderReview);
        var newStaff = await factory.CreateUserAsync(Roles.ClaimStaff);

        var response = await (await Manager()).ActAsync(c.Id, "reassign", new { assigneeUserId = newStaff.Id, reason = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Reassign_SameAssignee_Returns422()
    {
        var c = await factory.ArrangeClaimAsync(ClaimStatus.UnderReview);

        var response = await (await Manager()).ActAsync(c.Id, "reassign", new { assigneeUserId = c.StaffUser.Id, reason = "No-op" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Reassign_Submitted_Returns409()
    {
        var c = await factory.ArrangeClaimAsync(ClaimStatus.Submitted);
        var newStaff = await factory.CreateUserAsync(Roles.ClaimStaff);

        var response = await (await Manager()).ActAsync(c.Id, "reassign", new { assigneeUserId = newStaff.Id, reason = "Use assign" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ---- AC-CLM-13 Documents ----------------------------------------------------------------------

    [Fact]
    public async Task AddDocument_Owner_Returns201AndChangesETag()
    {
        var c = await factory.ArrangeClaimAsync(ClaimStatus.Submitted);
        var before = await c.Owner.GetETagAsync(c.Id);

        var response = await c.Owner.ActAsync(c.Id, "documents", Document(), ifMatch: before);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.ToString().Should().EndWith($"/api/v1/claims/{c.Id}");
        response.Headers.ETag!.ToString().Should().NotBe(before, "adding a child row must still change the claim's ETag");
        var claim = await response.Content.ReadFromJsonAsync<ClaimDetailResponse>(ApiJson.Options);
        claim!.Documents.Should().ContainSingle(d => d.FileName == "estimate.pdf");
    }

    [Fact]
    public async Task AddDocument_OtherClaimant_Returns404()
    {
        var c = await factory.ArrangeClaimAsync(ClaimStatus.Submitted);
        var (intruder, _) = await factory.CreateNewClaimantClientAsync();

        var response = await intruder.ActAsync(c.Id, "documents", Document(), ifMatch: StaleETag);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddDocument_UnassignedStaff_Returns403()
    {
        var c = await factory.ArrangeClaimAsync(ClaimStatus.UnderReview);
        var (otherStaff, _) = await factory.CreateNewClientAsync(Roles.ClaimStaff);

        var response = await otherStaff.ActAsync(c.Id, "documents", Document());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("application/x-msdownload", 2048)]
    [InlineData("application/pdf", 0)]
    [InlineData("application/pdf", 20 * 1024 * 1024 + 1)]
    public async Task AddDocument_BadContentTypeOrSize_Returns400(string contentType, long size)
    {
        var c = await factory.ArrangeClaimAsync(ClaimStatus.Submitted);

        var response = await c.Owner.ActAsync(c.Id, "documents", Document(contentType, size));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---- AC-CLM-8 Terminal + AC-X-1 Concurrency (every action) ------------------------------------

    public static TheoryData<string> AllActions() =>
    [
        "assign", "reassign", "request-info", "information", "assess",
        "approve", "reject", "reject-no-response", "settle", "documents",
    ];

    /// <summary>A body that passes validation, so only the check under test can fail.</summary>
    private static object ValidBody(string action, Guid assignee) => action switch
    {
        "assign" => new { assigneeUserId = assignee },
        "reassign" => new { assigneeUserId = assignee, reason = "Workload" },
        "request-info" => new { question = "More detail?" },
        "information" => new { response = "Here it is" },
        "assess" => new { reserve = 100m },
        "approve" => new { approvedAmount = 1m },
        "reject" => new { reason = "Not covered" },
        "settle" => new { amount = 1m },
        "documents" => Document(),
        _ => new { },
    };

    /// <summary>The caller who passes the role policy and the act rule: the owner for "information", else a manager.</summary>
    private async Task<(HttpClient Client, Guid Assignee)> ActorFor(string action, ArrangedClaim c)
    {
        var manager = await Manager();
        var newStaff = await factory.CreateUserAsync(Roles.ClaimStaff);
        return (action == "information" ? c.Owner : manager, newStaff.Id);
    }

    [Theory]
    [MemberData(nameof(AllActions))]
    public async Task Action_MissingIfMatch_Returns428(string action)
    {
        var c = await factory.ArrangeClaimAsync(ClaimStatus.Submitted);
        var (client, assignee) = await ActorFor(action, c);

        var response = await client.ActAsync(c.Id, action, ValidBody(action, assignee), ifMatch: null);

        response.StatusCode.Should().Be(HttpStatusCode.PreconditionRequired);
    }

    [Theory]
    [MemberData(nameof(AllActions))]
    public async Task Action_StaleIfMatch_Returns412(string action)
    {
        var c = await factory.ArrangeClaimAsync(ClaimStatus.Submitted);
        var (client, assignee) = await ActorFor(action, c);

        var response = await client.ActAsync(c.Id, action, ValidBody(action, assignee), ifMatch: StaleETag);

        response.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
    }

    [Theory]
    [MemberData(nameof(AllActions))]
    public async Task Action_OnTerminalClaim_Returns409(string action)
    {
        foreach (var status in new[] { ClaimStatus.Rejected, ClaimStatus.Settled })
        {
            var c = await factory.ArrangeClaimAsync(status);
            var (client, assignee) = await ActorFor(action, c);

            var response = await client.ActAsync(c.Id, action, ValidBody(action, assignee));

            response.StatusCode.Should().Be(HttpStatusCode.Conflict, $"{action} on {status}");
        }
    }

    [Fact]
    public async Task Approve_TwoRequestsWithSameETag_SecondReturns412()
    {
        var c = await factory.ArrangeClaimAsync(ClaimStatus.UnderReview);
        await c.Staff.ActOkAsync(c.Id, "assess", new { reserve = 500m });
        var etag = await c.Staff.GetETagAsync(c.Id);

        var first = await c.Staff.ActAsync(c.Id, "approve", new { approvedAmount = 400m }, ifMatch: etag);
        var second = await c.Staff.ActAsync(c.Id, "approve", new { approvedAmount = 450m }, ifMatch: etag);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
    }

    [Theory]
    [InlineData("W/\"AAAAAAAAAAA=\"")]
    [InlineData("*")]
    [InlineData("not-an-etag")]
    public async Task Action_MalformedIfMatch_Returns412(string ifMatch)
    {
        var c = await factory.ArrangeClaimAsync(ClaimStatus.Submitted);

        var response = await c.Staff.ActAsync(c.Id, "assign", ifMatch: ifMatch);

        response.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
    }
}
