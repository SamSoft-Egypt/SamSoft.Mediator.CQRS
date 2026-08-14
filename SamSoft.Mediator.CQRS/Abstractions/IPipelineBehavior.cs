namespace SamSoft.Mediator.CQRS.Abstractions;

/// <summary>
/// Defines a pipeline behavior (decorator) for handling requests and responses.
/// </summary>
/// <typeparam name="TRequest">The type of the request.</typeparam>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public interface IPipelineBehavior<TRequest, TResponse>
{
    /// <summary>
    /// Handles the request and optionally invokes the next behavior or handler in the pipeline.
    /// </summary>
    /// <param name="request">The request instance.</param>
    /// <param name="next">The next delegate in the pipeline.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The response from the next behavior or handler.</returns>
    Task<TResponse> Handle(
        TRequest request,
        HandlerDelegate<TResponse> next,
        CancellationToken cancellationToken
    );
}

/// <summary>
/// Async continuation for the next pipeline stage (eventually the request handler).
/// </summary>
/// <typeparam name="TResponse">The type of the response.</typeparam>
/// <param name="cancellationToken">
/// Token for downstream work. Prefer <c>next(cancellationToken)</c>.
/// Calling <c>next()</c> keeps the token the current behavior received
/// (including a token substituted by an outer behavior such as timeout).
/// Pass a different token only when intentionally replacing it.
/// </param>
/// <returns>The response from the next handler or behavior.</returns>
public delegate Task<TResponse> HandlerDelegate<TResponse>(CancellationToken cancellationToken = default);
