namespace SamSoft.Mediator.CQRS.Pipelines;

/// <summary>
/// Pipeline behavior that runs all registered pre- and post-processors for a request.
/// </summary>
public sealed class PrePostProcessorBehavior<TRequest, TResponse>(
    IEnumerable<IRequestPreProcessor<TRequest>> preProcessors,
    IEnumerable<IRequestPostProcessor<TRequest, TResponse>> postProcessors)
    : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        HandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        foreach (var pre in preProcessors)
        {
            await pre.Process(request, cancellationToken).ConfigureAwait(false);
        }

        var response = await next(cancellationToken).ConfigureAwait(false);

        foreach (var post in postProcessors)
        {
            await post.Process(request, response, cancellationToken).ConfigureAwait(false);
        }

        return response;
    }
}
