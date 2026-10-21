using Claims.Application.Notifications;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Claims.Infrastructure.Messaging;

/// <summary>Runs an outbox relay pass every RelayIntervalSeconds. Request handlers never call Kafka.</summary>
internal sealed class OutboxRelay(IServiceScopeFactory scopes, IOptions<KafkaOptions> options, ILogger<OutboxRelay> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.RelayIntervalSeconds));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var published = await scope.ServiceProvider.GetRequiredService<OutboxProcessor>().ProcessBatchAsync(stoppingToken);
                if (published > 0)
                {
                    logger.LogInformation("Outbox relay published {Count} event(s)", published);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // e.g. database unavailable: log and try again next tick.
                logger.LogError("Outbox relay pass failed ({ErrorType})", ex.GetType().Name);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

/// <summary>
/// Consumes claims.events and creates notifications. Manual offset commit after each message is
/// handled or dead-lettered (at-least-once; the handler de-duplicates on eventId).
/// </summary>
internal sealed class NotificationConsumer(
    IServiceScopeFactory scopes, IDeadLetterPublisher deadLetters, IOptions<KafkaOptions> options, ILogger<NotificationConsumer> logger)
    : BackgroundService
{
    // Consume() blocks, so the loop gets its own long-running thread instead of a pool thread.
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.Factory.StartNew(() => RunAsync(stoppingToken), stoppingToken, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();

    private async Task RunAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = options.Value.BootstrapServers,
            GroupId = options.Value.ConsumerGroup,
            EnableAutoCommit = false,
            AutoOffsetReset = AutoOffsetReset.Earliest,
        };

        using var consumer = new ConsumerBuilder<string, string>(config)
            .SetErrorHandler((_, error) => logger.LogWarning("Kafka consumer: {Reason}", error.Reason))
            .Build();
        consumer.Subscribe(options.Value.Topic);

        var dispatcher = new MessageDispatcher(HandleInScopeAsync, deadLetters, MessageDispatcher.DefaultRetryDelays, logger);

        while (!stoppingToken.IsCancellationRequested)
        {
            ConsumeResult<string, string>? result;
            try
            {
                result = consumer.Consume(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ConsumeException ex)
            {
                logger.LogWarning("Kafka consume failed: {Reason}", ex.Error.Reason);
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                continue;
            }

            if (result?.Message is null)
            {
                continue;
            }

            await dispatcher.DispatchAsync(result.Message.Key, result.Message.Value, stoppingToken);
            consumer.Commit(result);
        }

        consumer.Close();
    }

    private async Task HandleInScopeAsync(Application.Common.Messaging.EventEnvelope envelope, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ClaimEventNotificationHandler>().HandleAsync(envelope, cancellationToken);
    }
}
