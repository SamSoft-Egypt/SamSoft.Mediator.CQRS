using SamSoft.Mediator.CQRS.Abstractions;

namespace SamSoft.Mediator.CQRS.ConsoleAppDemo.Notifications;

public record MyNotification(string NotificationMessage) : INotification;