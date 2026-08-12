namespace SamSoft.Mediator.CQRS.Pipelines.Validation;

/// <summary>
/// Runs FluentValidation validators for commands and queries (<see cref="IResponseRequest{TResponse}"/> with a
/// <see cref="Result"/> response). Validation failures are returned as <see cref="Result"/> /
/// <see cref="Result{TValue}"/> failures (they are not thrown).
/// </summary>
/// <remarks>
/// Field errors are placed in <see cref="Error.Metadata"/> as <c>PropertyName → string[]</c> messages (SamSoft.Common
/// convention).
/// </remarks>
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
            // FluentValidation's ValidationContext is mutable and not thread-safe (shared Failures /
            // RootContextData). Each parallel ValidateAsync must get its own context; reusing one
            // causes duplicated errors and intermittent "collection was modified" failures.
            var validationResults = await Task.WhenAll(
                    validators.Select(validator =>
                        validator.ValidateAsync(new ValidationContext<TRequest>(request), cancellationToken)))
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