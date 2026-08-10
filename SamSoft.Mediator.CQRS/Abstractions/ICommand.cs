namespace SamSoft.Mediator.CQRS.Abstractions;

public interface ICommand : IResponseRequest<Result>, IBaseCommand;

public interface ICommand<TResponse> : IResponseRequest<Result<TResponse>>, IBaseCommand;

public interface IBaseCommand;