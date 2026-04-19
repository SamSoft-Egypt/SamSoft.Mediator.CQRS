namespace SamSoft.Mediator.CQRS.Abstractions;

/// <summary>
/// Strategies for publishing notifications to handlers.
/// </summary>
public enum NotificationPublishStrategy
{
    /// <summary>
    /// Handlers are invoked concurrently (default), fail fast on first exception.
    /// </summary>
    Parallel,
    /// <summary>
    /// Handlers are invoked one after another, stop on first exception.
    /// </summary>
    Sequential
}

/// <summary>
/// Attribute to specify the notification publishing strategy for a notification type.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class NotificationPublishStrategyAttribute(NotificationPublishStrategy strategy) : Attribute
{
    public NotificationPublishStrategy Strategy { get; } = strategy;
}