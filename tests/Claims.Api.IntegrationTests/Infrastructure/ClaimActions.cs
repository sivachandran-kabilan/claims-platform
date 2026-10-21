using System.Net.Http.Json;
using Claims.Application.Claims.Common;
using Claims.Domain.Claims;
using Claims.Domain.Common;

namespace Claims.Api.IntegrationTests.Infrastructure;

/// <summary>A claim driven into a status through the real endpoints, with the people involved.</summary>
public sealed record ArrangedClaim(Guid Id, HttpClient Owner, TestUser OwnerUser, HttpClient Staff, TestUser StaffUser);

public static class ClaimActions
{
    /// <summary>Pass as ifMatch to fetch the current ETag with the acting client first.</summary>
    public const string CurrentETag = "<current>";

    /// <summary>A well-formed ETag that never matches a real rowversion.</summary>
    public const string StaleETag = "\"AAAAAAAAAAA=\"";

    public const decimal Reserve = 1000m;
    public const decimal ApprovedAmount = 800m;

    public static async Task<string> GetETagAsync(this HttpClient client, Guid claimId)
    {
        var response = await client.GetAsync($"/api/v1/claims/{claimId}");
        response.EnsureSuccessStatusCode();
        return response.Headers.ETag!.ToString();
    }

    public static async Task<HttpResponseMessage> ActAsync(
        this HttpClient client, Guid claimId, string action, object? body = null, string? ifMatch = CurrentETag)
    {
        var etag = ifMatch == CurrentETag ? await client.GetETagAsync(claimId) : ifMatch;
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/claims/{claimId}/{action}")
        {
            Content = JsonContent.Create(body ?? new { }),
        };
        if (etag is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", etag);
        }

        return await client.SendAsync(request);
    }

    public static async Task<ClaimDetailResponse> ActOkAsync(
        this HttpClient client, Guid claimId, string action, object? body = null)
    {
        var response = await client.ActAsync(claimId, action, body);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"{action} returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }

        return (await response.Content.ReadFromJsonAsync<ClaimDetailResponse>(ApiJson.Options))!;
    }

    public static object Document(string contentType = "application/pdf", long sizeBytes = 2048) =>
        new { fileName = "estimate.pdf", contentType, sizeBytes, storageRef = "blob://estimate-1" };

    public static async Task<ArrangedClaim> ArrangeClaimAsync(this ClaimsApiFactory factory, ClaimStatus status)
    {
        var (owner, ownerUser) = await factory.CreateNewClientAsync(Roles.Claimant);
        var (staff, staffUser) = await factory.CreateNewClientAsync(Roles.ClaimStaff);
        var id = (await owner.SubmitOkAsync()).Id;

        if (status != ClaimStatus.Submitted)
        {
            await staff.ActOkAsync(id, "assign");
        }

        switch (status)
        {
            case ClaimStatus.InfoRequested:
                await staff.ActOkAsync(id, "request-info", new { question = "Please send the repair estimate" });
                break;
            case ClaimStatus.Approved or ClaimStatus.Settled:
                await staff.ActOkAsync(id, "assess", new { reserve = Reserve });
                await staff.ActOkAsync(id, "approve", new { approvedAmount = ApprovedAmount });
                if (status == ClaimStatus.Settled)
                {
                    await staff.ActOkAsync(id, "settle", new { amount = ApprovedAmount });
                }

                break;
            case ClaimStatus.Rejected:
                await staff.ActOkAsync(id, "reject", new { reason = "Not covered" });
                break;
        }

        return new ArrangedClaim(id, owner, ownerUser, staff, staffUser);
    }
}
