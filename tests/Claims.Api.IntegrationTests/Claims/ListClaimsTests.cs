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

/// <summary>AC-CLM-9: paged and filtered lists; the unassigned queue. The DB is shared, so assertions check filters hold.</summary>
[Collection(ApiCollection.Name)]
public sealed class ListClaimsTests(ClaimsApiFactory factory)
{
    private async Task<PagedResult<ClaimSummaryResponse>> ListAsync(string query)
    {
        var staff = await factory.CreateClientForRoleAsync(Roles.ClaimStaff);
        return (await staff.GetFromJsonAsync<PagedResult<ClaimSummaryResponse>>($"/api/v1/claims{query}", ApiJson.Options))!;
    }

    [Fact]
    public async Task ListClaims_FilterByMarketAndType_ReturnsOnlyMatching()
    {
        var (client, _) = await factory.CreateNewClaimantClientAsync();
        var target = await client.SubmitOkAsync(ClaimRequests.ValidBody(market: "Philippines", type: "Property"));
        await client.SubmitOkAsync(ClaimRequests.ValidBody(market: "Philippines", type: "Motor"));

        var page = await ListAsync("?market=Philippines&type=Property&pageSize=100");

        page.Items.Should().Contain(c => c.Id == target.Id);
        page.Items.Should().OnlyContain(c => c.Market == Market.Philippines && c.Type == ClaimType.Property);
    }

    [Fact]
    public async Task ListClaims_FilterByStatusAndAssignee_ReturnsOnlyMatching()
    {
        var (client, _) = await factory.CreateNewClaimantClientAsync();
        var claim = await client.SubmitOkAsync();
        var staff = await factory.CreateUserAsync(Roles.ClaimStaff);
        await factory.WithDbAsync(async db =>
        {
            var tracked = await db.Claims.Include(c => c.History).SingleAsync(c => c.Id == claim.Id);
            tracked.Assign(staff.Id, new Actor(staff.Id, Roles.ClaimStaff), DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        });

        var assigned = await ListAsync($"?assignedTo={staff.Id}&status=UnderReview");
        var unassigned = await ListAsync("?unassigned=true&pageSize=100");

        assigned.Items.Should().ContainSingle().Which.Id.Should().Be(claim.Id);
        unassigned.Items.Should().NotContain(c => c.Id == claim.Id);
        unassigned.Items.Should().OnlyContain(c => c.AssignedToUserId == null);
    }

    [Fact]
    public async Task ListClaims_Paging_ReturnsRequestedPageAndTotal()
    {
        var (client, _) = await factory.CreateNewClaimantClientAsync();
        await client.SubmitOkAsync();
        await client.SubmitOkAsync();

        var page = await ListAsync("?page=2&pageSize=1");

        page.Page.Should().Be(2);
        page.PageSize.Should().Be(1);
        page.Items.Should().HaveCount(1);
        page.TotalCount.Should().BeGreaterThanOrEqualTo(2);
    }

    [Theory]
    [InlineData("?pageSize=101")]
    [InlineData("?page=0")]
    [InlineData("?sort=amount")]
    [InlineData("?unassigned=true&assignedTo=6f1e3c2a-0b7d-4c5e-9a10-000000000001")]
    [InlineData("?status=Lost")]
    public async Task ListClaims_InvalidQuery_Returns400(string query)
    {
        var staff = await factory.CreateClientForRoleAsync(Roles.ClaimStaff);

        var response = await staff.GetAsync($"/api/v1/claims{query}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ListClaims_SortOldestFirst_IsOrderedBySubmittedAt()
    {
        var page = await ListAsync("?sort=submittedAt&pageSize=100");

        page.Items.Select(c => c.SubmittedAt).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task Queue_ReturnsOnlySubmittedUnassignedOldestFirst()
    {
        var (client, _) = await factory.CreateNewClaimantClientAsync();
        await client.SubmitOkAsync();
        var staff = await factory.CreateClientForRoleAsync(Roles.ClaimStaff);

        var queue = await staff.GetFromJsonAsync<PagedResult<ClaimSummaryResponse>>("/api/v1/claims/queue?pageSize=100", ApiJson.Options);

        queue!.Items.Should().NotBeEmpty();
        queue.Items.Should().OnlyContain(c => c.Status == ClaimStatus.Submitted && c.AssignedToUserId == null);
        queue.Items.Select(c => c.SubmittedAt).Should().BeInAscendingOrder();
    }
}
