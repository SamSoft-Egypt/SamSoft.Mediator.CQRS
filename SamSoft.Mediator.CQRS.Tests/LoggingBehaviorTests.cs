using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SamSoft.Mediator.CQRS.Abstractions;
using SamSoft.Mediator.CQRS.Pipelines;
using SamSoft.Mediator.CQRS.Tests.TestObjects;

namespace SamSoft.Mediator.CQRS.Tests;

[Collection(nameof(NonParallelCollection))]
public class LoggingBehaviorTests
{
    [Fact]
    public async Task AdvancedLoggingBehavior_LogsRequestAndResponse_OnSuccess()
    {
        var sink = new CollectingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(b =>
        {
            b.ClearProviders();
            b.AddProvider(sink);
            b.SetMinimumLevel(LogLevel.Information);
        });
        services.AddMediatorService(options =>
        {
            options.RegisterServicesFromAssembly(typeof(FastCommand).Assembly);
            options.AddOpenBehavior(typeof(AdvancedLoggingBehavior<,>));
            options.RegisterTimeoutBehavior = false;
            options.RegisterValidationBehavior = false;
        });

        await using var sp = services.BuildServiceProvider();
        var result = await sp.GetRequiredService<IMediator>().Send(new FastCommand(), TestCancel.Token);

        Assert.True(result.IsSuccess);
        Assert.Contains(sink.Entries, e => e.Contains("Handling request", StringComparison.Ordinal));
        Assert.Contains(sink.Entries, e => e.Contains("Handled request", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AdvancedLoggingBehavior_LogsError_AndRethrows()
    {
        var sink = new CollectingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(b =>
        {
            b.ClearProviders();
            b.AddProvider(sink);
            b.SetMinimumLevel(LogLevel.Information);
        });
        services.AddMediatorService(options =>
        {
            options.RegisterServicesFromAssembly(typeof(ThrowingTestCommand).Assembly);
            options.AddOpenBehavior(typeof(AdvancedLoggingBehavior<,>));
        });

        await using var sp = services.BuildServiceProvider();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await sp.GetRequiredService<IMediator>().Send(new ThrowingTestCommand(), TestCancel.Token));

        Assert.Contains(sink.Entries, e => e.Contains("Exception handling request", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoggingPipelineBehavior_LogsFailure_ForValidationResult()
    {
        var sink = new CollectingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(b =>
        {
            b.ClearProviders();
            b.AddProvider(sink);
            b.SetMinimumLevel(LogLevel.Information);
        });
        services.AddMediatorService(options =>
        {
            options.RegisterServicesFromAssembly(typeof(TestCommand).Assembly);
            options.RegisterValidationBehavior = true;
            options.AddOpenBehavior(typeof(LoggingPipelineBehavior<,>));
        });

        await using var sp = services.BuildServiceProvider();
        var result = await sp.GetRequiredService<IMediator>().Send(new TestCommand("fail"), TestCancel.Token);

        Assert.True(result.IsFailure);
        Assert.Contains(sink.Entries, e => e.Contains("Request failure", StringComparison.Ordinal));
    }
}

internal sealed class CollectingLoggerProvider : ILoggerProvider
{
    private readonly List<string> _entries = [];
    private readonly object _gate = new();

    public IReadOnlyList<string> Entries
    {
        get
        {
            lock (_gate)
            {
                return _entries.ToList();
            }
        }
    }

    public ILogger CreateLogger(string categoryName) => new CollectingLogger(this);

    public void Dispose()
    {
    }

    internal void Add(string message)
    {
        lock (_gate)
        {
            _entries.Add(message);
        }
    }

    private sealed class CollectingLogger(CollectingLoggerProvider owner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            owner.Add(formatter(state, exception));
        }
    }
}
