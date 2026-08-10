namespace SamSoft.Mediator.CQRS.Pipelines.Validation;

/// <summary>
/// Previously thrown by <see cref="ValidationBehavior{TRequest,TResponse}"/>. Validation now returns
/// <see cref="Result"/> failures instead.
/// </summary>
[Obsolete("ValidationBehavior returns Result.Failure. Catch Result.IsFailure instead of this exception.")]
public sealed class CustomValidationException(IEnumerable<ValidationError> errors)
    : Exception("Validation Failure")
{
    public IEnumerable<ValidationError> Errors { get; } = errors;
}
