namespace Claims.Infrastructure.Messaging;

/// <summary>Bound from the "Kafka" section. Enabled=false (tests) registers no relay, consumer or producer.</summary>
public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    public bool Enabled { get; set; } = true;
    public string BootstrapServers { get; set; } = "localhost:9092";
    public string Topic { get; set; } = "claims.events";
    public string DeadLetterTopic { get; set; } = "claims.events.dlt";
    public string ConsumerGroup { get; set; } = "claims-notifications";
    public int RelayIntervalSeconds { get; set; } = 2;
}
