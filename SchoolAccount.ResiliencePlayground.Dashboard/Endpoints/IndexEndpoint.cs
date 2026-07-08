using Microsoft.Extensions.Options;
using SchoolAccount.ResiliencePlayground.Dashboard.Endpoints.Interfaces;
using SchoolAccount.ResiliencePlayground.Engines;
using SchoolAccount.ResiliencePlayground.Extensions;
using SchoolAccount.ResiliencePlayground.Models;

namespace SchoolAccount.ResiliencePlayground.Dashboard.Endpoints;

public class IndexEndpoint : IEndpoint
{
    public static void MapEndpoint(WebApplication app)
    {
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
    }
}