namespace SamSoft.Mediator.CQRS.Pipelines.Validation;

public sealed record ValidationError(string PropertyName, string ErrorMessage);
