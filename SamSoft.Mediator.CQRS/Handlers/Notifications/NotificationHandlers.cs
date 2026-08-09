using System.Collections.Concurrent;
using System.Reflection;

namespace SamSoft.Mediator.CQRS.Handlers.Notifications;

internal interface INotificationPublisher
{
    Task Publish(
        IEnumerable<NotificationHandlerExecutor> handlerExecutors,
        INotification notification,
        CancellationToken cancellationToken);
}

/// <summary>
/// Publishes notifications using per-type <see cref="NotificationPublishStrategyAttribute"/>
/// or the configured default strategy.
/// </summary>
internal sealed class StrategyAwareNotificationPublisher(NotificationPublishStrategy defaultStrategy)
    : INotificationPublisher
{
    private static readonly ConcurrentDictionary<Type, NotificationPublishStrategy?> AttributeCache = new();

    public Task Publish(
        IEnumerable<NotificationHandlerExecutor> handlerExecutors,
        INotification notification,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handlerExecutors);
        ArgumentNullException.ThrowIfNull(notification);

        var strategy = ResolveStrategy(notification.GetType());
        return strategy == NotificationPublishStrategy.Sequential
            ? PublishSequentialAsync(handlerExecutors, notification, cancellationToken)
            : PublishParallelAsync(handlerExecutors, notification, cancellationToken);
    }

    private NotificationPublishStrategy ResolveStrategy(Type notificationType)
    {
        var attributed = AttributeCache.GetOrAdd(
            notificationType,
            static type => type.GetCustomAttribute<NotificationPublishStrategyAttribute>()?.Strategy);

        return attributed ?? defaultStrategy;
    }

    private static async Task PublishSequentialAsync(
        IEnumerable<NotificationHandlerExecutor> handlerExecutors,
        INotification notification,
        CancellationToken cancellationToken)
    {
        foreach (var handler in handlerExecutors)
        {
            await handler.HandlerCallback(notification, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task PublishParallelAsync(
        IEnumerable<NotificationHandlerExecutor> handlerExecutors,
        INotification notification,
        CancellationToken cancellationToken)
    {
        var tasks = new List<Task>();
        foreach (var handler in handlerExecutors)
        {
            tasks.Add(handler.HandlerCallback(notification, cancellationToken));
        }

        if (tasks.Count == 0)
        {
            return;
        }

        await Task.WhenAll(tasks).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

        var errors = tasks
            .Where(static t => t.IsFaulted)
            .Select(static t => t.Exception!.GetBaseException())
            .ToArray();

        switch (errors.Length)
        {
            case 0:
                return;
            case 1:
                throw errors[0];
            default:
                throw new AggregateException(errors);
        }
    }
}

internal abstract class NotificationHandlerWrapper
{
    public abstract Task Handle(
        INotification notification,
        IServiceProvider serviceProvider,
        Func<IEnumerable<NotificationHandlerExecutor>, INotification, CancellationToken, Task> publish,
        CancellationToken cancellationToken);
}

internal sealed class NotificationHandlerWrapperImplementation<TNotification> : NotificationHandlerWrapper
    where TNotification : INotification
{
    public override Task Handle(
        INotification notification,
        IServiceProvider serviceProvider,
        Func<IEnumerable<NotificationHandlerExecutor>, INotification, CancellationToken, Task> publish,
        CancellationToken cancellationToken)
    {
        var handlers = serviceProvider
            .GetServices<INotificationHandler<TNotification>>()
            .Select(h => new NotificationHandlerExecutor((n, ct) => h.Handle((TNotification)n, ct)));

        return publish(handlers, notification, cancellationToken);
    }
}

public sealed class NotificationHandlerExecutor(Func<INotification, CancellationToken, Task> handlerCallback)
{
    public Func<INotification, CancellationToken, Task> HandlerCallback { get; } = handlerCallback;
}
