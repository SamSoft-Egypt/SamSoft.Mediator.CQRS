using SamSoft.Common.Results;
using SamSoft.Mediator.CQRS.Abstractions;
using SamSoft.Mediator.CQRS.ConsoleAppDemo.Notifications;


namespace SamSoft.Mediator.CQRS.ConsoleAppDemo.Command;

internal sealed class EncodeCommandHandler(IPublisher mediator) : ICommandHandler<EncodeCommand, string>
{
    private readonly IPublisher publisher = mediator;

    public async Task<Result<string>> Handle(EncodeCommand command, CancellationToken cancellationToken = default)
    {
        // Simulate encoding logic
        var encodedName = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(command.Name));
        var currentDateTime = DateTime.UtcNow;
        Console.WriteLine($"Encoded Name: {encodedName}");
        var notification = new MyNotification($"Hello From Notification Handler {command.Name} On {currentDateTime}");
        await publisher.Publish(notification, cancellationToken);
        return Result.Success(encodedName);
    }
}
