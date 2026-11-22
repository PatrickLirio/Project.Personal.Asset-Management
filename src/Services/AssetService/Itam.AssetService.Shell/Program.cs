using Itam.AssetService.Api.Controllers;
using Itam.AssetService.Application.Events;
using Itam.AssetService.Application.Identity;
using Itam.AssetService.Application.Repositories;
using Itam.AssetService.Application.Services;
using Itam.AssetService.Infrastructure.Data;
using Itam.AssetService.Infrastructure.Messaging;
using Itam.AssetService.Infrastructure.Repositories;
using Itam.AssetService.Shell.Identity;
using Itam.Common.Configuration.Options;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

var dbOptions = builder.Configuration
    .GetSection(DatabaseOptions.SectionName)
    .Get<DatabaseOptions>()
    ?? throw new InvalidOperationException("Database options missing!");

var rmqOptions = builder.Configuration
    .GetSection(RabbitMqOptions.SectionName)
    .Get<RabbitMqOptions>()
    ?? throw new InvalidOperationException("RabbitMq options missing!");

builder.Services.AddDbContext<AssetDbContext>(options =>
    options.UseSqlServer(dbOptions.BizDb, sqlOptions =>
        sqlOptions.CommandTimeout(dbOptions.CommandTimeoutSeconds)));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

// builder.Services.AddScoped<IAssetRepository, AssetRepository>();
// builder.Services.AddScoped<ICustodianRepository, CustodianRepository>();
builder.Services.AddScoped<IAssetService, AssetService>();
builder.Services.AddScoped<IAssetEventPublisher, MassTransitAssetEventPublisher>();



builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(rmqOptions.Host, (ushort)rmqOptions.Port, rmqOptions.VirtualHost, host =>
        {
            host.Username(rmqOptions.Username);
            host.Password(rmqOptions.Password);
        });

        cfg.ConfigureEndpoints(context);
    });
});

builder.Services.AddControllers()
    .AddApplicationPart(typeof(AssetsController).Assembly);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "IT Asset Management API", Version = "v1" });
});

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();

sealed class DatabaseHealthCheck(AssetDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("Database is reachable.")
                : HealthCheckResult.Unhealthy("Database is unreachable.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Database is unreachable.", exception);
        }
    }
}
