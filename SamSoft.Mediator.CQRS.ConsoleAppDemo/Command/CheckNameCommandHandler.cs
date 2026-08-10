using SamSoft.Common.Results;
using SamSoft.Mediator.CQRS.Abstractions;

namespace SamSoft.Mediator.CQRS.ConsoleAppDemo.Command;

internal sealed class CheckNameCommandHandler : ICommandHandler<CheckNameCommand>
{
    public Task<Result> Handle(CheckNameCommand command, CancellationToken cancellationToken = default)
    {
        // Simulate checking logic
        var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(command.Encoded));
        var isValid = decoded.ToLower().Equals(command.Name.ToLower());
        Console.WriteLine($"Is Name Valid: {isValid}");
        return Task.FromResult(isValid ?
            Result.Success() :
            Result.Failure(Error.Validation("NOtSame", "Invalid name or encoded value")));
    }
}