using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using SchoolAccount.ResiliencePlayground.Factories;
using SchoolAccount.ResiliencePlayground.Models;

namespace SchoolAccount.ResiliencePlayground.Engines;

public class ServiceRegistry
{
    private readonly ConcurrentDictionary<string, Service> _registry = new();

    public void Register(ServiceManifest manifest, ResiliencePipeline<HttpResponseMessage> pipeline)
    {
        _registry[manifest.ServiceName.ToLowerInvariant()] = new Service(manifest.ServiceName, manifest.BaseUrl, pipeline);
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
        _registry[serviceName.ToLowerInvariant()].History.Add(status);
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
            builder.Services.AddHttpClient(vendor.ServiceName, c => c.BaseAddress = new Uri(vendor.BaseUrl));
            registry.Register(vendor, ResiliencePipelineFactory.Create(vendor, options.Defaults));
        }
        
        builder.Services.AddSingleton(registry);
    }
}