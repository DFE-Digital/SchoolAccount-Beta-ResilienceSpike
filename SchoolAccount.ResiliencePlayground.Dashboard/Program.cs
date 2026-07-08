using SchoolAccount.ResiliencePlayground.Dashboard.Extensions;
using SchoolAccount.ResiliencePlayground.Engines;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceRegistry();
builder.Services.AddHostedService<ServiceMonitoring>();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment()) app.MapOpenApi();

app.MapAllEndpoints();

app.Run();