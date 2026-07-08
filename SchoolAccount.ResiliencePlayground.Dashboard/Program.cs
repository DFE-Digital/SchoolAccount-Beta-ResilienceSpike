using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SchoolAccount.ResiliencePlayground.Dashboard.Endpoints;
using SchoolAccount.ResiliencePlayground.Dashboard.Extensions;
using SchoolAccount.ResiliencePlayground.Engines;
using SchoolAccount.ResiliencePlayground.Extensions;
using SchoolAccount.ResiliencePlayground.Models;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceRegistry();
builder.Services.AddHostedService<ServiceMonitoring>();
builder.Services.AddSingleton<ResilienceQueryHandler>();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapAllEndpoints();

app.Run();