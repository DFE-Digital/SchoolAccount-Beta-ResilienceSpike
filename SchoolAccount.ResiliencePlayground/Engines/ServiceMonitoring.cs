using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchoolAccount.ResiliencePlayground.Factories;
using SchoolAccount.ResiliencePlayground.Models;

namespace SchoolAccount.ResiliencePlayground.Engines;

public class ServiceMonitoring(
    ResilienceQueryHandler queryHandler,
    IOptions<IntegrationSettings> options,
    ILogger<ServiceMonitoring> logger)
    : BackgroundService
{
    private readonly IntegrationSettings _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("[ServiceMonitoring] Background Integration Monitor started.");

        var monitoringTasks = _options.Services.Select(service =>
            Task.Run(() => MonitorService(service, cancellationToken), cancellationToken)
        );

        await Task.WhenAll(monitoringTasks);
    }

    private async Task MonitorService(ServiceManifest service, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await queryHandler.GetAsync<Nothing>(
                    service.HealthEndpoint,
                    service,
                    TrafficSource.Probe,
                    ResiliencePipelineFactory.Monitor(),
                    cancellationToken);
                
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            }
            catch (TaskCanceledException)
            {
                // Absorb cleanup shutdown exceptions gracefully
            }
        }
    }
}