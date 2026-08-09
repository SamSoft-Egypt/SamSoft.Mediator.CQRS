using System.Collections.Concurrent;
using System.Reflection;
using SamSoft.Mediator.CQRS.Abstractions.Requests;

namespace SamSoft.Mediator.CQRS.Pipelines;

/// <summary>
/// Runs FluentValidation validators for commands and queries
/// (<see cref="IResponseRequest{TResponse}"/> with a <see cref="Result"/> response).
/// Validation failures are returned as <see cref="Result"/> / <see cref="Result{TValue}"/> failures
/// (they are not thrown).
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>>? validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IResponseRequest<TResponse>
    where TResponse : Result
{
    public async Task<TResponse> Handle(
        TRequest request,
        HandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (validators is not null)
        {
            var context = new ValidationContext<TRequest>(request);
            var validationResults = await Task.WhenAll(
                    validators.Select(validator => validator.ValidateAsync(context, cancellationToken)))
                .ConfigureAwait(false);

            var errors = validationResults
                .Where(static result => !result.IsValid)
                .SelectMany(static result => result.Errors)
                .Select(static failure => new ValidationError(failure.PropertyName, failure.ErrorMessage))
                .ToList();

            if (errors.Count > 0)
            {
                return ValidationFailureFactory.Create<TResponse>(errors);
            }
        }

        return await next(cancellationToken).ConfigureAwait(false);
    }
}

public sealed record ValidationError(string PropertyName, string ErrorMessage);

/// <summary>
/// Previously thrown by <see cref="ValidationBehavior{TRequest,TResponse}"/>.
/// Validation now returns <see cref="Result"/> failures instead.
/// </summary>
[Obsolete("ValidationBehavior returns Result.Failure. Catch Result.IsFailure instead of this exception.")]
public sealed class CustomValidationException(IEnumerable<ValidationError> errors)
    : Exception("Validation Failure")
{
    public IEnumerable<ValidationError> Errors { get; } = errors;
}

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

        var error = Error.Validation("Validation.Failed", message);
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
