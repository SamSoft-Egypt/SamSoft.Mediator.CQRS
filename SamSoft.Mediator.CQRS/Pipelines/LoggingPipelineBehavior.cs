namespace SamSoft.Mediator.CQRS.Pipelines;

public class LoggingPipelineBehavior<TRequest, TResponse>(
    ILogger<LoggingPipelineBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    //where TRequest : IBaseCommand
    where TResponse : Result
{
    public async Task<TResponse> Handle(TRequest request, HandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        Type myType = request!.GetType();
        var requestName = myType.Name;
        logger.LogInformation(
            "Starting request [{@RequestName}] On {@CurrentDateTime}",
            requestName, DateTime.UtcNow);

        var props = new List<PropertyInfo>(myType.GetProperties());
        foreach (var prop in props)
        {
            var value = prop.GetValue(request, null);
            logger.LogInformation("Property {Name} : {Value}", prop.Name, value);
        }
        var sw = Stopwatch.StartNew();
        var result = await next(cancellationToken);
        if (result.IsFailure)
        {
            logger.LogError(
                "Request failure [{@RequestName}] Error: {@Error} On {@CurrentDateTime}",
                requestName, result.Error.Message, DateTime.UtcNow);
        }
        logger.LogInformation(
            "Request [{@RequestName}] Completed On {@CurrentDateTime} in {ms} ms",
            requestName, DateTime.UtcNow, sw.ElapsedMilliseconds);
        sw.Stop();
        return result;
    }
}