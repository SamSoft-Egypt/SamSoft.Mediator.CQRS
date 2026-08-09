using SamSoft.Mediator.CQRS.Abstractions.Requests;

namespace SamSoft.Mediator.CQRS.Handlers;

internal sealed class RequestHandlerWrapperImplementation<TRequest, TResponse> : RequestHandlerWrapper<TResponse>
    where TRequest : IResponseRequest<TResponse>
{
    public override async Task<object?> Handle(
        object request,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken) =>
        await Handle((IResponseRequest<TResponse>)request, serviceProvider, cancellationToken)
            .ConfigureAwait(false);

    public override Task<TResponse> Handle(
        IResponseRequest<TResponse> request,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        Task<TResponse> Handler(CancellationToken token) =>
            serviceProvider
                .GetRequiredService<IRequestHandlerBase<TRequest, TResponse>>()
                .Handle((TRequest)request, token);

        return serviceProvider
            .GetServices<IPipelineBehavior<TRequest, TResponse>>()
            .Reverse()
            .Aggregate(
                (HandlerDelegate<TResponse>)Handler,
                (next, pipeline) => token => pipeline.Handle((TRequest)request, next, token))(cancellationToken);
    }
}
