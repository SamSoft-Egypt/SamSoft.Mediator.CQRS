namespace SamSoft.Mediator.CQRS.Pipelines.Validation;

internal static class ValidationFailureFactory
{
    private static readonly ConcurrentDictionary<Type, Func<Error, Result>> Factories = new();

    public static TResponse Create<TResponse>(IReadOnlyList<ValidationError> errors)
        where TResponse : Result
    {
        ArgumentNullException.ThrowIfNull(errors);

        var message = string.Join("; ", errors.Select(static e =>
            string.IsNullOrWhiteSpace(e.PropertyName)
                ? e.ErrorMessage
                : $"{e.PropertyName}: {e.ErrorMessage}"));

        var metadata = errors
            .GroupBy(static e => e.PropertyName ?? string.Empty, StringComparer.Ordinal)
            .ToDictionary(
                static g => g.Key,
                static g => (object?)g.Select(static e => e.ErrorMessage).ToArray(),
                StringComparer.Ordinal);

        var error = Error.Validation(
            ValidationBehaviorConstants.ValidationFailureErrorCode,
            message,
            metadata);

        return (TResponse)GetFactory(typeof(TResponse)).Invoke(error);
    }

    private static Func<Error, Result> GetFactory(Type responseType) =>
        Factories.GetOrAdd(responseType, static type =>
        {
            if (type == typeof(Result))
            {
                return Result.Failure;
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Result<>))
            {
                var valueType = type.GetGenericArguments()[0];
                var method = typeof(Result)
                    .GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Single(m =>
                        m.Name == nameof(Result.Failure) &&
                        m.IsGenericMethodDefinition &&
                        m.GetParameters() is [{ ParameterType: var parameterType }] &&
                        parameterType == typeof(Error));

                var genericMethod = method.MakeGenericMethod(valueType);
                return error => (Result)genericMethod.Invoke(null, [error])!;
            }

            throw new InvalidOperationException(
                $"ValidationBehavior requires TResponse to be Result or Result<T>. Got {type}.");
        });
}