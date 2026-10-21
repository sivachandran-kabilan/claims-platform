using System.Net;
using System.Net.Http.Json;
using Claims.Api.IntegrationTests.Infrastructure;
using Claims.Domain.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Claims.Api.IntegrationTests;

public enum Caller { Anonymous, Claimant, ClaimStaff, ClaimManager }

/// <summary>
/// The endpoint role matrix from CLAUDE.md as data. Each row lists the callers that pass authorization.
/// Denied callers must get exactly 401 (anonymous) or 403 (authenticated); allowed callers must get neither.
/// Resource-level rules ("own only", "assigned") are tested in the feature tests, not here.
/// </summary>
public static class RoleMatrix
{
    public sealed record Row(string Method, string Route, string Url, Caller[] Allowed)
    {
        public string Name => $"{Method} /{Route}";
    }

    private static readonly Caller[] Everyone = [Caller.Anonymous, Caller.Claimant, Caller.ClaimStaff, Caller.ClaimManager];
    private static readonly Caller[] AnyUser = [Caller.Claimant, Caller.ClaimStaff, Caller.ClaimManager];
    private static readonly Caller[] ManagerOnly = [Caller.ClaimManager];
    private static readonly Caller[] ClaimantOnly = [Caller.Claimant];
    private static readonly Caller[] StaffAndManager = [Caller.ClaimStaff, Caller.ClaimManager];
    private static readonly string AnyClaimId = Guid.NewGuid().ToString();

    // Route = the endpoint's route template (used to prove every endpoint has a row). Url = a concrete URL to call.
    public static readonly Row[] Rows =
    [
        new("GET", "health", "/health", Everyone),
        new("POST", "api/v1/auth/register", "/api/v1/auth/register", Everyone),
        new("POST", "api/v1/auth/login", "/api/v1/auth/login", Everyone),
        new("GET", "api/v1/auth/me", "/api/v1/auth/me", AnyUser),
        new("POST", "api/v1/users", "/api/v1/users", ManagerOnly),
        new("GET", "api/v1/users", "/api/v1/users", ManagerOnly),

        // Claims. GET by id: every role passes authorization; ownership (404) is tested in ViewClaimTests.
        new("POST", "api/v1/claims", "/api/v1/claims", ClaimantOnly),
        new("GET", "api/v1/claims/mine", "/api/v1/claims/mine", ClaimantOnly),
        new("GET", "api/v1/claims/{id:guid}", $"/api/v1/claims/{AnyClaimId}", AnyUser),
        new("GET", "api/v1/claims", "/api/v1/claims", StaffAndManager),
        new("GET", "api/v1/claims/queue", "/api/v1/claims/queue", StaffAndManager),

        // Workflow actions. "Assigned"/"own"/"self" rules are resource checks, tested in WorkflowTests.
        Action("assign", StaffAndManager),
        Action("reassign", ManagerOnly),
        Action("request-info", StaffAndManager),
        Action("information", ClaimantOnly),
        Action("assess", StaffAndManager),
        Action("approve", StaffAndManager),
        Action("reject", StaffAndManager),
        Action("reject-no-response", StaffAndManager),
        Action("settle", StaffAndManager),
        Action("documents", AnyUser),

        new("GET", "api/v1/notifications/mine", "/api/v1/notifications/mine", ClaimantOnly),
    ];

    private static Row Action(string action, Caller[] allowed) =>
        new("POST", $"api/v1/claims/{{id:guid}}/{action}", $"/api/v1/claims/{AnyClaimId}/{action}", allowed);

    public static TheoryData<string, Caller> Cases()
    {
        var data = new TheoryData<string, Caller>();
        foreach (var row in Rows)
        {
            foreach (var caller in Everyone)
            {
                data.Add(row.Name, caller);
            }
        }

        return data;
    }
}

[Collection(ApiCollection.Name)]
public sealed class RoleMatrixTests(ClaimsApiFactory factory)
{
    [Theory]
    [MemberData(nameof(RoleMatrix.Cases), MemberType = typeof(RoleMatrix))]
    public async Task Endpoint_CalledByRole_MatchesRoleMatrix(string endpoint, Caller caller)
    {
        var row = RoleMatrix.Rows.Single(r => r.Name == endpoint);
        var client = caller == Caller.Anonymous
            ? factory.CreateClient()
            : await factory.CreateClientForRoleAsync(caller.ToString());

        // Empty JSON body: allowed callers then fail validation (400), which still proves they passed authorization.
        using var request = new HttpRequestMessage(new HttpMethod(row.Method), row.Url);
        if (row.Method != "GET")
        {
            request.Content = JsonContent.Create(new { });
        }

        var response = await client.SendAsync(request);

        if (row.Allowed.Contains(caller))
        {
            response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized, $"{caller} is allowed on {endpoint}")
                .And.NotBe(HttpStatusCode.Forbidden, $"{caller} is allowed on {endpoint}");
        }
        else
        {
            var expected = caller == Caller.Anonymous ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden;
            response.StatusCode.Should().Be(expected, $"{caller} is not allowed on {endpoint}");
        }
    }

    [Fact]
    public void EveryEndpoint_HasMatrixRow()
    {
        _ = factory.Server; // make sure the host (and its endpoints) is built
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .SelectMany(e => (e.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? ["GET"])
                .Select(method => $"{method} /{e.RoutePattern.RawText!.Trim('/')}"))
            .ToList();

        endpoints.Should().NotBeEmpty();
        endpoints.Should().BeSubsetOf(RoleMatrix.Rows.Select(r => r.Name),
            "every endpoint needs a row in RoleMatrix (CLAUDE.md role matrix)");
    }

    [Fact]
    public void RoleMatrix_CallerNames_MatchRoleConstants()
    {
        Enum.GetNames<Caller>().Except([nameof(Caller.Anonymous)]).Should().BeEquivalentTo(Roles.All);
    }
}
