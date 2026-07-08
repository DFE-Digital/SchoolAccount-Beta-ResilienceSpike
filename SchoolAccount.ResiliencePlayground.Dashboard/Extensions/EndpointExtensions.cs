using System.Reflection;
using SchoolAccount.ResiliencePlayground.Dashboard.Endpoints.Interfaces;

namespace SchoolAccount.ResiliencePlayground.Dashboard.Extensions;

public static class EndpointExtensions
{
    public static IEndpointRouteBuilder MapAllEndpoints(this IEndpointRouteBuilder app)
    {
        var endpointTypes = Assembly
            .GetExecutingAssembly()
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.IsAssignableTo(typeof(IEndpoint)));

        foreach (var type in endpointTypes)
        {
            var method = type.GetMethod(nameof(IEndpoint.MapEndpoint), BindingFlags.Public | BindingFlags.Static);
            method?.Invoke(null, [app]);
        }

        return app;
    }
}