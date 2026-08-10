namespace SamSoft.Mediator.CQRS.Pipelines.Validation;

/// <summary>
/// Stable contract for validation failures produced by <see cref="ValidationBehavior{TRequest,TResponse}"/>.
/// </summary>
public static class ValidationBehaviorConstants
{
    /// <summary>
    /// <see cref="Error.Code"/> when FluentValidation rejects a request in this package.
    /// </summary>
    public const string ValidationFailureErrorCode = "CQRS.Pipelines.Validation.Failed";
}
