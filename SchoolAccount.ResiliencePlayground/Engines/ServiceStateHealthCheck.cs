using Microsoft.Extensions.Diagnostics.HealthChecks;
using SchoolAccount.ResiliencePlayground.Models;

namespace SchoolAccount.ResiliencePlayground.Engines;

public sealed class ServiceStateHealthCheck(
    ServiceRegistry registry, string serviceName, TrafficSource source) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var entry = registry.Get(serviceName);

        if (entry is null)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"'{serviceName}' is not registered — check integration configuration."));
        }

        var current = entry.CurrentState(source);
        var describe = source == TrafficSource.Probe ? "health endpoint" : "query traffic";

        var data = new Dictionary<string, object>
        {
            ["service"] = serviceName,
            ["source"] = source.ToString(),
            ["status"] = current.Status.ToString(),
            ["statusCode"] = current.Performance.StatusCode?.ToString() ?? "none",
            ["responseMs"] = Math.Round(current.Performance.Response.TotalMilliseconds),
            ["lastChecked"] = current.LastChecked,
        };

        if (!string.IsNullOrEmpty(current.Error?.Message))
        {
            data["error"] = current.Error.Message;
        }

        var result = current.Status switch
        {
            ServiceStatus.Healthy => HealthCheckResult.Healthy(
                $"Healthy via {describe} — HTTP {current.Performance.StatusCode} in {current.Performance.Response.TotalMilliseconds:0}ms",
                data),

            ServiceStatus.Degraded => HealthCheckResult.Degraded(
                $"Degraded via {describe} — HTTP {current.Performance.StatusCode} in {current.Performance.Response.TotalSeconds:0.0}s (over threshold)",
                data: data),

            ServiceStatus.Error => HealthCheckResult.Unhealthy(
                $"Failing via {describe} — {current.Error?.Message ?? $"HTTP {current.Performance.StatusCode}"} after {current.Performance.Response.TotalSeconds:0.0}s",
                data: data),

            _ => HealthCheckResult.Degraded(
                $"No {describe} traffic observed yet for '{serviceName}'.",
                data: data),
        };

        return Task.FromResult(result);
    }
}