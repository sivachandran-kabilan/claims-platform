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
