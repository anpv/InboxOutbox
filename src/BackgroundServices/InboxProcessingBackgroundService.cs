using System.Collections.Concurrent;
using System.Threading.Channels;
using Confluent.Kafka;
using InboxOutbox.Contracts;
using InboxOutbox.Entities;
using InboxOutbox.Implementations;
using InboxOutbox.Options;
using Microsoft.Extensions.Options;

namespace InboxOutbox.BackgroundServices;

public sealed class InboxProcessingBackgroundService(
    IServiceScopeFactory serviceScopeFactory,
    RawKafkaConsumer consumer,
    IOptions<InboxOptions> options,
    ILogger<InboxProcessingBackgroundService> logger)
    : BackgroundService
{
    private readonly ConcurrentDictionary<TopicPartition, WorkItem> _workItemsByPartition = new();
    private readonly Channel<WorkItem> _completionChannel = Channel.CreateUnbounded<WorkItem>(
        new UnboundedChannelOptions { SingleReader = true });

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        await using var registration = stoppingToken.Register(() =>
        {
            consumer.AssignmentsAdded -= OnAdded;
            consumer.AssignmentsRemoved -= OnRemoved;
            OnTopicPartitionsRemoved(_workItemsByPartition.Keys.ToArray());
            _completionChannel.Writer.Complete();
        });

        consumer.AssignmentsAdded += OnAdded;
        consumer.AssignmentsRemoved += OnRemoved;
        OnTopicPartitionsAdded(consumer.Assignments, CreateWorkItem);

        await WaitForWorkItemsCompleted();

        return;

        WorkItem CreateWorkItem(TopicPartition topicPartition)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var task = ProcessAsync(topicPartition, cts.Token);

            return new WorkItem(task, cts);
        }

        void OnAdded(object? sender, IReadOnlyCollection<TopicPartition> partitions)
        {
            OnTopicPartitionsAdded(partitions, CreateWorkItem);
        }

        void OnRemoved(object? sender, IReadOnlyCollection<TopicPartition> partitions)
        {
            OnTopicPartitionsRemoved(partitions);
        }
    }

    private async Task ProcessAsync(TopicPartition topicPartition, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var messages = await GetPendingMessagesAsync(topicPartition, token);

                if (messages.Count == 0)
                {
                    await Task.Delay(options.Value.EmptyDelay, token);
                    continue;
                }
                
                foreach (var message in messages)
                {
                    await ProcessAsync(message, token);
                }
            }
            catch (OperationCanceledException e) when (e.CancellationToken == token)
            {
                // Ignore
            }
            catch (Exception e)
            {
                logger.LogError(e, "Inbox processing failed");
                await Task.Delay(options.Value.ErrorDelay, token);
            }
        }
    }

    private async Task ProcessAsync(InboxMessage message, CancellationToken token)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var inboxStorage = scope.ServiceProvider.GetRequiredService<InboxStorage>();

        try
        {
            var inboxConsumer = scope.ServiceProvider.GetRequiredKeyedService<IInboxConsumer>(message.Topic);
            await inboxConsumer.ConsumeAsync(message, token);
            await inboxStorage.MarkAsReceivedAsync(message.Id, CancellationToken.None);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            try
            {
                await inboxStorage.MarkAsFailedAsync(message.Id, CancellationToken.None);
            }
            catch (Exception exception)
            {
                throw new AggregateException(e, exception);
            }

            throw;
        }
    }

    private async Task<IReadOnlyCollection<InboxMessage>> GetPendingMessagesAsync(
        TopicPartition topicPartition,
        CancellationToken token)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var inboxStorage = scope.ServiceProvider.GetRequiredService<InboxStorage>();

        return await inboxStorage.GetPendingAsync(
            topicPartition.Topic,
            topicPartition.Partition,
            options.Value.BatchSize,
            token);
    }

    private async Task WaitForWorkItemsCompleted()
    {
        await foreach (var workItem in _completionChannel.Reader.ReadAllAsync(CancellationToken.None))
        {
            using var cts = workItem.TokenSource;
            await cts.CancelAsync();
            await workItem.ProcessTask;
        }
    }

    private void OnTopicPartitionsAdded(
        IReadOnlyCollection<TopicPartition> partitions,
        Func<TopicPartition, WorkItem> workItemFactory)
    {
        foreach (var topicPartition in partitions)
        {
            _ = _workItemsByPartition.GetOrAdd(topicPartition, workItemFactory);
        }
    }

    private void OnTopicPartitionsRemoved(IReadOnlyCollection<TopicPartition> partitions)
    {
        foreach (var topicPartition in partitions)
        {
            if (_workItemsByPartition.TryRemove(topicPartition, out var workItem))
            {
                _completionChannel.Writer.TryWrite(workItem);
            }
        }
    }

    private sealed record WorkItem(Task ProcessTask, CancellationTokenSource TokenSource);
}