namespace SamSoft.Mediator.CQRS.Handlers.Notifications;

internal interface INotificationPublisher
{
    Task Publish(
        IEnumerable<NotificationHandlerExecutor> handlerExecutors,
        INotification notification,
        CancellationToken cancellationToken);
}

/// <summary>
/// Publishes notifications using per-type <see cref="NotificationPublishStrategyAttribute"/> or the configured default
/// strategy.
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
        cancellationToken.ThrowIfCancellationRequested();

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
            cancellationToken.ThrowIfCancellationRequested();
            await handler.HandlerCallback(notification, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task PublishParallelAsync(
        IEnumerable<NotificationHandlerExecutor> handlerExecutors,
        INotification notification,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

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

        var errors = new List<Exception>();
        var canceled = false;

        foreach (var task in tasks)
        {
            if (task.IsFaulted)
            {
                errors.Add(task.Exception!.GetBaseException());
            }
            else if (task.IsCanceled)
            {
                canceled = true;
            }
        }

        if (errors.Count > 0)
        {
            if (canceled)
            {
                errors.Add(new OperationCanceledException(cancellationToken));
            }

            if (errors.Count == 1)
            {
                throw errors[0];
            }

            throw new AggregateException(errors);
        }

        if (canceled)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new OperationCanceledException(cancellationToken);
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

internal sealed class NotificationHandlerExecutor(Func<INotification, CancellationToken, Task> handlerCallback)
{
    public Func<INotification, CancellationToken, Task> HandlerCallback { get; } = handlerCallback;
}
