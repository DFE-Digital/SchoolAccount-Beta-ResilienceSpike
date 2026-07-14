using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using SchoolAccount.ResiliencePlayground.Engines;
using SchoolAccount.ResiliencePlayground.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.AddServiceRegistry();
builder.Services.AddSingleton<ResilienceQueryHandler>();
builder.Services.AddHostedService<ServiceMonitoring>();
builder.Services.AddHostedService<QueryTrafficGenerator>();

var integrations = builder.Configuration
    .GetSection("Integrations")
    .Get<IntegrationSettings>();

var healthChecks = builder.Services.AddHealthChecks();

foreach (var service in integrations!.Services)
    healthChecks.AddTypeActivatedCheck<ServiceStateHealthCheck>(
        service.ServiceName,
        HealthStatus.Unhealthy,
        new[] { "integration" },
        service.ServiceName);

builder.Services
    .AddHealthChecksUI()
    .AddInMemoryStorage();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error", true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.UseStaticFiles();

var settings = app.Services.GetRequiredService<IOptions<IntegrationSettings>>().Value;

foreach (var vendor in settings.Services)
{
    var tag = vendor.ServiceName.ToLowerInvariant();
    app.MapHealthChecks($"/health/{tag}", new HealthCheckOptions
    {
        Predicate = r => r.Tags.Contains(tag),
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });
}

app.MapHealthChecksUI();

app.Run();