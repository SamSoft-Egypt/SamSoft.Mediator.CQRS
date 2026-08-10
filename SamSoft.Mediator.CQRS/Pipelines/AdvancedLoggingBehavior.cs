namespace SamSoft.Mediator.CQRS.Pipelines;

/// <summary>
/// Structured logging around mediator requests. Full request/response payloads are logged only at
/// <see cref="LogLevel.Debug"/> to reduce accidental PII/secret leakage.
/// </summary>
/// <remarks>
/// Prefer not enabling this behavior in production without reviewing log sinks and redaction.
/// </remarks>
public sealed class AdvancedLoggingBehavior<TRequest, TResponse>(
    ILogger<AdvancedLoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        HandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestType = typeof(TRequest).Name;
        logger.LogInformation("Handling request {RequestType}", requestType);
        logger.LogDebug("Handling request {RequestType} payload {@Request}", requestType, request);

        try
        {
            var response = await next(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Handled request {RequestType}", requestType);
            logger.LogDebug("Handled request {RequestType} response {@Response}", requestType, response);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception handling request {RequestType}", requestType);
            logger.LogDebug(ex, "Exception handling request {RequestType} payload {@Request}", requestType, request);
            throw;
        }
    }
}
