using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SchoolAccount.ResiliencePlayground.Dashboard.Endpoints.Interfaces;
using SchoolAccount.ResiliencePlayground.Dashboard.Models;
using SchoolAccount.ResiliencePlayground.Engines;
using SchoolAccount.ResiliencePlayground.Models;

namespace SchoolAccount.ResiliencePlayground.Dashboard.Endpoints;

public class QueryEndpoint : IEndpoint
{
    public static void MapEndpoint(WebApplication app)
    {
        app.MapGet("/query", async (
            [FromServices] IOptions<IntegrationSettings> options,
            [FromServices] ServiceRegistry registry,
            [FromServices] ResilienceQueryHandler queryHandler,
            CancellationToken cancellationToken) =>
        {
            var settings = options.Value;

            try
            {
                var queryingTasks = settings.Services.Select(service =>
                    GetPayloadAsync(queryHandler, registry, service, cancellationToken)
                );

                var results = await Task.WhenAll(queryingTasks);

                return Results.Json(Operation.Ok(results));
            }
            catch (Exception ex)
            {
                return Results.Json(Operation.Exception(ex));
            }
        });
    }

    private static async Task<Payload<PayloadTaskEntity>> GetPayloadAsync(ResilienceQueryHandler queryHandler,
        ServiceRegistry registry,
        ServiceManifest manifest,
        CancellationToken cancellationToken)
    {
        var service = registry.Get(manifest.ServiceName);

        if (service is null) throw new ApplicationException($"Could not find service \"{manifest.ServiceName}\"");

        var query = await queryHandler.GetAsync<Operation<List<PayloadTaskEntity>>>(
            manifest.TaskEndpoint,
            manifest,
            TrafficSource.Query,
            null,
            cancellationToken);

        var serviceIdentifier = new PayloadService
        {
            Name = service.ServiceName,
            QueryPath = manifest.TaskEndpoint
        };

        if (!query.Success)
            return new Payload<PayloadTaskEntity>
            {
                Service = serviceIdentifier,
                Health = new PayloadHealth
                {
                    StatusCode = 503
                }
            };

        var payload = new Payload<PayloadTaskEntity>
        {
            Service = serviceIdentifier,
            Health = new PayloadHealth
            {
                StatusCode = 200
            },
            Rows = query.Value!.Value!
                .Select(item => new IdentifyingRow<PayloadTaskEntity>
                {
                    Id = TaskReference.Create(service.ServiceName, item.Key.ToString()),
                    Data = item
                })
                .ToList()
        };

        return payload;
    }
}