using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
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
            await queryHandler.GetAsync<Nothing>(service.HealthEndpoint, service, ResiliencePipelineFactory.Monitor(),
                cancellationToken);
            
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            }
            catch (TaskCanceledException)
            {
                // Absorb cleanup shutdown exceptions gracefully
            }
        }
    }
}