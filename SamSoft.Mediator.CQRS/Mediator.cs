using SamSoft.Mediator.CQRS.Abstractions.Requests;
using SamSoft.Mediator.CQRS.Handlers;
using SamSoft.Mediator.CQRS.Handlers.Notifications;
using System.Collections.Concurrent;

namespace SamSoft.Mediator.CQRS;

/// <summary>
/// Default <see cref="IMediator"/> implementation for CQRS send and notification publish.
/// </summary>
public sealed class Mediator : IMediator
{
    private static readonly ConcurrentDictionary<Type, RequestHandlerBase> RequestHandlers = new();
    private static readonly ConcurrentDictionary<Type, NotificationHandlerWrapper> NotificationHandlers = new();

    private readonly IServiceProvider _serviceProvider;
    private readonly INotificationPublisher _publisher;

    public Mediator(IServiceProvider serviceProvider)
        : this(
            serviceProvider,
            serviceProvider.GetService<INotificationPublisher>()
            ?? new StrategyAwareNotificationPublisher(NotificationPublishStrategy.Parallel))
    {
    }

    internal Mediator(IServiceProvider serviceProvider, INotificationPublisher publisher)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
    }

    private Task<TResponse> SendImplementation<TResponse>(
        IResponseRequest<TResponse> request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var handler = (RequestHandlerWrapper<TResponse>)RequestHandlers.GetOrAdd(
            request.GetType(),
            static requestType =>
            {
                var wrapperType = typeof(RequestHandlerWrapperImplementation<,>)
                    .MakeGenericType(requestType, typeof(TResponse));
                var wrapper = Activator.CreateInstance(wrapperType)
                    ?? throw new InvalidOperationException($"Could not create wrapper type for {requestType}");
                return (RequestHandlerBase)wrapper;
            });

        return handler.Handle(request, _serviceProvider, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result> Send(ICommand command, CancellationToken cancellationToken = default)
        => SendImplementation(command, cancellationToken);

    /// <inheritdoc />
    public Task<Result<TResponse>> Send<TResponse>(
        ICommand<TResponse> command,
        CancellationToken cancellationToken = default)
        => SendImplementation(command, cancellationToken);

    /// <inheritdoc />
    public Task<Result<TResponse>> Send<TResponse>(
        IQuery<TResponse> query,
        CancellationToken cancellationToken = default)
        => SendImplementation(query, cancellationToken);

    /// <inheritdoc />
    public Task Publish<TNotification>(
        TNotification notification,
        CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        ArgumentNullException.ThrowIfNull(notification);
        return PublishNotification(notification, cancellationToken);
    }

    private Task PublishCore(
        IEnumerable<NotificationHandlerExecutor> handlerExecutors,
        INotification notification,
        CancellationToken cancellationToken)
        => _publisher.Publish(handlerExecutors, notification, cancellationToken);

    private Task PublishNotification(INotification notification, CancellationToken cancellationToken)
    {
        var handler = NotificationHandlers.GetOrAdd(
            notification.GetType(),
            static notificationType =>
            {
                var wrapperType = typeof(NotificationHandlerWrapperImplementation<>)
                    .MakeGenericType(notificationType);
                var wrapper = Activator.CreateInstance(wrapperType)
                    ?? throw new InvalidOperationException($"Could not create wrapper for type {notificationType}");
                return (NotificationHandlerWrapper)wrapper;
            });

        return handler.Handle(notification, _serviceProvider, PublishCore, cancellationToken);
    }
}
