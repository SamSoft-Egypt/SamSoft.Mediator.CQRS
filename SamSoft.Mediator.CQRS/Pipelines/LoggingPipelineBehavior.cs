namespace SamSoft.Mediator.CQRS.Pipelines;

/// <summary>
/// Logs request type name and duration. Property values are only written at
/// <see cref="LogLevel.Debug"/> to reduce accidental PII/secret leakage.
/// </summary>
/// <remarks>
/// Prefer not enabling this behavior in production without reviewing log sinks and redaction.
/// </remarks>
public sealed class LoggingPipelineBehavior<TRequest, TResponse>(
    ILogger<LoggingPipelineBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TResponse : Result
{
    public async Task<TResponse> Handle(
        TRequest request,
        HandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        logger.LogInformation(
            "Starting request {RequestName} at {Timestamp:O}",
            requestName,
            DateTimeOffset.UtcNow);

        if (logger.IsEnabled(LogLevel.Debug) && request is not null)
        {
            foreach (var prop in typeof(TRequest).GetProperties())
            {
                logger.LogDebug(
                    "Request {RequestName} property {PropertyName} = {PropertyValue}",
                    requestName,
                    prop.Name,
                    prop.GetValue(request));
            }
        }

        var sw = Stopwatch.StartNew();
        var result = await next(cancellationToken).ConfigureAwait(false);
        sw.Stop();

        if (result.IsFailure)
        {
            logger.LogError(
                "Request {RequestName} failed with {ErrorCode}: {ErrorMessage} in {ElapsedMs} ms",
                requestName,
                result.Error.Code,
                result.Error.Message,
                sw.ElapsedMilliseconds);
        }
        else
        {
            logger.LogInformation(
                "Request {RequestName} completed in {ElapsedMs} ms",
                requestName,
                sw.ElapsedMilliseconds);
        }

        return result;
    }
}
