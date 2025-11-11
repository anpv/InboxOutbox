using InboxOutbox.Contracts;
using InboxOutbox.Implementations;
using InboxOutbox.Options;

namespace InboxOutbox.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKafka(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IKafkaOptionsBuilder> configure)
    {
        var options = new KafkaOptionsBuilder(configuration);
        configure(options);

        return options.Register(services);
    }

    private sealed class KafkaOptionsBuilder(IConfiguration configuration) : IKafkaOptionsBuilder
    {
        private readonly ProducerOptionsBuilder _producerOptions = ProducerOptionsBuilder.Create(configuration);

        public IServiceCollection Register(IServiceCollection services)
        {
            _producerOptions.Register(services, configuration);

            return services;
        }

        public IKafkaOptionsBuilder AddProducer<TKey, TValue>(string topic, IKafkaSerializer? serializer = null)
        {
            _producerOptions.AddProducer<TKey, TValue>(topic, serializer ?? KafkaSerializer.Default);

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
            services.AddOptionsWithValidateOnStart<KafkaOptions>()
                .Bind(configuration.GetSection(KafkaOptions.ConfigSectionPath));

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
}