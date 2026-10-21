using System.Net;
using System.Net.Http.Json;
using Claims.Api.IntegrationTests.Infrastructure;
using Claims.Application.Claims.Common;
using Claims.Application.Common.Paging;
using Claims.Domain.Claims;
using Claims.Domain.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Claims.Api.IntegrationTests.Claims;

/// <summary>AC-CLM-2: claimant isolation, staff visibility, ETag, claimant view hides internal data.</summary>
[Collection(ApiCollection.Name)]
public sealed class ViewClaimTests(ClaimsApiFactory factory)
{
    /// <summary>Submitted claim moved to UnderReview with a reserve, directly through the domain + DbContext.</summary>
    private async Task<ClaimDetailResponse> ClaimWithReserveAsync(HttpClient owner)
    {
        var claim = await owner.SubmitOkAsync();
        var staff = await factory.CreateUserAsync(Roles.ClaimStaff);
        var actor = new Actor(staff.Id, Roles.ClaimStaff);
        await factory.WithDbAsync(async db =>
        {
            var tracked = await db.Claims.Include(c => c.History).SingleAsync(c => c.Id == claim.Id);
            tracked.Assign(staff.Id, actor, DateTimeOffset.UtcNow);
            tracked.SetReserve(1200m, actor, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        });
        return claim;
    }

    [Fact]
    public async Task GetClaim_Owner_Returns200WithETagAndClaimantView()
    {
        var (owner, _) = await factory.CreateNewClaimantClientAsync();
        var claim = await ClaimWithReserveAsync(owner);

        var response = await owner.GetAsync($"/api/v1/claims/{claim.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.ETag.Should().NotBeNull();
        var view = (await response.Content.ReadFromJsonAsync<ClaimDetailResponse>(ApiJson.Options))!;
        view.Status.Should().Be(ClaimStatus.UnderReview);
        view.Reserve.Should().BeNull();
        view.OutstandingReserve.Should().BeNull();
        view.AssignedToUserId.Should().BeNull();
        view.History.Select(h => h.Action).Should().Equal(ClaimAction.Submit, ClaimAction.Assign);
        view.History.Should().OnlyContain(h => h.ActorUserId == null && h.TargetUserId == null);
        view.History.Last().ActorRole.Should().Be(Roles.ClaimStaff);
    }

    [Theory]
    [InlineData(Roles.ClaimStaff)]
    [InlineData(Roles.ClaimManager)]
    public async Task GetClaim_StaffAndManager_Return200WithInternalFields(string role)
    {
        var (owner, _) = await factory.CreateNewClaimantClientAsync();
        var claim = await ClaimWithReserveAsync(owner);
        var reader = await factory.CreateClientForRoleAsync(role);

        var view = await reader.GetFromJsonAsync<ClaimDetailResponse>($"/api/v1/claims/{claim.Id}", ApiJson.Options);

        view!.Reserve.Should().Be(1200m);
        view.OutstandingReserve.Should().Be(1200m);
        view.AssignedToUserId.Should().NotBeNull();
        view.History.Select(h => h.Action).Should().Equal(ClaimAction.Submit, ClaimAction.Assign, ClaimAction.SetReserve);
        view.History.Should().OnlyContain(h => h.ActorUserId != null);
    }

    [Fact]
    public async Task GetClaim_OtherClaimant_Returns404()
    {
        var (owner, _) = await factory.CreateNewClaimantClientAsync();
        var (intruder, _) = await factory.CreateNewClaimantClientAsync();
        var claim = await owner.SubmitOkAsync();

        var response = await intruder.GetAsync($"/api/v1/claims/{claim.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetClaim_UnknownId_Returns404()
    {
        var staff = await factory.CreateClientForRoleAsync(Roles.ClaimStaff);

        var response = await staff.GetAsync($"/api/v1/claims/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListMine_ReturnsOnlyOwnClaimsWithoutInternalFields()
    {
        var (alice, _) = await factory.CreateNewClaimantClientAsync();
        var (bob, _) = await factory.CreateNewClaimantClientAsync();
        var mine1 = await alice.SubmitOkAsync();
        var mine2 = await ClaimWithReserveAsync(alice);
        await bob.SubmitOkAsync();

        var page = await alice.GetFromJsonAsync<PagedResult<ClaimSummaryResponse>>("/api/v1/claims/mine", ApiJson.Options);

        page!.Items.Select(c => c.Id).Should().BeEquivalentTo([mine1.Id, mine2.Id]);
        page.TotalCount.Should().Be(2);
        page.Items.Should().OnlyContain(c => c.OutstandingReserve == null && c.AssignedToUserId == null);
    }
}
