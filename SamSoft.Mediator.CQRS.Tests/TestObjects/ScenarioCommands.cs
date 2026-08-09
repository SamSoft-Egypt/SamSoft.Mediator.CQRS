using SamSoft.Common.Results;
using SamSoft.Mediator.CQRS.Abstractions;

namespace SamSoft.Mediator.CQRS.Tests.TestObjects;

public sealed class PingCommand : ICommand;

public sealed class PingCommandHandler : ICommandHandler<PingCommand>
{
    public Task<Result> Handle(PingCommand command, CancellationToken cancellationToken = default)
        => Task.FromResult(Result.Success());
}

public sealed class CancelableCommand : ICommand<string>;

public sealed class CancelableCommandHandler : ICommandHandler<CancelableCommand, string>
{
    public async Task<Result<string>> Handle(CancelableCommand command, CancellationToken cancellationToken = default)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return Result.Success("should-not-reach");
    }
}

/// <summary>
/// Intentionally has no handler registered when scanning is disabled.
/// </summary>
public sealed record UnregisteredCommand(string Value) : ICommand<string>;

public sealed record FastCommand : ICommand<string>;

public sealed class FastCommandHandler : ICommandHandler<FastCommand, string>
{
    public Task<Result<string>> Handle(FastCommand command, CancellationToken cancellationToken = default)
        => Task.FromResult(Result.Success("fast"));
}

public sealed record EmptyNotification : INotification;

public sealed record FailingSequentialNotification : INotification;

public sealed class FailingSequentialHandlerA : INotificationHandler<FailingSequentialNotification>
{
    public static int Calls;

    public Task Handle(FailingSequentialNotification notification, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref Calls);
        throw new InvalidOperationException("first handler failed");
    }
}

public sealed class FailingSequentialHandlerB : INotificationHandler<FailingSequentialNotification>
{
    public static int Calls;

    public Task Handle(FailingSequentialNotification notification, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref Calls);
        return Task.CompletedTask;
    }
}

public sealed record FailingParallelNotification : INotification;

public sealed class FailingParallelHandlerA : INotificationHandler<FailingParallelNotification>
{
    public static int Calls;

    public async Task Handle(FailingParallelNotification notification, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref Calls);
        await Task.Yield();
        throw new InvalidOperationException("parallel-A");
    }
}

public sealed class FailingParallelHandlerB : INotificationHandler<FailingParallelNotification>
{
    public static int Calls;

    public async Task Handle(FailingParallelNotification notification, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref Calls);
        await Task.Yield();
        throw new InvalidOperationException("parallel-B");
    }
}

public sealed record OrderedDefaultNotification(string Message) : INotification;

public sealed class OrderedDefaultHandlerA : INotificationHandler<OrderedDefaultNotification>
{
    public static List<string> Log { get; } = [];

    public async Task Handle(OrderedDefaultNotification notification, CancellationToken cancellationToken = default)
    {
        await Task.Delay(30, cancellationToken);
        lock (Log)
        {
            Log.Add("A");
        }
    }
}

public sealed class OrderedDefaultHandlerB : INotificationHandler<OrderedDefaultNotification>
{
    public Task Handle(OrderedDefaultNotification notification, CancellationToken cancellationToken = default)
    {
        lock (OrderedDefaultHandlerA.Log)
        {
            OrderedDefaultHandlerA.Log.Add("B");
        }

        return Task.CompletedTask;
    }
}
