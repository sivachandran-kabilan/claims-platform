using System.Text.Json.Serialization;
using Claims.Application.Claims.Common;
using Claims.Application.Common.Exceptions;
using Claims.Application.Common.Persistence;
using Claims.Application.Common.Security;
using Claims.Domain.Claims;
using Microsoft.EntityFrameworkCore;

namespace Claims.Application.Claims.Workflow;

/// <summary>Base for every claim action. Route id and If-Match come from HTTP, never from the body.</summary>
public abstract record ClaimCommand
{
    [JsonIgnore] public Guid ClaimId { get; init; }
    [JsonIgnore] public string? IfMatch { get; init; }
}

/// <summary>
/// The one path every claim action takes, so the checks can't drift between endpoints:
/// 428 (no If-Match) → 404 (not visible) → 403 (may not act) → 412 (stale) → domain (409/422) → save → detail.
/// 404 before 403 so claimants can't discover other claims; 403 before 412 so versions can't be probed.
/// </summary>
public sealed class ClaimWorkflow(IClaimsDbContext db, ICurrentUser currentUser, TimeProvider clock)
{
    public Task<ClaimDetailResult> RunAsync(
        ClaimCommand command, ActRule rule, Action<Claim, Actor, DateTimeOffset> action, CancellationToken cancellationToken) =>
        RunAsync(command, rule, (claim, actor, now) => { action(claim, actor, now); return Task.CompletedTask; }, cancellationToken);

    public async Task<ClaimDetailResult> RunAsync(
        ClaimCommand command, ActRule rule, Func<Claim, Actor, DateTimeOffset, Task> action, CancellationToken cancellationToken)
    {
        var expectedVersion = ETags.ParseIfMatch(command.IfMatch);

        // Children are loaded because the aggregate needs them (open request, history sequence).
        var claim = await db.Claims
            .Include(c => c.InformationRequests)
            .Include(c => c.Documents)
            .Include(c => c.History)
            .VisibleTo(currentUser)
            .SingleOrDefaultAsync(c => c.Id == command.ClaimId, cancellationToken)
            ?? throw new NotFoundException("Claim not found.");

        ClaimAccess.EnsureCanAct(claim, currentUser, rule);

        var version = db.Entry(claim).Property<byte[]>(ClaimsDbConstants.RowVersion);
        if (expectedVersion is null || !expectedVersion.AsSpan().SequenceEqual(version.CurrentValue))
        {
            throw new PreconditionFailedException();
        }

        // EF also checks this value in the UPDATE, catching a change between our read and our write.
        version.OriginalValue = expectedVersion;

        await action(claim, currentUser.ToActor(), clock.GetUtcNow());

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new PreconditionFailedException();
        }

        return await ClaimReadModel.GetDetailAsync(db, claim.Id, currentUser, cancellationToken)
            ?? throw new InvalidOperationException("Claim could not be read back after the action.");
    }
}
