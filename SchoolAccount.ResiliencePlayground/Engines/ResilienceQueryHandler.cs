using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using SchoolAccount.ResiliencePlayground.Factories;
using SchoolAccount.ResiliencePlayground.Models;

namespace SchoolAccount.ResiliencePlayground.Engines;

public class ResilienceQueryHandler(
    IHttpClientFactory clientFactory,
    ServiceRegistry registry,
    IOptions<IntegrationSettings> options,
    ILogger<ResilienceQueryHandler> logger)
{
    private readonly IntegrationSettings _settings = options.Value;

    public async Task<Operation<T>> GetAsync<T>(
        string requestUrl,
        ServiceManifest service,
        TrafficSource source,
        ResiliencePipeline? enforcedResilience,
        CancellationToken cancellationToken)
    {
        var serviceState = registry.Get(service.ServiceName);
        if (serviceState is null)
        {
            Operation.Failed($"Unknown service \"{service.ServiceName}\"");
        }

        var client = clientFactory.CreateClient(service.ServiceName);

        var stopwatch = Stopwatch.StartNew();
        int? statusCode = null;

        var currentRequestLogs = new List<string> { $"[{LogType.Start}]Initializing Request Connection" };

        var context = ResilienceContextPool.Shared.Get();
        context.Properties.Set(ResiliencePipelineFactory.LogTraceKey, currentRequestLogs);

        HttpResponseMessage? response = null;

        try
        {
            response = await (enforcedResilience ?? serviceState!.Pipeline)
                .ExecuteAsync(
                    async (ctx, httpClient) => await httpClient.GetAsync(requestUrl, ctx.CancellationToken),
                    context,
                    state: client);

            stopwatch.Stop();
            statusCode = (int)response.StatusCode;

            var state = TimeSpan.FromMilliseconds(stopwatch.ElapsedMilliseconds).TotalSeconds <=
                        (service.DegradedResponseThresholdSeconds ??
                         _settings.Defaults.DegradedResponseThresholdSeconds)
                ? ServiceStatus.Healthy
                : ServiceStatus.Degraded;

            if (!response.IsSuccessStatusCode)
            {
                state = ServiceStatus.Error;
            }

            currentRequestLogs.Add($"[{LogType.Complete}] Response Complete with HTTP status code: {statusCode}");
            registry.UpdateStatus(service.ServiceName,
                new ServiceState(source, state,
                    ServicePerformance.Create(statusCode, stopwatch.ElapsedMilliseconds),
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
                new ServiceState(source, ServiceStatus.Error, ex.Message,
                    ServicePerformance.Create(statusCode, stopwatch.ElapsedMilliseconds),
                    currentRequestLogs));
        }
        finally
        {
            ResilienceContextPool.Shared.Return(context);
        }
            
        if (typeof(T) == typeof(Nothing))
        {
            return Operation.Ok<T>(default);
        }

        if (response is null)
        {
            return Operation.Failed<T>("No response payload");
        }

        try
        {
            var fromContent = await response.Content.ReadFromJsonAsync<T>(cancellationToken);
            return fromContent is not null
                ? Operation.Ok(fromContent)
                : Operation.Failed<T>("Failed to convert response content");
        }
        catch (JsonException ex)
        {
            Operation.Failed<T>($"Failed to convert response content: {ex.Message}");
        }
        
        return Operation.Failed<T>($"Erm");
    }
}