using System.Net;
using System.Net.Http.Json;
using Claims.Api.IntegrationTests.Infrastructure;
using Claims.Application.Common.Paging;
using Claims.Application.Identity;
using Claims.Domain.Common;
using FluentAssertions;

namespace Claims.Api.IntegrationTests.Identity;

[Collection(ApiCollection.Name)]
public sealed class UsersTests(ClaimsApiFactory factory)
{
    private static object NewStaffBody(string role = Roles.ClaimStaff) => new
    {
        email = $"staff-{Guid.NewGuid():N}@test.local",
        password = "a-long-enough-pass",
        displayName = "New Staff",
        role,
    };

    // AC-AUTH-4 -----------------------------------------------------------------

    [Theory]
    [InlineData(Roles.ClaimStaff)]
    [InlineData(Roles.ClaimManager)]
    public async Task CreateUser_AsManager_Returns201WithRequestedRole(string role)
    {
        var manager = await factory.CreateClientForRoleAsync(Roles.ClaimManager);

        var response = await manager.PostAsJsonAsync("/api/v1/users", NewStaffBody(role));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await response.Content.ReadFromJsonAsync<UserResponse>())!.Role.Should().Be(role);
    }

    [Theory]
    [InlineData(Roles.ClaimStaff)]
    [InlineData(Roles.Claimant)]
    public async Task CreateUser_AsNonManager_Returns403(string callerRole)
    {
        var caller = await factory.CreateClientForRoleAsync(callerRole);

        var response = await caller.PostAsJsonAsync("/api/v1/users", NewStaffBody());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateUser_ClaimantRole_Returns400()
    {
        var manager = await factory.CreateClientForRoleAsync(Roles.ClaimManager);

        var response = await manager.PostAsJsonAsync("/api/v1/users", NewStaffBody(Roles.Claimant));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // AC-USR-2 ------------------------------------------------------------------

    [Fact]
    public async Task ListUsers_AsManager_ReturnsRequestedPage()
    {
        var manager = await factory.CreateClientForRoleAsync(Roles.ClaimManager);
        await factory.CreateUserAsync(Roles.ClaimStaff);
        await factory.CreateUserAsync(Roles.ClaimStaff);

        var page = await manager.GetFromJsonAsync<PagedResult<UserResponse>>("/api/v1/users?page=1&pageSize=1&role=ClaimStaff");

        page!.Items.Should().ContainSingle().Which.Role.Should().Be(Roles.ClaimStaff);
        page.PageSize.Should().Be(1);
        page.TotalCount.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task ListUsers_PageSizeOver100_Returns400()
    {
        var manager = await factory.CreateClientForRoleAsync(Roles.ClaimManager);

        var response = await manager.GetAsync("/api/v1/users?pageSize=101");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
