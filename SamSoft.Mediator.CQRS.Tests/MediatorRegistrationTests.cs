using Microsoft.Extensions.DependencyInjection;
using SamSoft.Common.Results;
using SamSoft.Mediator.CQRS.Abstractions;
using SamSoft.Mediator.CQRS.Pipelines;
using SamSoft.Mediator.CQRS.Tests.TestObjects;

namespace SamSoft.Mediator.CQRS.Tests;

[Collection(nameof(NonParallelCollection))]
public class MediatorRegistrationTests
{
    [Fact]
    public async Task Lifetime_Scoped_ReturnsSameInstance_WithinScope()
    {
        await using var sp = TestServiceFactory.Create(scanTestAssembly: false);

        using var scope = sp.CreateScope();
        var a = scope.ServiceProvider.GetRequiredService<IMediator>();
        var b = scope.ServiceProvider.GetRequiredService<IMediator>();
        Assert.Same(a, b);

        using var other = sp.CreateScope();
        var c = other.ServiceProvider.GetRequiredService<IMediator>();
        Assert.NotSame(a, c);
    }

    [Fact]
    public async Task Lifetime_Singleton_ReturnsSameInstance_AcrossScopes()
    {
        await using var sp = TestServiceFactory.Create(options =>
        {
            options.Lifetime = ServiceLifetime.Singleton;
        }, scanTestAssembly: false);

        using var scope1 = sp.CreateScope();
        using var scope2 = sp.CreateScope();
        Assert.Same(
            scope1.ServiceProvider.GetRequiredService<IMediator>(),
            scope2.ServiceProvider.GetRequiredService<IMediator>());
    }

    [Fact]
    public async Task Lifetime_Transient_ReturnsNewInstance_EachResolve()
    {
        await using var sp = TestServiceFactory.Create(options =>
        {
            options.Lifetime = ServiceLifetime.Transient;
        }, scanTestAssembly: false);

        Assert.NotSame(sp.GetRequiredService<IMediator>(), sp.GetRequiredService<IMediator>());
    }

    [Fact]
    public async Task ParamsOverload_EnablesTimeoutAndPrePostByDefault()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediatorService(typeof(FastCommand).Assembly);
        await using var sp = services.BuildServiceProvider();

        var behaviors = sp.GetServices<IPipelineBehavior<FastCommand, Result<string>>>()
            .Select(b => b.GetType().GetGenericTypeDefinition())
            .ToList();

        Assert.Contains(typeof(TimeoutBehavior<,>), behaviors);
        Assert.Contains(typeof(PrePostProcessorBehavior<,>), behaviors);
    }

    [Fact]
    public async Task OptionsOverload_DoesNotRegisterBuiltIns_UnlessRequested()
    {
        await using var sp = TestServiceFactory.Create(options =>
        {
            options.RegisterTimeoutBehavior = false;
            options.RegisterPrePostProcessorBehavior = false;
            options.RegisterValidationBehavior = false;
        });

        Assert.Empty(sp.GetServices<IPipelineBehavior<FastCommand, Result<string>>>());
    }

    [Fact]
    public async Task RegisterValidationBehavior_False_AllowsInvalidCommandThrough()
    {
        await using var sp = TestServiceFactory.Create(options =>
        {
            options.RegisterValidationBehavior = false;
        });

        var result = await sp.GetRequiredService<IMediator>().Send(new TestCommand("fail"), TestCancel.Token);
        Assert.True(result.IsSuccess);
        Assert.Equal("fail_handled", result.Value);
    }

    [Fact]
    public void AddOpenBehavior_RejectsClosedGenericType()
    {
        var services = new ServiceCollection();
        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddOpenBehavior(typeof(TimeoutBehavior<FastCommand, Result<string>>)));

        Assert.Contains("open generic", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddOpenBehavior_RejectsTypeThatDoesNotImplementPipelineBehavior()
    {
        var services = new ServiceCollection();
        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddOpenBehavior(typeof(List<>)));

        Assert.Contains("IPipelineBehavior", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ParamsOverload_RegistersMediatorAndHandlers()
    {
        await using var sp = TestServiceFactory.CreateWithAssemblies(typeof(PingCommand).Assembly);
        var result = await sp.GetRequiredService<IMediator>().Send(new PingCommand(), TestCancel.Token);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Options_AddRequestPreProcessor_RegistersProcessor()
    {
        await using var sp = TestServiceFactory.Create(options =>
        {
            options.RegisterPrePostProcessorBehavior = true;
            options.AddRequestPreProcessor(typeof(TestPreProcessor<>));
        });

        var pre = sp.GetServices<IRequestPreProcessor<PingCommand>>().ToList();
        Assert.Contains(pre, p => p.GetType() == typeof(TestPreProcessor<PingCommand>));
    }

    [Fact]
    public async Task Options_AddOpenBehavior_RegistersBehavior()
    {
        await using var sp = TestServiceFactory.Create(options =>
        {
            options.AddOpenBehavior(typeof(AdvancedLoggingBehavior<,>));
        });

        var behaviors = sp.GetServices<IPipelineBehavior<PingCommand, Result>>()
            .Select(b => b.GetType().GetGenericTypeDefinition())
            .ToList();

        Assert.Contains(typeof(AdvancedLoggingBehavior<,>), behaviors);
    }

    [Fact]
    public async Task DefaultNotificationPublishStrategy_Sequential_IsHonored_WithoutAttribute()
    {
        OrderedDefaultHandlerA.Log.Clear();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTransient<INotificationHandler<OrderedDefaultNotification>, OrderedDefaultHandlerA>();
        services.AddTransient<INotificationHandler<OrderedDefaultNotification>, OrderedDefaultHandlerB>();
        services.AddMediatorService(options =>
        {
            options.DefaultNotificationPublishStrategy = NotificationPublishStrategy.Sequential;
            options.RegisterHandlersFromCallingAssembly = false;
            options.Lifetime = ServiceLifetime.Singleton;
        });

        await using var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<IMediator>().Publish(new OrderedDefaultNotification("x"), TestCancel.Token);

        lock (OrderedDefaultHandlerA.Log)
        {
            Assert.Equal(["A", "B"], OrderedDefaultHandlerA.Log);
        }
    }
}
