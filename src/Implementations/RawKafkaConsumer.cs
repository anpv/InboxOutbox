using System.Collections.Immutable;
using System.Threading.Tasks.Dataflow;
using Confluent.Kafka;
using InboxOutbox.Options;
using Microsoft.Extensions.Options;
using RawConsumer = Confluent.Kafka.IConsumer<byte[]?, byte[]?>;
using RawConsumeResult = Confluent.Kafka.ConsumeResult<byte[]?, byte[]?>;

namespace InboxOutbox.Implementations;

public sealed class RawKafkaConsumer : IAsyncDisposable
{
    private readonly ILogger<RawKafkaConsumer> _logger;
    private readonly CancellationToken _stoppingToken;
    private readonly RawConsumer _consumer;
    private readonly BufferBlock<ITask> _channel = new();
    private readonly ConsumeSession _session = new();
    private readonly Task _consumeTask;
    private ImmutableHashSet<TopicPartition> _assignments = ImmutableHashSet<TopicPartition>.Empty;

    public RawKafkaConsumer(
        IEnumerable<string> topics,
        IHostApplicationLifetime appLifetime,
        IOptions<KafkaOptions> options,
        ILogger<RawKafkaConsumer> logger)
    {
        _logger = logger;
        _stoppingToken = appLifetime.ApplicationStopping;
        _consumer = CreateConsumer(options.Value, topics);
        _consumeTask = Task.Factory.StartNew(Consume, state: this, TaskCreationOptions.LongRunning);
    }

    public event EventHandler<IReadOnlyCollection<TopicPartition>>? AssignmentsAdded;

    public event EventHandler<IReadOnlyCollection<TopicPartition>>? AssignmentsRemoved;

    public IReadOnlySet<TopicPartition> Assignments => _assignments;

    public async Task<RawConsumeResult> ConsumeAsync(CancellationToken token)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(_stoppingToken, token);
        var task = new ConsumeTask(_consumer, cts.Token);
        _ = await _channel.SendAsync(task, cts.Token);
        var consumeResult = await task.Task;
        _session.Add(consumeResult);

        return consumeResult;
    }

    public async Task<IReadOnlyCollection<RawConsumeResult>> ConsumeBatchAsync(
        int batchSize,
        TimeSpan batchTimeout,
        CancellationToken token)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(_stoppingToken, token);
        var task = new ConsumeBatchTask(_consumer, batchSize, batchTimeout, cts.Token);
        _ = await _channel.SendAsync(task, cts.Token);
        var consumeResults = await task.Task;

        if (consumeResults.Count > 0)
        {
            _session.Add(consumeResults[^1]);
        }

        return consumeResults;
    }

    public async Task CommitAsync(CancellationToken token)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(_stoppingToken, token);
        var task = new CommitTask(_consumer, _session, cts.Token);
        _ = await _channel.SendAsync(task, cts.Token);
        await task.Task;
    }

    public async Task ResetAsync(CancellationToken token)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(_stoppingToken, token);
        var task = new ResetTask(_consumer, _session, cts.Token);
        _ = await _channel.SendAsync(task, cts.Token);
        await task.Task;
    }

    public async ValueTask DisposeAsync()
    {
        _channel.Complete();
        await _consumeTask;
        _consumer.Close();
        _consumer.Dispose();
    }

    private static void Consume(object? obj)
    {
        ArgumentNullException.ThrowIfNull(obj);
        var self = (RawKafkaConsumer)obj;

        while (!self._stoppingToken.IsCancellationRequested)
        {
            ITask? task = null;

            try
            {
                task = self._channel.Receive(); // Will fail when calling Complete on Dispose
                task.Run();
            }
            catch (Exception e)
            {
                task?.TrySetException(e);
            }
        }
    }

    private RawConsumer CreateConsumer(KafkaOptions options, IEnumerable<string> topics)
    {
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = options.BootstrapServers,
            GroupId = options.Consumer.GroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            PartitionAssignmentStrategy = PartitionAssignmentStrategy.CooperativeSticky,
        };

        var consumer = new ConsumerBuilder<byte[]?, byte[]?>(consumerConfig)
            .SetLogHandler((_, message) => KafkaLogger.Log(_logger, message))
            .SetPartitionsAssignedHandler((_, partitions) =>
            {
                if (partitions.Count == 0)
                {
                    return;
                }

                _assignments = _assignments.Union(partitions);
                AssignmentsAdded?.Invoke(this, partitions);
            })
            .SetPartitionsRevokedHandler((_, partitionOffsets) =>
            {
                if (partitionOffsets.Count == 0)
                {
                    return;
                }

                var partitions = partitionOffsets.Select(x => x.TopicPartition).ToArray();
                _assignments = _assignments.Except(partitions);
                AssignmentsRemoved?.Invoke(this, partitions);
            })
            .Build();

        consumer.Subscribe(topics.ToArray());
        
        return consumer;
    }

    private interface ITask
    {
        void Run();

        bool TrySetException(Exception exception);
    }

    private sealed class ConsumeTask(RawConsumer consumer, CancellationToken token)
        : TaskCompletionSource<RawConsumeResult>, ITask
    {
        public void Run()
        {
            var consumeResult = consumer.Consume(token);
            TrySetResult(consumeResult);
        }
    }

    private sealed class ConsumeBatchTask(
        RawConsumer consumer,
        int batchSize,
        TimeSpan batchTimeout,
        CancellationToken token)
        : TaskCompletionSource<IReadOnlyList<RawConsumeResult>>, ITask
    {
        public void Run()
        {
            var consumeResults = new List<RawConsumeResult>();
            using var timeoutCts = new CancellationTokenSource(batchTimeout);
            using var mergedCts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);

            while (!mergedCts.IsCancellationRequested && consumeResults.Count < batchSize)
            {
                try
                {
                    var consumeResult = consumer.Consume(mergedCts.Token);
                    consumeResults.Add(consumeResult);
                }
                catch (OperationCanceledException e) when (e.CancellationToken == mergedCts.Token)
                {
                    // Ignore
                }
            }

            TrySetResult(consumeResults);
        }
    }

    private sealed class CommitTask(RawConsumer consumer, ConsumeSession session, CancellationToken token)
        : TaskCompletionSource, ITask
    {
        public void Run()
        {
            if (!token.IsCancellationRequested && session.LastResult is { } lastResult)
            {
                consumer.Commit(lastResult);
                session.Reset();
            }

            TrySetResult();
        }
    }

    private sealed class ResetTask(RawConsumer consumer, ConsumeSession session, CancellationToken token)
        : TaskCompletionSource, ITask
    {
        public void Run()
        {
            if (!token.IsCancellationRequested && session.FirstResult is { } firstResult)
            {
                consumer.Seek(firstResult.TopicPartitionOffset);
                session.Reset();
            }

            TrySetResult();
        }
    }

    private sealed class ConsumeSession
    {
        public RawConsumeResult? FirstResult { get; private set; }

        public RawConsumeResult? LastResult { get; private set; }

        public void Add(RawConsumeResult result)
        {
            FirstResult ??= result;
            LastResult = result;
        }

        public void Reset()
        {
            FirstResult = null;
            LastResult = null;
        }
    }
}