using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Polly;
using SchoolAccount.ResiliencePlayground.Factories;
using SchoolAccount.ResiliencePlayground.Models;

namespace SchoolAccount.ResiliencePlayground.Engines;

public class ServiceRegistry
{
    private readonly ConcurrentDictionary<string, Service> _registry = new();

    public void Register(ServiceManifest manifest, ResiliencePipeline pipeline)
    {
        _registry[manifest.ServiceName.ToLowerInvariant()] =
            new Service(manifest.ServiceName, manifest.BaseUrl, pipeline);
    }

    public Service? Get(string serviceName)
    {
        return _registry.GetValueOrDefault(serviceName.ToLowerInvariant());
    }

    public IEnumerable<Service> All()
    {
        return _registry.Values;
    }

    public void UpdateStatus(string serviceName, ServiceState status)
    {
        if (!_registry.TryGetValue(serviceName.ToLowerInvariant(), out var service)) return;

        lock (service.History)
        {
            service.History.Add(status);
            if (service.History.Count > 100) service.History.RemoveAt(0);
        }
    }
}

public static class ServiceRegistryCacheExtensions
{
    public static void AddServiceRegistry(this WebApplicationBuilder builder)
    {
        var section = builder.Configuration
            .GetSection(IntegrationSettings.SectionName);

        if (!section.Exists())
        {
            throw new InvalidOperationException("Integration settings not found");
        }
        
        builder.Services.Configure<IntegrationSettings>(
            builder.Configuration.GetSection(IntegrationSettings.SectionName)
        );

        var options = section.Get<IntegrationSettings>();

        if (options is null)
        {
            throw new InvalidOperationException("Integration settings not found");
        }

        var registry = new ServiceRegistry();

        foreach (var vendor in options.Services)
        {
            var name = vendor.ServiceName;
            var tag = name.ToLowerInvariant();

            builder.Services.AddHttpClient(name, c => c.BaseAddress = new Uri(vendor.BaseUrl));

            registry.Register(vendor, ResiliencePipelineFactory.Create(
                vendor,
                options.Defaults,
                vendor.Chaos ?? options.Defaults.Chaos ?? new ChaosSettings()));

            builder.Services.AddHealthChecks()
                .Add(new HealthCheckRegistration(
                    $"{name} (reported health)",
                    sp => new ServiceStateHealthCheck(sp.GetRequiredService<ServiceRegistry>(), name, TrafficSource.Probe),
                    null,
                    [tag]))
                .Add(new HealthCheckRegistration(
                    $"{name} (observed by consumers)",
                    sp => new ServiceStateHealthCheck(sp.GetRequiredService<ServiceRegistry>(), name, TrafficSource.Query),
                    null,
                    [tag]));
        }

        builder.Services.AddSingleton(registry);
    }
}