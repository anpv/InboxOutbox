using System.Text;
using Confluent.Kafka;
using InboxOutbox.Options;
using Microsoft.Extensions.Options;

namespace InboxOutbox.Implementations;

public sealed class RawKafkaProducer : IDisposable
{
    private readonly ILogger<RawKafkaProducer> _logger;
    private readonly bool _isTransactional;
    private readonly IProducer<byte[]?, byte[]?> _producer;
    private readonly Lock _initializeLock = new();
    private volatile bool _isInitialized;

    public RawKafkaProducer(IOptions<KafkaOptions> options, ILogger<RawKafkaProducer> logger)
    {
        _logger = logger;
        _isTransactional = options.Value.IsTransactional;
        var config = new ProducerConfig
        {
            BootstrapServers = options.Value.BootstrapServers,
            EnableIdempotence = true,
            DeliveryReportFields = "none",
            MessageSendMaxRetries = 5,
            TransactionalId = options.Value.TransactionalId,

        };

        _producer = new ProducerBuilder<byte[]?, byte[]?>(config)
            .SetLogHandler(ErrorHandler)
            .Build();
    }

    public async Task ProduceAsync(
        string topic,
        byte[]? key,
        byte[]? value,
        IReadOnlyDictionary<string, string?>? headers,
        CancellationToken token)
    {
        var message = new Message<byte[]?, byte[]?>
        {
            Key = key,
            Value = value,
            Headers = CreateHeaders(headers)
        };

        await _producer.ProduceAsync(topic, message, token);
    }

    public void BeginTransaction()
    {
        if (!_isTransactional)
        {
            return;
        }

        InitTransactions();
        _producer.BeginTransaction();
    }

    public void CommitTransaction()
    {
        if (!_isTransactional)
        {
            return;
        }

        InitTransactions();
        _producer.CommitTransaction();
    }

    public void AbortTransaction()
    {
        if (!_isTransactional)
        {
            return;
        }

        InitTransactions();
        _producer.AbortTransaction();
    }

    public void Dispose()
    {
        _producer.Flush();
        _producer.Dispose();
    }

    private static Headers? CreateHeaders(IReadOnlyDictionary<string, string?>? headers)
    {
        if (headers is null)
        {
            return null;
        }

        var result = new Headers();

        foreach (var (key, value) in headers)
        {
            result.Add(key, value is null ? null : Encoding.UTF8.GetBytes(value));
        }

        return result;
    }

    private void InitTransactions()
    {
        if (_isInitialized)
        {
            return;
        }

        lock (_initializeLock)
        {
            if (_isInitialized)
            {
                return;
            }

            _producer.InitTransactions(TimeSpan.FromSeconds(10));
            _isInitialized = true;
        }
    }

    private void ErrorHandler(IProducer<byte[]?, byte[]?> producer, LogMessage message)
    {
        KafkaLogger.Log(_logger, message);
    }
}