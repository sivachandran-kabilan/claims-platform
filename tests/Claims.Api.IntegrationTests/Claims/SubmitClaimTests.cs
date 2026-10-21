using System.Net;
using System.Net.Http.Json;
using Claims.Api.IntegrationTests.Infrastructure;
using Claims.Application.Claims.Common;
using Claims.Domain.Claims;
using Claims.Domain.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Claims.Api.IntegrationTests.Claims;

/// <summary>AC-CLM-1 and AC-EVT-1 (outbox row in the same transaction).</summary>
[Collection(ApiCollection.Name)]
public sealed class SubmitClaimTests(ClaimsApiFactory factory)
{
    [Fact]
    public async Task SubmitClaim_ValidRequest_Returns201WithLocationNumberAndETag()
    {
        var (client, user) = await factory.CreateNewClaimantClientAsync();

        var response = await client.SubmitAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.ETag.Should().NotBeNull();
        var claim = (await response.Content.ReadFromJsonAsync<ClaimDetailResponse>(ApiJson.Options))!;
        claim.Number.Should().MatchRegex(@"^CLM-\d{4}-\d{6}$");
        claim.Status.Should().Be(ClaimStatus.Submitted);
        claim.Currency.Should().Be("SGD");
        claim.ClaimedAmount.Should().Be(1500.50m);
        claim.History.Should().ContainSingle().Which.ToStatus.Should().Be(ClaimStatus.Submitted);
        response.Headers.Location!.ToString().Should().EndWith($"/api/v1/claims/{claim.Id}");

        var stored = await factory.WithDbAsync(db => db.Claims.AsNoTracking().SingleAsync(c => c.Id == claim.Id));
        stored.ClaimantUserId.Should().Be(user.Id, "the owner comes from the token");
    }

    [Fact]
    public async Task SubmitClaim_WritesClaimSubmittedOutboxRowInSameTransaction()
    {
        var (client, _) = await factory.CreateNewClaimantClientAsync();

        var claim = await client.SubmitOkAsync();

        var outbox = await factory.WithDbAsync(db => db.OutboxMessages.AsNoTracking().Where(m => m.ClaimId == claim.Id).ToListAsync());
        var message = outbox.Should().ContainSingle().Subject;
        message.Type.Should().Be("ClaimSubmitted");
        message.ProcessedAt.Should().BeNull();
        message.Payload.Should().Contain(claim.Number);
    }

    [Fact]
    public async Task OutboxPayload_ContainsNoPii()
    {
        var (client, _) = await factory.CreateNewClaimantClientAsync();
        var claim = await client.SubmitOkAsync();

        var payload = await factory.WithDbAsync(db => db.OutboxMessages.Where(m => m.ClaimId == claim.Id).Select(m => m.Payload).SingleAsync());

        payload.Should().NotContainAny("Rear-ended", "+6591234567", "POL-2026-001", "@");
    }

    [Fact]
    public async Task SubmitClaim_BodyWithCurrency_UsesMarketCurrency()
    {
        var (client, _) = await factory.CreateNewClaimantClientAsync();
        var body = ClaimRequests.ValidBody(market: "Thailand");
        body["currency"] = "USD";

        var claim = await client.SubmitOkAsync(body);

        claim.Currency.Should().Be("THB");
    }

    [Fact]
    public async Task SubmitClaim_SameKeySameBody_ReturnsSameClaimWithoutDuplicate()
    {
        var (client, user) = await factory.CreateNewClaimantClientAsync();
        var key = Guid.NewGuid().ToString();

        var first = await client.SubmitAsync(idempotencyKey: key);
        var second = await client.SubmitAsync(idempotencyKey: key);

        second.StatusCode.Should().Be(HttpStatusCode.Created);
        var a = await first.Content.ReadFromJsonAsync<ClaimDetailResponse>(ApiJson.Options);
        var b = await second.Content.ReadFromJsonAsync<ClaimDetailResponse>(ApiJson.Options);
        b!.Id.Should().Be(a!.Id);
        var (claims, events) = await factory.WithDbAsync(async db => (
            await db.Claims.CountAsync(c => c.ClaimantUserId == user.Id),
            await db.OutboxMessages.CountAsync(m => m.ClaimId == a.Id)));
        claims.Should().Be(1);
        events.Should().Be(1);
    }

    [Fact]
    public async Task SubmitClaim_SameKeyDifferentBody_Returns422()
    {
        var (client, _) = await factory.CreateNewClaimantClientAsync();
        var key = Guid.NewGuid().ToString();
        (await client.SubmitAsync(idempotencyKey: key)).StatusCode.Should().Be(HttpStatusCode.Created);
        var changed = ClaimRequests.ValidBody();
        changed["claimedAmount"] = 99m;

        var response = await client.SubmitAsync(changed, idempotencyKey: key);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task SubmitClaim_SameKeyDifferentUsers_CreatesSeparateClaims()
    {
        var (alice, _) = await factory.CreateNewClaimantClientAsync();
        var (bob, _) = await factory.CreateNewClaimantClientAsync();
        var key = Guid.NewGuid().ToString();

        var a = await (await alice.SubmitAsync(idempotencyKey: key)).Content.ReadFromJsonAsync<ClaimDetailResponse>(ApiJson.Options);
        var b = await (await bob.SubmitAsync(idempotencyKey: key)).Content.ReadFromJsonAsync<ClaimDetailResponse>(ApiJson.Options);

        b!.Id.Should().NotBe(a!.Id);
    }

    [Fact]
    public async Task SubmitClaim_MissingIdempotencyKey_Returns400()
    {
        var client = await factory.CreateClientForRoleAsync(Roles.Claimant);

        var response = await client.SubmitAsync(idempotencyKey: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem!.Errors.Keys.Should().Contain("IdempotencyKey");
    }

    [Theory]
    [InlineData("incidentDate", "2099-01-01", "IncidentDate")]
    [InlineData("claimedAmount", 0, "ClaimedAmount")]
    [InlineData("claimedAmount", 10.005, "ClaimedAmount")]
    [InlineData("contactPhone", "call me", "ContactPhone")]
    [InlineData("policyNumber", "", "PolicyNumber")]
    public async Task SubmitClaim_InvalidField_Returns400WithFieldError(string field, object value, string expectedKey)
    {
        var client = await factory.CreateClientForRoleAsync(Roles.Claimant);
        var body = ClaimRequests.ValidBody();
        body[field] = value;

        var response = await client.SubmitAsync(body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem!.Errors.Keys.Should().Contain(k => k.Contains(expectedKey, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SubmitClaim_UnknownMarket_Returns400()
    {
        var client = await factory.CreateClientForRoleAsync(Roles.Claimant);

        var response = await client.SubmitAsync(ClaimRequests.ValidBody(market: "Mars"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ClaimNumbers_AreUniqueAndIncreasing()
    {
        var (client, _) = await factory.CreateNewClaimantClientAsync();

        var first = await client.SubmitOkAsync();
        var second = await client.SubmitOkAsync();

        second.Number.Should().NotBe(first.Number);
        string.CompareOrdinal(second.Number, first.Number).Should().BePositive();
    }
}
