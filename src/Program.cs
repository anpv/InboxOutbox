using System.Diagnostics;
using InboxOutbox.Contracts;
using InboxOutbox.EntityFrameworkCore;
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
        options.AddProducer<string, TestMessage>("test-topic");
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
    .AddHostedService<StuckSendingBackgroundService>();

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
    IKafkaProducer<string, TestMessage> producer,
    CancellationToken token) =>
{
    const int chunkSize = 1000;
    var sw = Stopwatch.StartNew();
    var records = new List<ProducerRecord<string, TestMessage>>(chunkSize);
    foreach (var chunk in Enumerable.Range(0, count).Chunk(chunkSize))
    {
        records.Clear();

        foreach (var _ in chunk)
        {
            var key = Guid.NewGuid().ToString();
            var message = new TestMessage(Guid.NewGuid(), Guid.NewGuid().ToString());
            var headers = new Dictionary<string, string?>
            {
                ["H1"] = Guid.NewGuid().ToString(),
                ["H2"] = Guid.NewGuid().ToString(),
                ["H3"] = Guid.NewGuid().ToString()
            };

            records.Add(ProducerRecord.Create(key, message, headers));
        }

        await producer.ProduceAsync(records, token);
    }

    sw.Stop();

    return Results.Ok($"Produced {count} messages in {sw.Elapsed}");
});

app.Run();

public sealed record TestMessage(Guid Id, string Message);