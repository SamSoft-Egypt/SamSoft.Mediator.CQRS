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

        // Build the pipeline so each stage closes over the token it received.
        // HandlerDelegate allows next() (optional CT = default). Omitting the token must keep
        // this stage's token — not CancellationToken.None — so TimeoutBehavior / caller cancel
        // still reach the handler. Passing an explicit token still replaces it (e.g. timeout CTS).
        return serviceProvider
            .GetServices<IPipelineBehavior<TRequest, TResponse>>()
            .Reverse()
            .Aggregate(
                (HandlerDelegate<TResponse>)Handler,
                (next, behavior) => currentToken =>
                    behavior.Handle(
                        (TRequest)request,
                        nextToken => next(nextToken == default ? currentToken : nextToken),
                        currentToken))(cancellationToken);
    }
}
