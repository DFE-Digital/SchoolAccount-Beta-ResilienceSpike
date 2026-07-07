using System;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SchoolAccount.ResiliencePlayground.Engines;
using SchoolAccount.ResiliencePlayground.Extensions;
using SchoolAccount.ResiliencePlayground.Models;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceRegistry();
builder.Services.AddHostedService<ServiceMonitoring>();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/", (ServiceRegistry registry, IOptions<IntegrationSettings> options) =>
{
    var globalDefaults = options.Value.Defaults;
    var services = registry.All();

    var views = services
        .Select(vendor =>
        {
            // double failureRate = vendor.TotalRequests > 0 
            //     ? Math.Round(((double)vendor.FailedRequests / vendor.TotalRequests) * 100, 1) 
            //     : 0;

            return new
            {
                Vendor = vendor.ServiceName,
                Endpoint = vendor.BaseUrl,
                
                Health = new {
                    CurrentStatus = vendor.CurrentState.Status.ToObject(),
                    LastCheckedAt = vendor.CurrentState.LastChecked,
                    LastKnownError = vendor.CurrentState.Error?.Message ?? null
                },
                
                // Telemetry = new {
                //     TotalCallsEvaluated = vendor.TotalRequests,
                //     SuccessCount = vendor.SuccessfulRequests,
                //     FailureCount = vendor.FailedRequests,
                //     CalculatedFailureRate = $"{failureRate}%"
                // }
            };
        })
        .ToList();

    var dashboardPayload = new
    {
        DashboardTimestamp = DateTime.UtcNow,
        ActiveIntegrationsCount = views.Count,
        Integrations = views
    };

    return Results.Ok(dashboardPayload);
});

