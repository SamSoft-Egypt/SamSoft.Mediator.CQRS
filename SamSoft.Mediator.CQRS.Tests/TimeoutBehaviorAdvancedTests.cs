using Microsoft.Extensions.DependencyInjection;
using SamSoft.Common.Results;
using SamSoft.Mediator.CQRS.Abstractions;
using SamSoft.Mediator.CQRS.Abstractions.Requests;
using SamSoft.Mediator.CQRS.Pipelines;
using SamSoft.Mediator.CQRS.Tests.TestObjects;

namespace SamSoft.Mediator.CQRS.Tests;

[Collection(nameof(NonParallelCollection))]
public class TimeoutBehaviorAdvancedTests
{
    [Fact]
    public async Task FastCommand_WithinTimeout_Succeeds()
    {
        await using var sp = TestServiceFactory.Create(options =>
        {
            options.RegisterTimeoutBehavior = true;
            options.TimeoutSettings.Timeout = TimeSpan.FromSeconds(2);
        });

        var result = await sp.GetRequiredService<IMediator>().Send(new FastCommand(), TestCancel.Token);
        Assert.True(result.IsSuccess);
        Assert.Equal("fast", result.Value);
    }

    [Fact]
    public async Task CallerCancellation_ThrowsOperationCanceled_NotTimeoutException()
    {
        await using var sp = TestServiceFactory.Create(options =>
        {
            options.RegisterTimeoutBehavior = true;
            options.TimeoutSettings.Timeout = TimeSpan.FromSeconds(30);
        });

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        using var linked = TestCancel.CreateLinkedTokenSource(cts.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await sp.GetRequiredService<IMediator>().Send(new CancelableCommand(), linked.Token));
    }

    [Fact]
    public async Task Timeout_StillApplies_WhenInnerBehaviorCallsNextWithoutToken()
    {
        // Timeout (outer) substitutes a linked CTS token. An inner behavior may call next()
        // MediatR-style without args; the pipeline must keep the stage token, not None.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTransient<ICommandHandler<CancelableCommand, string>, CancelableCommandHandler>();
        services.AddTransient<IRequestHandlerBase<CancelableCommand, Result<string>>, CancelableCommandHandler>();
        services.AddMediatorService(options =>
        {
            options.RegisterHandlersFromCallingAssembly = false;
            options.RegisterTimeoutBehavior = false;
            options.AddOpenBehavior(typeof(TimeoutBehavior<,>));
            options.AddOpenBehavior(typeof(NextWithoutTokenBehavior<,>));
            options.TimeoutSettings.Timeout = TimeSpan.FromMilliseconds(100);
        });

        await using var sp = services.BuildServiceProvider();

        await Assert.ThrowsAsync<TimeoutException>(async () =>
            await sp.GetRequiredService<IMediator>()
                .Send(new CancelableCommand(), TestCancel.Token)
                .WaitAsync(TimeSpan.FromSeconds(3), TestCancel.Token));
    }

    [Fact]
    public async Task CallerCancellation_StillApplies_WhenOuterBehaviorCallsNextWithoutToken()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTransient<ICommandHandler<CancelableCommand, string>, CancelableCommandHandler>();
        services.AddTransient<IRequestHandlerBase<CancelableCommand, Result<string>>, CancelableCommandHandler>();
        services.AddMediatorService(options =>
        {
            options.RegisterHandlersFromCallingAssembly = false;
            options.RegisterTimeoutBehavior = true;
            options.TimeoutSettings.Timeout = TimeSpan.FromSeconds(30);
            options.AddOpenBehavior(typeof(NextWithoutTokenBehavior<,>));
        });

        await using var sp = services.BuildServiceProvider();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        using var linked = TestCancel.CreateLinkedTokenSource(cts.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await sp.GetRequiredService<IMediator>()
                .Send(new CancelableCommand(), linked.Token)
                .WaitAsync(TimeSpan.FromSeconds(3), TestCancel.Token));
    }
}

/// <summary>
/// MediatR-style behavior that calls <c>next()</c> and relies on the optional
/// <see cref="HandlerDelegate{TResponse}"/> parameter keeping the current stage token.
/// </summary>
file sealed class NextWithoutTokenBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    public Task<TResponse> Handle(
        TRequest request,
        HandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
        => next();
}
