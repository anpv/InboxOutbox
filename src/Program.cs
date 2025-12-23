using System.Diagnostics;
using InboxOutbox.BackgroundServices;
using InboxOutbox.Contracts;
using InboxOutbox.Entities;
using InboxOutbox.EntityFrameworkCore;
using InboxOutbox.Events;
using InboxOutbox.Extensions;
using InboxOutbox.Implementations;
using InboxOutbox.Options;
using LinqToDB;
using LinqToDB.DataProvider.PostgreSQL;
using LinqToDB.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;
var services = builder.Services;

services.TryAddSingleton(TimeProvider.System);

services
    .AddOpenApi()
    .AddDbContextPool<AppDbContext>((provider, options) =>
    {
        options
            .UseLinqToDB(x => x.AddCustomOptions(y => y.UsePostgreSQL(PostgreSQLVersion.v15)))
            .UseNpgsql(configuration.GetConnectionString("Default"))
            .UseSnakeCaseNamingConvention()
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .UseExtNpgsql()
            ;

        if (configuration.GetValue<bool>("FeatureManagement:SqlLoggingEnabled"))
        {
            options.UseLoggerFactory(provider.GetRequiredService<ILoggerFactory>());
        }
    })
    .AddScoped(x => x.GetRequiredService<AppDbContext>().CreateLinqToDBContext())
    .AddKafka(configuration, options =>
    {
        options.AddProducer<string, MeasurementAdded>("measurementAdded");
        options.AddConsumer<MeasurementAdded, MeasurementAddedConsumer>("measurementAdded");
    })
    .AddStackExchangeRedisCache(x =>
    {
        var options = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>() ??
            throw new InvalidOperationException("Missing Redis options");
        x.Configuration = options.Configuration;
        x.InstanceName = options.InstanceName;
    })
    .AddSingleton<ClusterService>()
    .AddSingleton<IClusterService, ClusterService>()
    .AddHostedService<ClusterBackgroundService>()
    .AddHostedService<StuckSendingBackgroundService>()
    .AddSingleton<TransactionScopeFactory>()
    .AddScoped<MeasurementService>()
    ;

services.AddOptionsWithValidateOnStart<ClusterOptions>()
    .Bind(configuration.GetSection(ClusterOptions.ConfigSectionKey))
    .ValidateDataAnnotations();

services.AddOptionsWithValidateOnStart<RedisOptions>()
    .Bind(configuration.GetSection(RedisOptions.SectionName));

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference();

app.MapPost("produce", async (
    [FromQuery] int count,
    MeasurementService service,
    TimeProvider timeProvider,
    CancellationToken token) =>
{
    var sw = Stopwatch.StartNew();
    var random = new Random();

    var measurements = Enumerable.Range(0, count)
        .Select(_ => new Measurement
        {
            Value = random.Next(),
            CreatedAt = timeProvider.GetUtcNow()
        });

    await service.AddRange(measurements, token);

    sw.Stop();

    return Results.Ok($"Produced {count} messages in {sw.Elapsed}");
});

app.Run();