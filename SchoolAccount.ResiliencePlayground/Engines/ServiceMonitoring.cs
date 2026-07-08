using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using SchoolAccount.ResiliencePlayground.Factories;
using SchoolAccount.ResiliencePlayground.Models;

namespace SchoolAccount.ResiliencePlayground.Engines;

public class ServiceMonitoring(
    IHttpClientFactory clientFactory,
    ServiceRegistry registry,
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
            var serviceState = registry.Get(service.ServiceName);
            if (serviceState is null)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                continue;
            }
            
            var client = clientFactory.CreateClient(service.ServiceName);

            var stopwatch = Stopwatch.StartNew();
            int? statusCode = null;

            var currentRequestLogs = new List<string> { $"[{LogType.Start}]Initializing Request Connection" };

            ResilienceContext context = ResilienceContextPool.Shared.Get();
            context.Properties.Set(ResiliencePipelineFactory.LogTraceKey, currentRequestLogs);

            try
            {
                var response = await serviceState.Pipeline
                    .ExecuteAsync(
                        async (ctx, httpClient) => await httpClient.GetAsync(service.HealthEndpoint, ctx.CancellationToken), 
                        context, 
                        state: client);

                stopwatch.Stop();
                statusCode = (int)response.StatusCode;

                var state = TimeSpan.FromMilliseconds(stopwatch.ElapsedMilliseconds).TotalSeconds <= (service.DegradedResponseThresholdSeconds ??
                                                              _options.Defaults.DegradedResponseThresholdSeconds)
                    ? ServiceStatus.Healthy
                    : ServiceStatus.Degraded;

                //var state = ServiceStatus.Healthy;

                if (!response.IsSuccessStatusCode)
                {
                    state = ServiceStatus.Error;
                }

                currentRequestLogs.Add($"[{LogType.Complete}] Response Complete with HTTP status code: {statusCode}");
                registry.UpdateStatus(service.ServiceName,
                    new ServiceState(state, ServicePerformance.Create(statusCode, stopwatch.ElapsedMilliseconds),
                        currentRequestLogs));
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                if (ex is HttpRequestException httpEx && httpEx.StatusCode.HasValue)
                {
                    statusCode = (int)httpEx.StatusCode.Value;
                }

                currentRequestLogs.Add($"[{LogType.Failed}] Execution Exception: {ex.GetType().Name} - {ex.Message}");
                logger.LogWarning("[ServiceMonitoring] {Name} is unreachable after retries.", service.ServiceName);
                registry.UpdateStatus(service.ServiceName,
                    new ServiceState(ServiceStatus.Error, ex.Message,
                        ServicePerformance.Create(statusCode, stopwatch.ElapsedMilliseconds), currentRequestLogs));
            }
            finally
            {
                ResilienceContextPool.Shared.Return(context);
            }
            
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