namespace SamSoft.Mediator.CQRS.Abstractions;

public interface ICommandHandler<in TCommand> : IRequestHandlerBase<TCommand, Result>
    where TCommand : ICommand;

public interface ICommandHandler<in TCommand, TResponse> : IRequestHandlerBase<TCommand, Result<TResponse>>
    where TCommand : ICommand<TResponse>;