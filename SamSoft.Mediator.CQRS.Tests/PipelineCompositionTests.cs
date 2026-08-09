using Microsoft.Extensions.DependencyInjection;
using SamSoft.Common.Results;
using SamSoft.Mediator.CQRS.Abstractions;
using SamSoft.Mediator.CQRS.Tests.TestObjects;

namespace SamSoft.Mediator.CQRS.Tests;

[Collection(nameof(NonParallelCollection))]
public class PipelineCompositionTests
{
    [Fact]
    public async Task LastRegisteredBehavior_IsOutermost()
    {
        BehaviorOrder.Reset();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediatorService(options =>
        {
            options.RegisterServicesFromAssembly(typeof(FastCommand).Assembly);
            options.RegisterTimeoutBehavior = false;
            options.RegisterPrePostProcessorBehavior = false;
            options.RegisterValidationBehavior = false;
            options.AddOpenBehavior(typeof(OuterProbeBehavior<,>));
            options.AddOpenBehavior(typeof(InnerProbeBehavior<,>));
        });

        await using var sp = services.BuildServiceProvider();
        await sp.GetRequiredService<IMediator>().Send(new FastCommand(), TestCancel.Token);

        // Registration: Outer then Inner.
        // Reverse + Aggregate makes the first registered behavior outermost:
        // Outer enter → Inner enter → handler → Inner exit → Outer exit
        Assert.Equal(
            ["outer:enter", "inner:enter", "inner:exit", "outer:exit"],
            BehaviorOrder.Steps);
    }
}

internal static class BehaviorOrder
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

public sealed class OuterProbeBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        HandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        BehaviorOrder.Add("outer:enter");
        var response = await next(cancellationToken);
        BehaviorOrder.Add("outer:exit");
        return response;
    }
}

public sealed class InnerProbeBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        HandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        BehaviorOrder.Add("inner:enter");
        var response = await next(cancellationToken);
        BehaviorOrder.Add("inner:exit");
        return response;
    }
}
