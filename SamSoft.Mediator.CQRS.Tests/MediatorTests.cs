using Microsoft.Extensions.DependencyInjection;
using SamSoft.Mediator.CQRS.Abstractions;
using SamSoft.Mediator.CQRS.Tests.TestObjects;

namespace SamSoft.Mediator.CQRS.Tests;

[Collection(nameof(NonParallelCollection))]
public class MediatorTests : MediatorTestsBase
{
    [Fact]
    public async Task CommandHandler_ReturnsExpectedResult()
    {
        var sp = BuildServices();
        var mediator = sp.GetRequiredService<IMediator>();
        var result = await mediator.Send(new TestCommand("foo"), TestCancel.Token);
        Assert.True(result.IsSuccess);
        Assert.Equal("foo_handled", result.Value);
    }

    [Fact]
    public async Task QueryHandler_ReturnsExpectedResult()
    {
        var sp = BuildServices();
        var mediator = sp.GetRequiredService<IMediator>();
        var result = await mediator.Send(new TestQuery("bar"), TestCancel.Token);
        Assert.True(result.IsSuccess);
        Assert.Equal("bar_queried", result.Value);
    }

    [Fact]
    public async Task PipelineBehaviors_AreInvoked()
    {
        PipelineTracker.ValidationWasCalled = false;
        PipelineTracker.LoggingWasCalled = false;
        var sp = BuildServices();
        var mediator = sp.GetRequiredService<IMediator>();
        await mediator.Send(new TestCommand("foo"), TestCancel.Token);
        Assert.True(PipelineTracker.ValidationWasCalled);
        Assert.True(PipelineTracker.LoggingWasCalled);
    }

    [Fact]
    public async Task NotificationHandlers_AreAllInvoked()
    {
        TestNotificationHandlerA.Received.Clear();
        TestNotificationHandlerB.Received.Clear();
        var sp = BuildServices();
        var mediator = sp.GetRequiredService<IMediator>();
        await mediator.Publish(new TestNotification("notify"), TestCancel.Token);
        Assert.Contains("A:notify", TestNotificationHandlerA.Received);
        Assert.Contains("B:notify", TestNotificationHandlerB.Received);
    }
}
