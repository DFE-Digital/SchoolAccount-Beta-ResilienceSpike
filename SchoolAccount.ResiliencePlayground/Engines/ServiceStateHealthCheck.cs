using Microsoft.Extensions.Diagnostics.HealthChecks;
using SchoolAccount.ResiliencePlayground.Engines;
using SchoolAccount.ResiliencePlayground.Models;

namespace SchoolAccount.ResiliencePlayground.HealthChecksUI.Engines;

public class ServiceStateHealthCheck(ServiceRegistry registry, string serviceName) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var latest = registry.Get(serviceName)?.History.LastOrDefault();

        var result = latest?.Status switch
        {
            ServiceStatus.Healthy => HealthCheckResult.Healthy("Last observed healthy"),
            ServiceStatus.Degraded => HealthCheckResult.Degraded("Slow responses observed"),
            ServiceStatus.Error => HealthCheckResult.Unhealthy("Failures observed"),
            _ => HealthCheckResult.Degraded("No traffic observed yet")
        };

        return Task.FromResult(result);
    }
}