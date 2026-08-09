using Microsoft.Extensions.DependencyInjection;
using SamSoft.Mediator.CQRS.Abstractions;
using SamSoft.Mediator.CQRS.Handlers.Notifications;
using Xunit;

namespace SamSoft.Mediator.CQRS.Tests;

[NotificationPublishStrategy(NotificationPublishStrategy.Sequential)]
file sealed record SequentialNote : INotification;

[NotificationPublishStrategy(NotificationPublishStrategy.Parallel)]
file sealed record ParallelNote : INotification;

file sealed record DefaultNote : INotification;

[Collection(nameof(NonParallelCollection))]
public class NotificationPublishStrategyTests
{
    [Fact]
    public async Task Sequential_Publisher_InvokesHandlersOneAfterAnother()
    {
        var log = new List<string>();
        var publisher = new StrategyAwareNotificationPublisher(NotificationPublishStrategy.Parallel);

        var executors = new[]
        {
            new NotificationHandlerExecutor(async (_, ct) =>
            {
                log.Add("A:start");
                await Task.Delay(50, ct);
                log.Add("A:end");
            }),
            new NotificationHandlerExecutor((_, _) =>
            {
                log.Add("B");
                return Task.CompletedTask;
            })
        };

        await publisher.Publish(executors, new SequentialNote(), TestCancel.Token);

        Assert.Equal(["A:start", "A:end", "B"], log);
    }

    [Fact]
    public async Task Parallel_Publisher_AllowsHandlersToOverlap()
    {
        var started = 0;
        var bothStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var log = new List<string>();
        var gate = new object();

        var publisher = new StrategyAwareNotificationPublisher(NotificationPublishStrategy.Sequential);

        Task Handle(string name)
        {
            if (Interlocked.Increment(ref started) == 2)
            {
                bothStarted.TrySetResult();
            }

            return AwaitRelease(name);
        }

        async Task AwaitRelease(string name)
        {
            await release.Task;
            lock (gate)
            {
                log.Add(name);
            }
        }

        var executors = new[]
        {
            new NotificationHandlerExecutor((_, _) => Handle("A")),
            new NotificationHandlerExecutor((_, _) => Handle("B"))
        };

        var publishTask = publisher.Publish(executors, new ParallelNote(), TestCancel.Token);

        await bothStarted.Task.WaitAsync(TimeSpan.FromSeconds(2), TestCancel.Token);
        release.TrySetResult();
        await publishTask;

        Assert.Contains("A", log);
        Assert.Contains("B", log);
    }

    [Fact]
    public async Task Default_Strategy_Is_Parallel_When_No_Attribute()
    {
        var started = 0;
        var bothStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var publisher = new StrategyAwareNotificationPublisher(NotificationPublishStrategy.Parallel);

        Task Handle()
        {
            if (Interlocked.Increment(ref started) == 2)
            {
                bothStarted.TrySetResult();
            }

            return release.Task;
        }

        var executors = new[]
        {
            new NotificationHandlerExecutor((_, _) => Handle()),
            new NotificationHandlerExecutor((_, _) => Handle())
        };

        var publishTask = publisher.Publish(executors, new DefaultNote(), TestCancel.Token);

        await bothStarted.Task.WaitAsync(TimeSpan.FromSeconds(2), TestCancel.Token);
        release.TrySetResult();
        await publishTask;
    }

    [Fact]
    public async Task Mediator_Publish_Honors_Sequential_Attribute()
    {
        var services = new ServiceCollection();
        services.AddTransient<INotificationHandler<IntegrationSequentialNotification>, IntegrationSequentialHandlerA>();
        services.AddTransient<INotificationHandler<IntegrationSequentialNotification>, IntegrationSequentialHandlerB>();
        services.AddMediatorService(options =>
        {
            options.Lifetime = ServiceLifetime.Singleton;
            options.RegisterHandlersFromCallingAssembly = false;
        });

        await using var sp = services.BuildServiceProvider();
        IntegrationSequentialHandlerA.Log.Clear();

        await sp.GetRequiredService<IMediator>().Publish(new IntegrationSequentialNotification(), TestCancel.Token);

        Assert.Equal(["A:start", "A:end", "B"], IntegrationSequentialHandlerA.Log);
    }
}

[NotificationPublishStrategy(NotificationPublishStrategy.Sequential)]
public sealed record IntegrationSequentialNotification : INotification;

public sealed class IntegrationSequentialHandlerA : INotificationHandler<IntegrationSequentialNotification>
{
    public static List<string> Log { get; } = [];

    public async Task Handle(IntegrationSequentialNotification notification, CancellationToken cancellationToken = default)
    {
        Log.Add("A:start");
        await Task.Delay(40, cancellationToken);
        Log.Add("A:end");
    }
}

public sealed class IntegrationSequentialHandlerB : INotificationHandler<IntegrationSequentialNotification>
{
    public Task Handle(IntegrationSequentialNotification notification, CancellationToken cancellationToken = default)
    {
        IntegrationSequentialHandlerA.Log.Add("B");
        return Task.CompletedTask;
    }
}
