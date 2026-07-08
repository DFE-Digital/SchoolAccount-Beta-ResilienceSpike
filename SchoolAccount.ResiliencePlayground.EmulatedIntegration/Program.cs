using Microsoft.Extensions.Options;
using SchoolAccount.ResiliencePlayground;
using SchoolAccount.ResiliencePlayground.EmulatedIntegration.Builders;
using SchoolAccount.ResiliencePlayground.EmulatedIntegration.Models;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<EnvironmentSettings>(builder.Configuration.GetSection(EnvironmentSettings.SectionName));

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/about", (IOptions<EnvironmentSettings> options) =>
{
    var settings = options.Value;
    return Results.Ok(settings);
});

app.MapGet("/health", async (IOptions<EnvironmentSettings> options) =>
{
    var settings = options.Value;
    var emulate = await Resilience(settings);

    if (!emulate.Success)
    {
        return Results.Json(
            new
            {
                Environment = settings.Name, 
                Detail = "Simulated integration failure."
            }, 
            statusCode: StatusCodes.Status503ServiceUnavailable
        );
    }

    return Results.Ok(new
    {
        Environment = settings.Name,
        Timestamp = DateTime.UtcNow
    });
});

app.MapGet("/tasks", async (int? amount, IOptions<EnvironmentSettings> options) =>
{
    var emulate = await Resilience(options.Value);

    if (!emulate.Success)
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }

    var tasks = TaskEntityCollectionBuilder.ATaskCollection(amount: amount).Build();
    return Results.Ok(Operation.Ok(tasks));
});

app.Run();

static async Task<Operation> Resilience(EnvironmentSettings settings)
{
    var random = Random.Shared;

    if (settings.SlowRate > 0d && random.NextDouble() < settings.SlowRate)
    {
        await Task.Delay(settings.SlowDelayMs);
    }

    if (settings.ErrorRate > 0d && random.NextDouble() <= settings.ErrorRate)
    {
        return Operation.Failed("Hit failure rate");
    }
    
    return Operation.Ok();
}
