using System.Net;
using System.Net.Http.Json;
using Claims.Api.IntegrationTests.Infrastructure;
using Claims.Domain.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace Claims.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class HostingTests(ClaimsApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Health_Anonymous_Returns200()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UnknownRoute_Anonymous_Returns401ProblemDetails()
    {
        // The fallback policy covers unmatched URLs too, so anonymous callers can't probe which routes exist.
        var response = await _client.GetAsync("/api/v1/does-not-exist");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task UnknownRoute_Authenticated_Returns404ProblemDetails()
    {
        var client = await factory.CreateClientForRoleAsync(Roles.Claimant);

        var response = await client.GetAsync("/api/v1/does-not-exist");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Status.Should().Be(404);
    }
}
