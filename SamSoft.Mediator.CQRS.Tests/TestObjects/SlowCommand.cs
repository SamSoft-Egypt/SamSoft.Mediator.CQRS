using SamSoft.Common.Results;
using SamSoft.Mediator.CQRS.Abstractions;

namespace SamSoft.Mediator.CQRS.Tests.TestObjects;

public sealed record SlowCommand : ICommand<string>;

public sealed class SlowCommandHandler : ICommandHandler<SlowCommand, string>
{
    public static bool WasCancelled { get; set; }

    public async Task<Result<string>> Handle(SlowCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            return Result.Success("Done");
        }
        catch (OperationCanceledException)
        {
            WasCancelled = true;
            throw;
        }
    }
}
