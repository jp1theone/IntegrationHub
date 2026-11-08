using IntegrationHub.Application.Webhooks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IntegrationHub.Infrastructure.Background;

public sealed class WebhookProcessingService(IServiceScopeFactory scopeFactory, ILogger<WebhookProcessingService> logger, IConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollInterval = TimeSpan.FromMilliseconds(Math.Clamp(configuration.GetValue("Webhooks:PollIntervalMs", 1000), 250, 60000));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                IReadOnlyList<Guid> queued;
                await using (var scope = scopeFactory.CreateAsyncScope())
                    queued = await scope.ServiceProvider.GetRequiredService<IWebhookEventStore>().GetQueuedIdsAsync(25, stoppingToken);
                foreach (var id in queued)
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<WebhookUseCases>().ProcessQueuedAsync(id, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "Webhook queue polling failed; the worker will retry after {DelayMs}ms.", pollInterval.TotalMilliseconds); }
            await Task.Delay(pollInterval, stoppingToken);
        }
    }
}
