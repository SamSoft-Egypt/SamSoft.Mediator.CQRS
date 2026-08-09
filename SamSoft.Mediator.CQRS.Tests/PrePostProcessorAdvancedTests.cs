using Microsoft.Extensions.DependencyInjection;
using SamSoft.Common.Results;
using SamSoft.Mediator.CQRS.Abstractions;
using SamSoft.Mediator.CQRS.Tests.TestObjects;

namespace SamSoft.Mediator.CQRS.Tests;

[Collection(nameof(NonParallelCollection))]
public class PrePostProcessorAdvancedTests
{
    [Fact]
    public async Task MultiplePreProcessors_RunInRegistrationOrder_BeforeHandler()
    {
        ProcessorOrder.Reset();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTransient(typeof(IRequestPreProcessor<>), typeof(OrderedPreProcessor1<>));
        services.AddTransient(typeof(IRequestPreProcessor<>), typeof(OrderedPreProcessor2<>));
        services.AddTransient(typeof(IRequestPostProcessor<,>), typeof(OrderedPostProcessor<,>));
        services.AddTransient<ICommandHandler<OrderedFlowCommand, string>, OrderedFlowCommandHandler>();
        services.AddTransient<Abstractions.Requests.IRequestHandlerBase<OrderedFlowCommand, Result<string>>, OrderedFlowCommandHandler>();
        services.AddMediatorService(options =>
        {
            options.RegisterHandlersFromCallingAssembly = false;
            options.RegisterPrePostProcessorBehavior = true;
            options.Lifetime = ServiceLifetime.Singleton;
        });

        await using var sp = services.BuildServiceProvider();
        var result = await sp.GetRequiredService<IMediator>().Send(new OrderedFlowCommand(), TestCancel.Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(["pre1", "pre2", "handler", "post"], ProcessorOrder.Steps);
    }

    [Fact]
    public async Task PostProcessor_IsSkipped_WhenHandlerThrows()
    {
        ProcessorOrder.Reset();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTransient(typeof(IRequestPreProcessor<>), typeof(OrderedPreProcessor1<>));
        services.AddTransient(typeof(IRequestPostProcessor<,>), typeof(OrderedPostProcessor<,>));
        services.AddTransient<ICommandHandler<ThrowingTestCommand>, ThrowingTestCommandHandler>();
        services.AddTransient<Abstractions.Requests.IRequestHandlerBase<ThrowingTestCommand, Result>, ThrowingTestCommandHandler>();
        services.AddMediatorService(options =>
        {
            options.RegisterHandlersFromCallingAssembly = false;
            options.RegisterPrePostProcessorBehavior = true;
            options.Lifetime = ServiceLifetime.Singleton;
        });

        await using var sp = services.BuildServiceProvider();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await sp.GetRequiredService<IMediator>().Send(new ThrowingTestCommand(), TestCancel.Token));

        Assert.Contains("pre1", ProcessorOrder.Steps);
        Assert.DoesNotContain("post", ProcessorOrder.Steps);
    }
}

public sealed record OrderedFlowCommand : ICommand<string>;

public sealed class OrderedFlowCommandHandler : ICommandHandler<OrderedFlowCommand, string>
{
    public Task<Result<string>> Handle(OrderedFlowCommand command, CancellationToken cancellationToken = default)
    {
        ProcessorOrder.Add("handler");
        return Task.FromResult(Result.Success("ok"));
    }
}

internal static class ProcessorOrder
{
    private static readonly object Gate = new();
    private static readonly List<string> Items = [];

    public static IReadOnlyList<string> Steps
    {
        get
        {
            lock (Gate)
            {
                return Items.ToList();
            }
        }
    }

    public static void Reset()
    {
        lock (Gate)
        {
            Items.Clear();
        }
    }

    public static void Add(string step)
    {
        lock (Gate)
        {
            Items.Add(step);
        }
    }
}

internal sealed class OrderedPreProcessor1<TRequest> : IRequestPreProcessor<TRequest>
{
    public Task Process(TRequest request, CancellationToken cancellationToken)
    {
        ProcessorOrder.Add("pre1");
        return Task.CompletedTask;
    }
}

internal sealed class OrderedPreProcessor2<TRequest> : IRequestPreProcessor<TRequest>
{
    public Task Process(TRequest request, CancellationToken cancellationToken)
    {
        ProcessorOrder.Add("pre2");
        return Task.CompletedTask;
    }
}

internal sealed class OrderedPostProcessor<TRequest, TResponse> : IRequestPostProcessor<TRequest, TResponse>
{
    public Task Process(TRequest request, TResponse response, CancellationToken cancellationToken)
    {
        ProcessorOrder.Add("post");
        return Task.CompletedTask;
    }
}
