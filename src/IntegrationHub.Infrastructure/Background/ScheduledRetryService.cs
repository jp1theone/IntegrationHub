using IntegrationHub.Application.Executions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IntegrationHub.Infrastructure.Background;

public sealed class ScheduledRetryService(IServiceScopeFactory scopeFactory, ILogger<ScheduledRetryService> logger, IConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollInterval = TimeSpan.FromMilliseconds(Math.Clamp(configuration.GetValue("Retry:PollIntervalMs", 2000), 250, 60000));
        var batchSize = Math.Clamp(configuration.GetValue("Retry:BatchSize", 20), 1, 100);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                IReadOnlyList<Guid> dueIds;
                await using (var scope = scopeFactory.CreateAsyncScope())
                {
                    var store = scope.ServiceProvider.GetRequiredService<IExecutionStore>();
                    dueIds = await store.GetDueRetryIdsAsync(DateTimeOffset.UtcNow, batchSize, stoppingToken);
                }
                foreach (var executionId in dueIds)
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var processor = scope.ServiceProvider.GetRequiredService<ExecuteIntegrationUseCase>();
                    await processor.ProcessScheduledRetryAsync(executionId, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "Scheduled retry polling failed; the worker will retry after {DelayMs}ms.", pollInterval.TotalMilliseconds); }
            await Task.Delay(pollInterval, stoppingToken);
        }
    }
}
