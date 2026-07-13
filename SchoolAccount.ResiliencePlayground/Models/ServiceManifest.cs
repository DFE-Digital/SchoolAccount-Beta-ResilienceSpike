namespace SchoolAccount.ResiliencePlayground.Models;

public class ServiceManifest : ResilienceSettings
{
    public required string ServiceName { get; init; }
    public required string BaseUrl { get; init; }

    public string HealthEndpoint { get; init; } = "/health";
    public string TaskEndpoint { get; init; } = "/tasks";
}