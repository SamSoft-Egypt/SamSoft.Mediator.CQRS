namespace SamSoft.Mediator.CQRS.Pipelines.Validation;

/// <summary>
/// Extracts structured field errors from an <see cref="Error"/> produced by
/// <see cref="ValidationBehavior{TRequest,TResponse}"/>.
/// </summary>
public static class ValidationErrors
{
    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="error"/> is a pipeline validation failure and field messages
    /// can be read from <see cref="Error.Metadata"/>.
    /// </summary>
    public static bool TryGet(Error error, out IReadOnlyList<ValidationError> errors)
    {
        ArgumentNullException.ThrowIfNull(error);

        if (error.Code != ValidationBehaviorConstants.ValidationFailureErrorCode ||
            error.Metadata is null ||
            error.Metadata.Count == 0)
        {
            errors = Array.Empty<ValidationError>();
            return false;
        }

        var list = new List<ValidationError>(error.Metadata.Count);
        foreach (var (propertyName, value) in error.Metadata)
        {
            switch (value)
            {
                case string[] messages:
                    foreach (var message in messages)
                    {
                        list.Add(new ValidationError(propertyName, message));
                    }
                    break;

                case IEnumerable<string> messages:
                    foreach (var message in messages)
                    {
                        list.Add(new ValidationError(propertyName, message));
                    }
                    break;

                case string message:
                    list.Add(new ValidationError(propertyName, message));
                    break;
            }
        }

        errors = list;
        return list.Count > 0;
    }
}
