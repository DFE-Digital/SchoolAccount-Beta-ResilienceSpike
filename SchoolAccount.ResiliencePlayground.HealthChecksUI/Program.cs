using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SchoolAccount.ResiliencePlayground.Engines;
using SchoolAccount.ResiliencePlayground.HealthChecksUI.Engines;
using SchoolAccount.ResiliencePlayground.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.AddServiceRegistry();
builder.Services.AddSingleton<ResilienceQueryHandler>();
builder.Services.AddHostedService<ServiceMonitoring>();

var integrations = builder.Configuration
    .GetSection("Integrations")
    .Get<IntegrationSettings>();

var healthChecks = builder.Services.AddHealthChecks();

foreach (var service in integrations!.Services)
    healthChecks.AddTypeActivatedCheck<ServiceRegistryHealthCheck>(
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

app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecksUI();

app.Run();