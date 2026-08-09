using Microsoft.Extensions.DependencyInjection;
using SamSoft.Mediator.CQRS.Abstractions;
using SamSoft.Mediator.CQRS.Tests.TestObjects;

namespace SamSoft.Mediator.CQRS.Tests;

[Collection(nameof(NonParallelCollection))]
public class MediatorCoreTests
{
    [Fact]
    public async Task Send_NonGenericCommand_ReturnsSuccessResult()
    {
        await using var sp = TestServiceFactory.Create();
        var result = await sp.GetRequiredService<IMediator>().Send(new PingCommand(), TestCancel.Token);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Send_NullCommand_ThrowsArgumentNullException()
    {
        await using var sp = TestServiceFactory.Create(scanTestAssembly: false);
        var mediator = sp.GetRequiredService<IMediator>();

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await mediator.Send((ICommand)null!, TestCancel.Token));
    }

    [Fact]
    public async Task Send_NullCommandWithResponse_ThrowsArgumentNullException()
    {
        await using var sp = TestServiceFactory.Create(scanTestAssembly: false);
        var mediator = sp.GetRequiredService<IMediator>();

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await mediator.Send((ICommand<string>)null!, TestCancel.Token));
    }

    [Fact]
    public async Task Send_NullQuery_ThrowsArgumentNullException()
    {
        await using var sp = TestServiceFactory.Create(scanTestAssembly: false);
        var mediator = sp.GetRequiredService<IMediator>();

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await mediator.Send((IQuery<string>)null!, TestCancel.Token));
    }

    [Fact]
    public async Task Publish_NullNotification_ThrowsArgumentNullException()
    {
        await using var sp = TestServiceFactory.Create(scanTestAssembly: false);
        var mediator = sp.GetRequiredService<IMediator>();

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await mediator.Publish<TestNotification>(null!, TestCancel.Token));
    }

    [Fact]
    public async Task Send_MissingHandler_ThrowsInvalidOperationException()
    {
        await using var sp = TestServiceFactory.Create(scanTestAssembly: false);
        var mediator = sp.GetRequiredService<IMediator>();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await mediator.Send(new UnregisteredCommand("x"), TestCancel.Token));
    }

    [Fact]
    public async Task Send_HandlerException_Propagates()
    {
        await using var sp = TestServiceFactory.Create();
        var mediator = sp.GetRequiredService<IMediator>();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await mediator.Send(new ThrowingTestCommand(), TestCancel.Token));

        Assert.Equal("Handler exception", ex.Message);
    }

    [Fact]
    public async Task Send_WhenCallerCancels_ThrowsOperationCanceledException()
    {
        await using var sp = TestServiceFactory.Create(options =>
        {
            options.RegisterTimeoutBehavior = false;
        });
        var mediator = sp.GetRequiredService<IMediator>();

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        using var linked = TestCancel.CreateLinkedTokenSource(cts.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await mediator.Send(new CancelableCommand(), linked.Token));
    }

    [Fact]
    public async Task ISender_And_IPublisher_Resolve_To_Same_Mediator_Instance_When_Singleton()
    {
        await using var sp = TestServiceFactory.Create(options =>
        {
            options.Lifetime = ServiceLifetime.Singleton;
            options.RegisterHandlersFromCallingAssembly = false;
            options.RegisterServicesFromAssembly(typeof(PingCommand).Assembly);
        });

        var mediator = sp.GetRequiredService<IMediator>();
        var sender = sp.GetRequiredService<ISender>();
        var publisher = sp.GetRequiredService<IPublisher>();

        Assert.Same(mediator, sender);
        Assert.Same(mediator, publisher);

        var result = await sender.Send(new PingCommand(), TestCancel.Token);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Publish_WithNoHandlers_Completes()
    {
        await using var sp = TestServiceFactory.Create(scanTestAssembly: false);
        var mediator = sp.GetRequiredService<IMediator>();

        await mediator.Publish(new EmptyNotification(), TestCancel.Token);
    }

    [Fact]
    public async Task Send_SameCommandType_Twice_UsesCachedWrapper()
    {
        await using var sp = TestServiceFactory.Create();
        var mediator = sp.GetRequiredService<IMediator>();

        var first = await mediator.Send(new TestCommand("a"), TestCancel.Token);
        var second = await mediator.Send(new TestCommand("b"), TestCancel.Token);

        Assert.Equal("a_handled", first.Value);
        Assert.Equal("b_handled", second.Value);
    }
}
