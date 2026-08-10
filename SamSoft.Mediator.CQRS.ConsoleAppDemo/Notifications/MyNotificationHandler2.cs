using SamSoft.Mediator.CQRS.Abstractions;

namespace SamSoft.Mediator.CQRS.ConsoleAppDemo.Notifications;

internal class MyNotificationHandler2 : INotificationHandler<MyNotification>
{
    public Task Handle(MyNotification notification, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Hello From Notification Handler 2 {notification.NotificationMessage}");
        return Task.CompletedTask;
    }
}