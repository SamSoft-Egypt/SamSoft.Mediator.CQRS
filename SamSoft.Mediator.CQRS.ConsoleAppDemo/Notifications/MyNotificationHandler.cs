using SamSoft.Mediator.CQRS.Abstractions;

namespace SamSoft.Mediator.CQRS.ConsoleAppDemo.Notifications;

internal class MyNotificationHandler : INotificationHandler<MyNotification>
{
    public Task Handle(MyNotification notification, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Hello From Notification Handler {notification.NotificationMessage}");
        return Task.CompletedTask;
    }
}
