namespace SamSoft.Mediator.CQRS.Pipelines;

/// <summary>
/// Pipeline behavior for logging requests, responses, and exceptions.
/// </summary>
public class AdvancedLoggingBehavior<TRequest, TResponse>(ILogger<AdvancedLoggingBehavior<TRequest, TResponse>> logger) : IPipelineBehavior<TRequest, TResponse>
{
    private readonly ILogger<AdvancedLoggingBehavior<TRequest, TResponse>> _logger = logger;

    public async Task<TResponse> Handle(TRequest request, HandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestType = typeof(TRequest).Name;
        _logger.LogInformation("Handling request: {RequestType} {@Request}", requestType, request);

        try
        {
            var response = await next(cancellationToken);
            _logger.LogInformation("Handled request: {RequestType} => {@Response}", requestType, response);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling request: {RequestType} {@Request}", requestType, request);
            throw;
        }
    }
}