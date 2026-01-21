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
    private readonly string _topic;
    private readonly KafkaOptions _options;
    private readonly ILogger<RawKafkaConsumer> _logger;
    private readonly CancellationToken _stoppingToken;
    private readonly BufferBlock<ITask> _channel = new();
    private readonly Task _runTask;
    private ImmutableHashSet<TopicPartition> _assignments = ImmutableHashSet<TopicPartition>.Empty;

    public RawKafkaConsumer(
        string topic,
        IHostApplicationLifetime appLifetime,
        IOptions<KafkaOptions> options,
        ILogger<RawKafkaConsumer> logger)
    {
        _topic = topic;
        _options = options.Value;
        _logger = logger;
        _stoppingToken = appLifetime.ApplicationStopping;
        _runTask = Task.Factory.StartNew(Run, TaskCreationOptions.LongRunning);
    }

    public event EventHandler<IReadOnlyCollection<TopicPartition>>? AssignmentsAdded;

    public event EventHandler<IReadOnlyCollection<TopicPartition>>? AssignmentsRemoved;

    public IReadOnlySet<TopicPartition> Assignments => _assignments;

    public async Task<RawConsumeResult> ConsumeAsync(CancellationToken token)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(_stoppingToken, token);
        var task = new ConsumeTask(cts.Token);
        _ = await _channel.SendAsync(task, cts.Token);

        return await task.Task;
    }

    public async Task<IReadOnlyCollection<RawConsumeResult>> ConsumeBatchAsync(
        int batchSize,
        TimeSpan batchTimeout,
        CancellationToken token)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(_stoppingToken, token);
        var task = new ConsumeBatchTask(batchSize, batchTimeout, cts.Token);
        _ = await _channel.SendAsync(task, cts.Token);

        return await task.Task;
    }

    public async Task CommitAsync(CancellationToken token)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(_stoppingToken, token);
        var task = new CommitTask(cts.Token);
        _ = await _channel.SendAsync(task, cts.Token);
        await task.Task;
    }

    public async Task ResetAsync(CancellationToken token)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(_stoppingToken, token);
        var task = new ResetTask(cts.Token);
        _ = await _channel.SendAsync(task, cts.Token);
        await task.Task;
    }

    public async ValueTask DisposeAsync()
    {
        _channel.Complete();
        await _runTask;
    }

    private void Run()
    {
        RawConsumer? consumer = null;
        ConsumeSession? session = null;
        
        while (!_stoppingToken.IsCancellationRequested)
        {
            ITask? task = null;

            try
            {
                task = _channel.Receive(_stoppingToken);
                consumer ??= CreateConsumer();
                session ??= new ConsumeSession();
                task.Run(consumer, session);
            }
            catch (Exception e)
            {
                task?.TrySetException(e);
            }
        }
        
        consumer?.Close();
        consumer?.Dispose();
    }

    private RawConsumer CreateConsumer()
    {
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = _options.Consumer.GroupId,
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

        consumer.Subscribe(_topic);

        return consumer;
    }

    private interface ITask
    {
        void Run(RawConsumer consumer, ConsumeSession session);

        bool TrySetException(Exception exception);
    }

    private sealed class ConsumeTask(CancellationToken token) : TaskCompletionSource<RawConsumeResult>, ITask
    {
        public void Run(RawConsumer consumer, ConsumeSession session)
        {
            var result = consumer.Consume(token);
            session.Add(result);
            TrySetResult(result);
        }
    }

    private sealed class ConsumeBatchTask(int batchSize, TimeSpan batchTimeout, CancellationToken token)
        : TaskCompletionSource<IReadOnlyList<RawConsumeResult>>, ITask
    {
        public void Run(RawConsumer consumer, ConsumeSession session)
        {
            var consumeResults = new List<RawConsumeResult>();
            using var timeoutCts = new CancellationTokenSource(batchTimeout);
            using var mergedCts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);

            while (!mergedCts.IsCancellationRequested && consumeResults.Count < batchSize)
            {
                try
                {
                    var result = consumer.Consume(mergedCts.Token);
                    session.Add(result);
                    consumeResults.Add(result);
                }
                catch (OperationCanceledException)
                    when (timeoutCts.IsCancellationRequested && !token.IsCancellationRequested)
                {
                    // Ignore, batch timeout
                    break;
                }
            }

            TrySetResult(consumeResults);
        }
    }

    private sealed class CommitTask(CancellationToken token) : TaskCompletionSource, ITask
    {
        public void Run(RawConsumer consumer, ConsumeSession session)
        {
            if (!token.IsCancellationRequested)
            {
                var offsets = session.GetCommitOffsets();
                consumer.Commit(offsets);
                session.Reset();
            }

            TrySetResult();
        }
    }

    private sealed class ResetTask(CancellationToken token) : TaskCompletionSource, ITask
    {
        public void Run(RawConsumer consumer, ConsumeSession session)
        {
            if (!token.IsCancellationRequested)
            {
                foreach (var offset in session.GetResetOffsets())
                {
                    consumer.Seek(offset);
                }

                session.Reset();
            }

            TrySetResult();
        }
    }
    
    private sealed class ConsumeSession
    {
        private readonly Dictionary<Partition, Offsets> _offsetsByPartition = new();

        public void Add(RawConsumeResult result)
        {
            if (_offsetsByPartition.TryGetValue(result.Partition, out var offsets))
            {
                offsets.Max = result.TopicPartitionOffset;
            }
            else
            {
                var offset = result.TopicPartitionOffset;
                _offsetsByPartition.Add(result.Partition, new Offsets { Min = offset, Max = offset });
            }
        }

        public void Reset() => _offsetsByPartition.Clear();

        public IReadOnlyCollection<TopicPartitionOffset> GetCommitOffsets() =>
            _offsetsByPartition.Select(x => x.Value.Max).ToArray();

        public IReadOnlyCollection<TopicPartitionOffset> GetResetOffsets() =>
            _offsetsByPartition.Select(x => x.Value.Min).ToArray();

        private sealed class Offsets
        {
            public required TopicPartitionOffset Min { get; init; }

            public required TopicPartitionOffset Max { get; set; }
        }
    }
}