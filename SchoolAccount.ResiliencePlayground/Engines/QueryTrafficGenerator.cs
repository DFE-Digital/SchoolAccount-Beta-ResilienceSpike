using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchoolAccount.ResiliencePlayground.Models;

namespace SchoolAccount.ResiliencePlayground.Engines;

public class QueryTrafficGenerator(
    ResilienceQueryHandler queryHandler,
    IOptions<IntegrationSettings> options,
    ILogger<QueryTrafficGenerator> logger)
    : BackgroundService
{
    private readonly IntegrationSettings _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("[QueryTraffic] Synthetic query traffic started.");

        var trafficTasks = _options.Services.Select(service =>
            Task.Run(() => GenerateTraffic(service, cancellationToken), cancellationToken));

        await Task.WhenAll(trafficTasks);
    }

    private async Task GenerateTraffic(ServiceManifest service, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await queryHandler.GetAsync<Nothing>(
                    service.TaskEndpoint,
                    service,
                    TrafficSource.Query,   // real-traffic tagging from last time
                    enforcedResilience: null,  // service's own pipeline → chaos applies
                    cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "[QueryTraffic] Unexpected error querying {Service}", service.ServiceName);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
            catch (TaskCanceledException)
            {
                // graceful shutdown
            }
        }
    }
}