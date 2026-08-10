using SamSoft.Common.Results;
using SamSoft.Mediator.CQRS.Abstractions;

namespace SamSoft.Mediator.CQRS.Tests.DuplicateHandlers;

public sealed record DuplicateProbeCommand : ICommand;

public sealed class DuplicateProbeHandlerA : ICommandHandler<DuplicateProbeCommand>
{
    public Task<Result> Handle(DuplicateProbeCommand command, CancellationToken cancellationToken = default)
        => Task.FromResult(Result.Success());
}

public sealed class DuplicateProbeHandlerB : ICommandHandler<DuplicateProbeCommand>
{
    public Task<Result> Handle(DuplicateProbeCommand command, CancellationToken cancellationToken = default)
        => Task.FromResult(Result.Success());
}
