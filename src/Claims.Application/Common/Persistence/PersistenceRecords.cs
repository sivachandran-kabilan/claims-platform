namespace Claims.Application.Common.Persistence;

/// <summary>
/// A domain event waiting to be published (ADR 003). Written in the same SaveChanges as the claim change;
/// the relay (later slice) publishes it to Kafka and sets ProcessedAt.
/// </summary>
public sealed class OutboxMessage
{
    private OutboxMessage() { } // EF

    public OutboxMessage(Guid id, string type, int version, Guid claimId, string payload, DateTimeOffset occurredAt)
    {
        Id = id;
        Type = type;
        Version = version;
        ClaimId = claimId;
        Payload = payload;
        OccurredAt = occurredAt;
    }

    /// <summary>The event id; consumers de-duplicate on it.</summary>
    public Guid Id { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public int Version { get; private set; }
    public Guid ClaimId { get; private set; }
    public string Payload { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }
    public int Attempts { get; private set; }
    public string? LastError { get; private set; }

    public void MarkProcessed(DateTimeOffset now)
    {
        ProcessedAt = now;
        LastError = null;
    }

    /// <summary>Keeps the row unprocessed so the next relay run retries it.</summary>
    public void RecordFailure(string error, DateTimeOffset now)
    {
        Attempts++;
        var text = $"{now:O} {error}";
        LastError = text.Length <= 2000 ? text : text[..2000];
    }
}

/// <summary>An in-app message for a user, written by the notification consumer. Text is built from the claim number only.</summary>
public sealed class Notification
{
    private Notification() { } // EF

    public Notification(Guid id, Guid userId, Guid claimId, string claimNumber, string type, string message, DateTimeOffset createdAt)
    {
        Id = id;
        UserId = userId;
        ClaimId = claimId;
        ClaimNumber = claimNumber;
        Type = type;
        Message = message;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid ClaimId { get; private set; }
    public string ClaimNumber { get; private set; } = string.Empty;
    public string Type { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
}

/// <summary>Consumer idempotency: an event id seen here is never handled again (ADR 003).</summary>
public sealed class ProcessedEvent
{
    private ProcessedEvent() { } // EF

    public ProcessedEvent(Guid eventId, DateTimeOffset processedAt)
    {
        EventId = eventId;
        ProcessedAt = processedAt;
    }

    public Guid EventId { get; private set; }
    public DateTimeOffset ProcessedAt { get; private set; }
}

/// <summary>Remembers a POST /claims Idempotency-Key per user so a retry returns the same claim.</summary>
public sealed class IdempotencyRecord
{
    private IdempotencyRecord() { } // EF

    public IdempotencyRecord(Guid userId, string key, string requestHash, Guid claimId, DateTimeOffset createdAt)
    {
        UserId = userId;
        Key = key;
        RequestHash = requestHash;
        ClaimId = claimId;
        CreatedAt = createdAt;
    }

    public Guid UserId { get; private set; }
    public string Key { get; private set; } = string.Empty;

    /// <summary>SHA-256 of the request (excluding the key): same key + different body → 422.</summary>
    public string RequestHash { get; private set; } = string.Empty;
    public Guid ClaimId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
