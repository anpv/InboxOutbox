using InboxOutbox.BackgroundServices;
using InboxOutbox.Contracts;
using InboxOutbox.Implementations;
using InboxOutbox.Options;
using Microsoft.Extensions.Options;

namespace InboxOutbox.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKafka(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IKafkaOptionsBuilder> configure)
    {
        services.AddOptionsWithValidateOnStart<KafkaOptions>()
            .Bind(configuration.GetSection(KafkaOptions.ConfigSectionPath));
        
        var options = new KafkaOptionsBuilder(configuration);
        configure(options);

        return options.Register(services);
    }

    private sealed class KafkaOptionsBuilder(IConfiguration configuration) : IKafkaOptionsBuilder
    {
        private readonly ProducerOptionsBuilder _producerOptions = ProducerOptionsBuilder.Create(configuration);
        private readonly ConsumerOptionsBuilder _consumerOptions = ConsumerOptionsBuilder.Create(configuration);

        public IServiceCollection Register(IServiceCollection services)
        {
            _producerOptions.Register(services, configuration);
            _consumerOptions.Register(services, configuration);

            return services;
        }

        public IKafkaOptionsBuilder AddProducer<TKey, TValue>(string topic, IKafkaSerializer? serializer = null)
        {
            _producerOptions.AddProducer<TKey, TValue>(topic, serializer ?? KafkaSerializer.Default);

            return this;
        }

        public IKafkaOptionsBuilder AddConsumer<TKey, TValue, TConsumer>(
            string topic,
            IKafkaDeserializer<TKey> keyDeserializer,
            IKafkaDeserializer<TValue>? valueDeserializer)
            where TConsumer : class, IKafkaConsumer<TKey, TValue>
        {
            valueDeserializer ??= KafkaDeserializer<TValue>.Json;
            _consumerOptions.AddConsumer<TKey, TValue, TConsumer>(topic, keyDeserializer, valueDeserializer);
            
            return this;
        }
    }

    private abstract class ProducerOptionsBuilder
    {
        private readonly List<Action<IServiceCollection>> _registrations = [];

        public static ProducerOptionsBuilder Create(IConfiguration configuration)
        {
            var isOutboxEnabled = configuration.GetValue<bool>(
                $"{OutboxOptions.ConfigSectionPath}:{nameof(OutboxOptions.IsEnabled)}");

            return isOutboxEnabled
                ? new OutboxProducerOptionsBuilder()
                : new KafkaProducerOptionsBuilder();
        }

        public abstract void AddProducer<TKey, TValue>(string topic, IKafkaSerializer serializer);

        public void Register(IServiceCollection services, IConfiguration configuration)
        {
            services.AddOptionsWithValidateOnStart<OutboxOptions>()
                .Bind(configuration.GetRequiredSection(OutboxOptions.ConfigSectionPath));

            services.AddSingleton<RawKafkaProducer>();

            foreach (var register in _registrations)
            {
                register(services);
            }

            Register(services);
        }

        protected virtual void Register(IServiceCollection services)
        {
        }

        protected void AddRegistration(Action<IServiceCollection> registration) =>
            _registrations.Add(registration);
    }

    private sealed class KafkaProducerOptionsBuilder : ProducerOptionsBuilder
    {
        public override void AddProducer<TKey, TValue>(string topic, IKafkaSerializer serializer)
        {
            AddRegistration(services =>
            {
                services.AddSingleton<IKafkaProducer<TKey, TValue>>(
                    x => new KafkaProducer<TKey, TValue>(
                        topic,
                        x.GetRequiredService<RawKafkaProducer>(),
                        serializer));
            });
        }
    }

    private sealed class OutboxProducerOptionsBuilder : ProducerOptionsBuilder
    {
        public override void AddProducer<TKey, TValue>(string topic, IKafkaSerializer serializer)
        {
            AddRegistration(services =>
            {
                services.AddTransient<IKafkaProducer<TKey, TValue>>(
                    x => new OutboxProducer<TKey, TValue>(
                        topic,
                        x.GetRequiredService<OutboxStorage>(),
                        x.GetRequiredService<TimeProvider>(),
                        x.GetRequiredService<IClusterService>(),
                        serializer));
            });
        }

        protected override void Register(IServiceCollection services)
        {
            services
                .AddTransient<OutboxStorage>()
                .AddScoped<OutboxProcessor>()
                .AddHostedService<OutboxBackgroundService>()
                ;
        }
    }

    private abstract class ConsumerOptionsBuilder
    {
        private readonly List<Action<IServiceCollection>> _registrations = [];
        
        public static ConsumerOptionsBuilder Create(IConfiguration configuration)
        {
            var isInboxEnabled = configuration.GetValue<bool>(
                $"{InboxOptions.ConfigSectionPath}:{nameof(InboxOptions.IsEnabled)}");
            
            return isInboxEnabled
                ? new InboxConsumerOptionsBuilder()
                : new KafkaConsumerOptionsBuilder();
        }
        
        public abstract void AddConsumer<TKey, TValue, TConsumer>(
            string topic,
            IKafkaDeserializer<TKey> keyDeserializer,
            IKafkaDeserializer<TValue> valueDeserializer)
            where TConsumer : class, IKafkaConsumer<TKey, TValue>;

        public void Register(IServiceCollection services, IConfiguration configuration)
        {
            services.AddOptionsWithValidateOnStart<InboxOptions>()
                .Bind(configuration.GetRequiredSection(InboxOptions.ConfigSectionPath));

            foreach (var register in _registrations)
            {
                register(services);
            }
            
            Register(services);
        }
        
        protected virtual void Register(IServiceCollection services)
        {
        }

        protected void AddRegistration(Action<IServiceCollection> registration) =>
            _registrations.Add(registration);
    }
    
    private sealed class KafkaConsumerOptionsBuilder : ConsumerOptionsBuilder
    {
        public override void AddConsumer<TKey, TValue, TConsumer>(
            string topic,
            IKafkaDeserializer<TKey> keyDeserializer,
            IKafkaDeserializer<TValue> valueDeserializer)
        {
            AddRegistration(services =>
            {
                services
                    .AddTransient<IKafkaConsumer<TKey, TValue>, TConsumer>()
                    .AddKeyedSingleton(
                        topic,
                        (x, _) => new RawKafkaConsumer(
                            [topic],
                            x.GetRequiredService<IHostApplicationLifetime>(),
                            x.GetRequiredService<IOptions<KafkaOptions>>(),
                            x.GetRequiredService<ILogger<RawKafkaConsumer>>()))
                    .AddSingleton<IKafkaConsumerWorker>(x => new KafkaConsumerWorker<TKey, TValue>(
                        x.GetRequiredService<IServiceScopeFactory>(),
                        x.GetRequiredKeyedService<RawKafkaConsumer>(topic),
                        x.GetRequiredService<ILogger<KafkaConsumerWorker<TKey, TValue>>>(),
                        keyDeserializer,
                        valueDeserializer));
            });
        }

        protected override void Register(IServiceCollection services)
        {
            services
                .AddHostedService<KafkaConsumingBackgroundService>()
                ;
        }
    }

    private sealed class InboxConsumerOptionsBuilder : ConsumerOptionsBuilder
    {
        private readonly List<string> _topics = [];
        
        public override void AddConsumer<TKey, TValue, TConsumer>(
            string topic,
            IKafkaDeserializer<TKey> keyDeserializer,
            IKafkaDeserializer<TValue> valueDeserializer)
        {
            _topics.Add(topic);

            AddRegistration(services =>
            {
                services
                    .AddTransient<IKafkaConsumer<TKey, TValue>, TConsumer>()
                    .AddKeyedScoped<IInboxConsumer>(
                        topic,
                        (x, _) => new InboxConsumer<TKey, TValue>(
                            x.GetRequiredService<IKafkaConsumer<TKey, TValue>>(),
                            keyDeserializer,
                            valueDeserializer))
                    ;
            });
        }

        protected override void Register(IServiceCollection services)
        {
            services
                .AddSingleton(x => new RawKafkaConsumer(
                    _topics,
                    x.GetRequiredService<IHostApplicationLifetime>(),
                    x.GetRequiredService<IOptions<KafkaOptions>>(),
                    x.GetRequiredService<ILogger<RawKafkaConsumer>>()))
                .AddTransient<InboxStorage>()
                .AddHostedService<InboxConsumingBackgroundService>()
                .AddHostedService<InboxProcessingBackgroundService>()
                ;
        }
    }
}