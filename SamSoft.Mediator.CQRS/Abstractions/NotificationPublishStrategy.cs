namespace SamSoft.Mediator.CQRS.Abstractions;

/// <summary>
/// Strategies for publishing notifications to handlers.
/// </summary>
public enum NotificationPublishStrategy
{
    /// <summary>
    /// Handlers are invoked concurrently via <see cref="Task.WhenAll"/>. Default when no attribute is applied.
    /// </summary>
    Parallel,

    /// <summary>
    /// Handlers are invoked one after another; stops on the first exception.
    /// </summary>
    Sequential
}

/// <summary>
/// Specifies the notification publishing strategy for a notification type.
/// When omitted, <see cref="MediatorOptions.DefaultNotificationPublishStrategy"/> is used.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class NotificationPublishStrategyAttribute(NotificationPublishStrategy strategy) : Attribute
{
    public NotificationPublishStrategy Strategy { get; } = strategy;
}