app.MapGet("/status", ([FromServices] ServiceRegistry registry) =>
{
    var services = registry.All(); // Or cache.GetAllStatuses() depending on your exact method name
    var htmlBuilder = new StringBuilder();
//<meta http-equiv="refresh" content="1"> <!-- Snappy 1-second auto-refresh -->
    htmlBuilder.Append("""
    <!DOCTYPE html>
    <html lang="en">
    <head>
        <meta charset="UTF-8">
        <title>Resilience & Performance Center</title>
        <style>
            :root { --bg: #0b0f19; --card: #151f32; --text: #f8fafc; --muted: #64748b; --online: #10b981; --offline: #f43f5e; --warn: #f59e0b; }
            body { font-family: system-ui, sans-serif; background: var(--bg); color: var(--text); margin: 0; padding: 2rem; }
            .header { display: flex; justify-content: space-between; align-items: center; border-bottom: 1px solid #1e293b; padding-bottom: 1rem; margin-bottom: 2rem; }
            .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(350px, 1fr)); gap: 1.5rem; }
            .card { background: var(--card); border-radius: 12px; padding: 1.5rem; border: 1px solid #1e293b; box-shadow: 0 10px 15px -3px rgba(0,0,0,0.3); }
            .card-header { display: flex; justify-content: space-between; align-items: flex-start; }
            .vendor-name { font-size: 1.3rem; font-weight: 700; margin: 0; }
            .vendor-url { font-size: 0.8rem; color: var(--muted); margin-top: 0.25rem; }
            .badge { font-size: 0.7rem; font-weight: 700; padding: 0.25rem 0.6rem; border-radius: 6px; text-transform: uppercase; }
            .badge.online { background: rgba(16, 185, 129, 0.1); color: var(--online); border: 1px solid var(--online); }
            .badge.degraded { background: rgba(185, 185, 129, 0.1); color: var(--warn); border: 1px solid var(--warn); }
            .badge.offline { background: rgba(244, 63, 94, 0.1); color: var(--offline); border: 1px solid var(--offline); }
            
            /* Performance Styles */
            .metrics-panel { display: flex; justify-content: space-between; background: #0f172a; border-radius: 8px; padding: 1rem; margin-top: 1.25rem; border: 1px solid #1e293b; }
            .metric-box { text-align: center; flex: 1; }
            .metric-box:not(:last-child) { border-right: 1px solid #1e293b; }
            .metric-val { font-size: 1.15rem; font-weight: 700; font-family: monospace; }
            .metric-lbl { font-size: 0.65rem; color: var(--muted); text-transform: uppercase; margin-top: 0.25rem; }
            .state-list { margin-top: 1rem; max-height: 500px; overflow-y: auto; }
            
            /* Bottom Row / Last State Styles */
            .last-state { background: #090d16; border-radius: 8px; padding: 0.75rem 1rem; margin-top: 1rem; font-size: 0.8rem; display: flex; justify-content: space-between; align-items: center; }
            .http-code { font-family: monospace; font-weight: 700; padding: 0.15rem 0.4rem; border-radius: 4px; font-size: 0.75rem; }
            .http-200 { background: #064e3b; color: #10b981; }
            .http-400 { background: #5d3c04; color: var(--warn); }
            .http-500 { background: #4c0519; color: #f43f5e; }
            .http-none { background: #334155; color: #94a3b8; }
            
            .last-state.log {  width: 100%; flex-basis: 100%; }
            .last-state { flex-wrap: wrap; }
        </style>
    </head>
    <body>
        <div class="header">
            <div>
                <h1 style="margin:0;">System Performance Center</h1>
            </div>
        </div>
        <div class="grid">
    """);

    foreach (var service in services)
    {
        var badgeClass = service.CurrentState.Status switch
        {
            ServiceStatus.Healthy => "online",
            ServiceStatus.Degraded => "degraded",
            _ => "offline"
        };
        
        htmlBuilder.Append($"""
            <div class="card">
                <div class="card-header">
                    <div>
                        <h2 class="vendor-name">{service.ServiceName}</h2>
                        <div class="vendor-url">{service.BaseUrl}</div>
                    </div>
                    <span class="badge {badgeClass}">{service.CurrentState.Status.ToHumanString()}</span>
                </div>

                <!-- Timing & Performance Analytics Panel -->
                <div class="metrics-panel">
                    <div class="metric-box">
                        <div class="metric-val" style="color: #3b82f6;">{service.CurrentState.Performance.Response.TotalSeconds.ToString("#,##0.00")}<span style="font-size:0.7rem;color:var(--muted)">s</span></div>
                        <div class="metric-lbl">Last Latency</div>
                    </div>
                    <div class="metric-box">
                        <div class="metric-val" style="color: var(--warn);">{service.AverageResponseTime.TotalSeconds:#,##0.00}<span style="font-size:0.7rem;color:var(--muted)">s</span></div>
                        <div class="metric-lbl">Avg Latency</div>
                    </div>
                    <div class="metric-box">
                        <div class="metric-val" style="color: var(--online);">{service.History.Count(x => x.Status == ServiceStatus.Healthy)}</div>
                        <div class="metric-lbl">Healthy</div>
                    </div>
                    <div class="metric-box">
                        <div class="metric-val" style="color: var(--warn);">{service.History.Count(x => x.Status != ServiceStatus.Healthy)}</div>
                        <div class="metric-lbl">Degraded</div>
                    </div>
                    <div class="metric-box">
                        <div class="metric-val" style="color: var(--text);">{service.History.Count}</div>
                        <div class="metric-lbl">Total Checks</div>
                    </div>
                </div>
                <div class="state-list">
        """);

        foreach (var history in service.History.TakeLast(15).Reverse())
        {
            var codeClass = history.Status switch {
                ServiceStatus.Healthy => "http-200",
                ServiceStatus.Degraded => "http-400",
                ServiceStatus.Error => "http-500",
                _ => "http-none"
            };
            var codeDisplay = history.Performance.StatusCode?.ToString() ?? "NONE";
            htmlBuilder.Append($"""
                <div class="last-state {codeClass}">
                    <div>
                        <span style="color: var(--muted)">Status</span> 
                        <span style="font-family: monospace;">{history.Status.ToHumanString()}</span>
                    </div>
                    <div>
                        <span style="color: var(--muted)">Timestamp</span> 
                        <span style="font-family: monospace;">{history.Performance.Timestamp.ToString("HH:mm:ss")}</span>
                    </div>
                    <div>
                        <span style="color: var(--muted)">Duration</span> 
                        <span style="font-family: monospace;">{history.Performance.Response.TotalSeconds.ToString("#,##0.00")}s</span>
                    </div>
                    <div>
                        <span style="color: var(--muted); margin-right: 0.25rem;">State</span>
                        <span class="http-code http-none">{codeDisplay}</span>
                    </div>
            """);

            if (!history.Logs.All(x => x.Type is LogType.Start or LogType.Complete))
            {
                foreach (var log in history.Logs)
                {
                    htmlBuilder.Append($"""
                                            <div class="last-state log">
                                                <div>
                                                    <span style="color: var(--muted)">Type</span> 
                                                    <span style="font-family: monospace;">{log.Type.ToHumanString()}</span>
                                                </div>
                                                <div style="width: 100%; text-align: left;">
                                                    <span style="color: var(--muted)">Message</span> 
                                                    <span style="font-family: monospace;">{log.Message}</span>
                                                </div>
                                            </div>
                                        """);
                }
            }

            htmlBuilder.Append("""    
                                   </div>
                               """);
        }
        
        htmlBuilder.Append("""
            </div>
            </div>
        """);
    }

    htmlBuilder.Append("""
        </div>
    </body>
    </html>
    """);

    // Return raw compiled document with the proper Content-Type header properties
    return Results.Content(htmlBuilder.ToString(), "text/html", Encoding.UTF8);
});

app.Run();