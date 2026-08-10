namespace SamSoft.Mediator.CQRS.Handlers;

internal abstract class RequestHandlerWrapper<TResponse> : RequestHandlerBase
{
    public abstract Task<TResponse> Handle(IResponseRequest<TResponse> request, IServiceProvider serviceProvider,
        CancellationToken cancellationToken);
}