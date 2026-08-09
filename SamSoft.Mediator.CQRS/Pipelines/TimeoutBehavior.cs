using Microsoft.Extensions.Options;

namespace SamSoft.Mediator.CQRS.Pipelines;

/// <summary>
/// Cancels a request when it exceeds <see cref="TimeoutSettings.Timeout"/>.
/// Uses a linked <see cref="CancellationTokenSource"/> so the handler observes cancellation.
/// </summary>
public sealed class TimeoutBehavior<TRequest, TResponse>(IOptions<TimeoutSettings> options)
    : IPipelineBehavior<TRequest, TResponse>
{
    private readonly TimeSpan _timeout = options.Value.Timeout;

    public async Task<TResponse> Handle(
        TRequest request,
        HandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_timeout);

        try
        {
            return await next(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Request of type {typeof(TRequest).Name} timed out after {_timeout.TotalMilliseconds} ms.");
        }
    }
}
