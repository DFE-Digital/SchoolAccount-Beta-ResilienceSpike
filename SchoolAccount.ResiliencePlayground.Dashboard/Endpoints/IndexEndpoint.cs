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
                    var queryState = vendor.CurrentState(TrafficSource.Probe);

                    return new
                    {
                        Vendor = vendor.ServiceName,
                        Endpoint = vendor.BaseUrl,

                        Health = new
                        {
                            CurrentStatus = queryState.Status.ToObject(),
                            LastCheckedAt = queryState.LastChecked,
                            LastKnownError = queryState.Error?.Message
                        }
                        
                        
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