using System.Text.Json;
using Claims.Application.Common.Messaging;
using Claims.Application.Common.Persistence;
using Claims.Domain.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Claims.Application.Notifications;

/// <summary>
/// The notification consumer's logic: turns a claim event into an in-app message for the claimant.
/// Idempotent: the notification and the processed event id commit together, and a seen id is skipped.
/// </summary>
public sealed class ClaimEventNotificationHandler(
    IClaimsDbContext db, TimeProvider clock, ILogger<ClaimEventNotificationHandler> logger)
{
    /// <summary>The payload fields this consumer needs (no PII in events).</summary>
    private sealed record ClaimEventPayload(string ClaimNumber, Guid ClaimantUserId, DecisionOutcome? Outcome);

    public async Task HandleAsync(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        if (await AlreadyProcessedAsync(envelope.EventId, cancellationToken))
        {
            logger.LogInformation("Event {EventId} already processed; skipping duplicate", envelope.EventId);
            return;
        }

        var payload = envelope.Payload.Deserialize<ClaimEventPayload>(EventJson.Options)
            ?? throw new JsonException($"Event {envelope.EventId} has no payload.");
        var now = clock.GetUtcNow();

        var message = MessageFor(envelope.Type, payload);
        if (message is null)
        {
            logger.LogWarning("Event {EventId} has unknown type {EventType}; recorded without a notification", envelope.EventId, envelope.Type);
        }
        else
        {
            db.Notifications.Add(new Notification(
                Guid.NewGuid(), payload.ClaimantUserId, envelope.ClaimId, payload.ClaimNumber, envelope.Type, message, now));
        }

        db.ProcessedEvents.Add(new ProcessedEvent(envelope.EventId, now));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (!cancellationToken.IsCancellationRequested)
        {
            // Two deliveries of the same event raced: the other one won, which is the outcome we want.
            if (!await AlreadyProcessedAsync(envelope.EventId, cancellationToken))
            {
                throw;
            }
        }
    }

    private Task<bool> AlreadyProcessedAsync(Guid eventId, CancellationToken cancellationToken) =>
        db.ProcessedEvents.AsNoTracking().AnyAsync(e => e.EventId == eventId, cancellationToken);

    private static string? MessageFor(string type, ClaimEventPayload p) => type switch
    {
        "ClaimSubmitted" => $"Your claim {p.ClaimNumber} has been received.",
        "InformationRequested" => $"We need more information for claim {p.ClaimNumber}. Please respond within {Claim.NoResponseDays} days.",
        "ClaimDecided" when p.Outcome == DecisionOutcome.Approved => $"Your claim {p.ClaimNumber} has been approved.",
        "ClaimDecided" => $"Your claim {p.ClaimNumber} has been rejected. See the claim for details.",
        "ClaimSettled" => $"Your claim {p.ClaimNumber} has been settled.",
        _ => null,
    };
}
