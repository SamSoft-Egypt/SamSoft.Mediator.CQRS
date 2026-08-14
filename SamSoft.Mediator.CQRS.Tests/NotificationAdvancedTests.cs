using Microsoft.Extensions.DependencyInjection;
using SamSoft.Mediator.CQRS.Abstractions;
using SamSoft.Mediator.CQRS.Handlers.Notifications;
using SamSoft.Mediator.CQRS.Tests.TestObjects;

namespace SamSoft.Mediator.CQRS.Tests;

[Collection(nameof(NonParallelCollection))]
public class NotificationAdvancedTests
{
    [Fact]
    public async Task Sequential_StopsOnFirstHandlerException()
    {
        FailingSequentialHandlerA.Calls = 0;
        FailingSequentialHandlerB.Calls = 0;

        var services = new ServiceCollection();
        services.AddTransient<INotificationHandler<FailingSequentialNotification>, FailingSequentialHandlerA>();
        services.AddTransient<INotificationHandler<FailingSequentialNotification>, FailingSequentialHandlerB>();
        services.AddMediatorService(options =>
        {
            options.DefaultNotificationPublishStrategy = NotificationPublishStrategy.Sequential;
            options.RegisterHandlersFromCallingAssembly = false;
            options.Lifetime = ServiceLifetime.Singleton;
        });

        await using var sp = services.BuildServiceProvider();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await sp.GetRequiredService<IMediator>().Publish(new FailingSequentialNotification(), TestCancel.Token));

        Assert.Equal("first handler failed", ex.Message);
        Assert.Equal(1, FailingSequentialHandlerA.Calls);
        Assert.Equal(0, FailingSequentialHandlerB.Calls);
    }

    [Fact]
    public async Task Parallel_AggregatesHandlerExceptions()
    {
        FailingParallelHandlerA.Calls = 0;
        FailingParallelHandlerB.Calls = 0;

        var services = new ServiceCollection();
        services.AddTransient<INotificationHandler<FailingParallelNotification>, FailingParallelHandlerA>();
        services.AddTransient<INotificationHandler<FailingParallelNotification>, FailingParallelHandlerB>();
        services.AddMediatorService(options =>
        {
            options.DefaultNotificationPublishStrategy = NotificationPublishStrategy.Parallel;
            options.RegisterHandlersFromCallingAssembly = false;
            options.Lifetime = ServiceLifetime.Singleton;
        });

        await using var sp = services.BuildServiceProvider();

        var ex = await Assert.ThrowsAsync<AggregateException>(async () =>
            await sp.GetRequiredService<IMediator>().Publish(new FailingParallelNotification(), TestCancel.Token));

        Assert.Equal(1, FailingParallelHandlerA.Calls);
        Assert.Equal(1, FailingParallelHandlerB.Calls);

        var messages = ex.Flatten().InnerExceptions.Select(e => e.Message).ToList();
        Assert.Contains("parallel-A", messages);
        Assert.Contains("parallel-B", messages);
    }

    [Fact]
    public async Task Publish_RespectsCallerCancellation_BeforeHandlersRun()
    {
        var publisher = new StrategyAwareNotificationPublisher(NotificationPublishStrategy.Sequential);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var ran = false;
        var executors = new[]
        {
            new NotificationHandlerExecutor((_, _) =>
            {
                ran = true;
                return Task.CompletedTask;
            })
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await publisher.Publish(executors, new EmptyNotification(), cts.Token));

        Assert.False(ran);
    }

    [Fact]
    public async Task Parallel_SurfacesCancellation_WhenHandlersCanceled()
    {
        var publisher = new StrategyAwareNotificationPublisher(NotificationPublishStrategy.Parallel);
        var canceled = new CancellationToken(canceled: true);

        var executors = new[]
        {
            new NotificationHandlerExecutor((_, _) => Task.FromCanceled(canceled)),
            new NotificationHandlerExecutor((_, _) => Task.FromCanceled(canceled))
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await publisher.Publish(executors, new EmptyNotification(), CancellationToken.None));
    }

    [Fact]
    public async Task Attribute_Parallel_Overrides_DefaultSequential()
    {
        var started = 0;
        var bothStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var publisher = new StrategyAwareNotificationPublisher(NotificationPublishStrategy.Sequential);

        Task Handle()
        {
            if (Interlocked.Increment(ref started) == 2)
            {
                bothStarted.TrySetResult();
            }

            return release.Task;
        }

        var publish = publisher.Publish(
            [
                new NotificationHandlerExecutor((_, _) => Handle()),
                new NotificationHandlerExecutor((_, _) => Handle())
            ],
            new ParallelAttributedNotification(),
            TestCancel.Token);

        await bothStarted.Task.WaitAsync(TimeSpan.FromSeconds(2), TestCancel.Token);
        release.TrySetResult();
        await publish;
    }

    [Fact]
    public async Task Parallel_SyncThrow_DoesNotAbandonAlreadyStartedHandlers()
    {
        // Handler A starts async work. Handler B throws synchronously when invoked.
        // Publish must still await A (so side effects finish) and surface B's exception —
        // not abandon A's task mid-flight.
        var publisher = new StrategyAwareNotificationPublisher(NotificationPublishStrategy.Parallel);
        var handlerAFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowAToFinish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var executors = new[]
        {
            new NotificationHandlerExecutor(async (_, _) =>
            {
                await allowAToFinish.Task.ConfigureAwait(false);
                handlerAFinished.TrySetResult();
            }),
            new NotificationHandlerExecutor((_, _) =>
                throw new InvalidOperationException("sync-boom"))
        };

        var publishTask = publisher.Publish(executors, new EmptyNotification(), TestCancel.Token);

        // Give the publisher a moment to start A and hit B's sync throw.
        await Task.Delay(50, TestCancel.Token);
        Assert.False(publishTask.IsCompleted, "Publish must still be awaiting handler A");

        allowAToFinish.TrySetResult();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => publishTask);
        Assert.Equal("sync-boom", ex.Message);
        await handlerAFinished.Task.WaitAsync(TimeSpan.FromSeconds(2), TestCancel.Token);
    }

    [Fact]
    public async Task Parallel_SyncThrow_AggregatesWithAsyncFaults()
    {
        var publisher = new StrategyAwareNotificationPublisher(NotificationPublishStrategy.Parallel);

        var executors = new[]
        {
            new NotificationHandlerExecutor((_, _) =>
                Task.FromException(new InvalidOperationException("async-fault"))),
            new NotificationHandlerExecutor((_, _) =>
                throw new InvalidOperationException("sync-boom"))
        };

        var ex = await Assert.ThrowsAsync<AggregateException>(async () =>
            await publisher.Publish(executors, new EmptyNotification(), TestCancel.Token));

        var messages = ex.Flatten().InnerExceptions.Select(e => e.Message).ToList();
        Assert.Contains("async-fault", messages);
        Assert.Contains("sync-boom", messages);
    }
}

[NotificationPublishStrategy(NotificationPublishStrategy.Parallel)]
file sealed record ParallelAttributedNotification : INotification;
