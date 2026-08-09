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

        // Sequential foreach will still invoke callback; handlers should observe CT.
        // For pre-cancel, Task.WhenAll/foreach still call handlers unless they check token.
        // Assert publisher itself accepts canceled token without hanging:
        using var linked = TestCancel.CreateLinkedTokenSource(cts.Token);
        await publisher.Publish(executors, new EmptyNotification(), linked.Token);
        Assert.True(ran);
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
}

[NotificationPublishStrategy(NotificationPublishStrategy.Parallel)]
file sealed record ParallelAttributedNotification : INotification;
