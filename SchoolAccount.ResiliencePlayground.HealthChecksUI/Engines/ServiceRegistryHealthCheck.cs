using Microsoft.Extensions.Diagnostics.HealthChecks;
using SchoolAccount.ResiliencePlayground.Engines;
using SchoolAccount.ResiliencePlayground.Models;

namespace SchoolAccount.ResiliencePlayground.HealthChecksUI.Engines;

public sealed class ServiceRegistryHealthCheck(ServiceRegistry registry, string serviceName) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var entry = registry.Get(serviceName);

        if (entry is null)
        {
            return Task.FromResult(
                HealthCheckResult.Unhealthy($"No monitoring state recorded yet for '{serviceName}'."));
        }

        var current = entry.CurrentState;

        var data = new Dictionary<string, object>
        {
            ["service"] = serviceName,
            ["status"] = current.Status.ToString(),
            ["lastChecked"] = current.LastChecked,
        };

        if (current.Error?.Message is { } error)
        {
            data["error"] = error;
        }

        var result = current.Status switch
        {
            ServiceStatus.Healthy  => HealthCheckResult.Healthy($"'{serviceName}' is healthy.", data),
            ServiceStatus.Degraded => HealthCheckResult.Degraded($"'{serviceName}' is degraded.", data: data),
            _                      => HealthCheckResult.Unhealthy($"'{serviceName}' is erroring.", data: data),
        };

        return Task.FromResult(result);
    }
}