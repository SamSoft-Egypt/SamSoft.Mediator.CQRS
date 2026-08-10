using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SamSoft.Common.Results;
using SamSoft.Mediator.CQRS.Abstractions;
using SamSoft.Mediator.CQRS.Pipelines.Validation;
using SamSoft.Mediator.CQRS.Tests.TestObjects;

namespace SamSoft.Mediator.CQRS.Tests;

[Collection(nameof(NonParallelCollection))]
public class ValidationBehaviorAdvancedTests
{
    [Fact]
    public async Task ValidatedVoidCommand_Invalid_ReturnsFailureWithoutThrowing()
    {
        await using var sp = TestServiceFactory.Create(options =>
        {
            options.RegisterValidationBehavior = true;
        });

        var result = await sp.GetRequiredService<IMediator>().Send(new ValidatedPingCommand(false), TestCancel.Token);

        Assert.True(result.IsFailure);
        Assert.Equal(ValidationBehaviorConstants.ValidationFailureErrorCode, result.Error.Code);
        Assert.Contains("MustBeValid", result.Error.Message, StringComparison.Ordinal);
        Assert.True(ValidationErrors.TryGet(result.Error, out var fieldErrors));
        Assert.Contains(fieldErrors, e => e.ErrorMessage == "MustBeValid");
    }

    [Fact]
    public async Task ValidatedVoidCommand_Valid_ReturnsSuccess()
    {
        await using var sp = TestServiceFactory.Create(options =>
        {
            options.RegisterValidationBehavior = true;
        });

        var result = await sp.GetRequiredService<IMediator>().Send(new ValidatedPingCommand(true), TestCancel.Token);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task MultipleValidators_AggregateErrors()
    {
        await using var sp = TestServiceFactory.Create(options =>
        {
            options.RegisterValidationBehavior = true;
        });

        var result = await sp.GetRequiredService<IMediator>().Send(new MultiRuleCommand(""), TestCancel.Token);

        Assert.True(result.IsFailure);
        Assert.Contains("Name required", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains("Name too short", result.Error.Message, StringComparison.Ordinal);
        Assert.True(ValidationErrors.TryGet(result.Error, out var fieldErrors));
        Assert.Equal(
            new[] { "Name required", "Name too short" },
            fieldErrors.Select(e => e.ErrorMessage).Distinct().OrderBy(x => x).ToArray());
        Assert.All(fieldErrors, e => Assert.Equal("Name", e.PropertyName));
        var nameMessages = Assert.IsType<string[]>(result.Error.Metadata!["Name"]);
        Assert.Contains("Name required", nameMessages);
        Assert.Contains("Name too short", nameMessages);
    }

    [Fact]
    public async Task NoValidators_PassThrough()
    {
        await using var sp = TestServiceFactory.Create(options =>
        {
            options.RegisterValidationBehavior = true;
        });

        var result = await sp.GetRequiredService<IMediator>().Send(new FastCommand(), TestCancel.Token);
        Assert.True(result.IsSuccess);
        Assert.Equal("fast", result.Value);
    }
}

public sealed record ValidatedPingCommand(bool IsValid) : ICommand;

public sealed class ValidatedPingCommandHandler : ICommandHandler<ValidatedPingCommand>
{
    public Task<Result> Handle(ValidatedPingCommand command, CancellationToken cancellationToken = default)
        => Task.FromResult(Result.Success());
}

public sealed class ValidatedPingCommandValidator : AbstractValidator<ValidatedPingCommand>
{
    public ValidatedPingCommandValidator()
    {
        RuleFor(x => x.IsValid)
            .Equal(true)
            .WithMessage("MustBeValid");
    }
}

public sealed record MultiRuleCommand(string Name) : ICommand<string>;

public sealed class MultiRuleCommandHandler : ICommandHandler<MultiRuleCommand, string>
{
    public Task<Result<string>> Handle(MultiRuleCommand command, CancellationToken cancellationToken = default)
        => Task.FromResult(Result.Success(command.Name));
}

public sealed class MultiRuleCommandValidatorA : AbstractValidator<MultiRuleCommand>
{
    public MultiRuleCommandValidatorA()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Name required");
    }
}

public sealed class MultiRuleCommandValidatorB : AbstractValidator<MultiRuleCommand>
{
    public MultiRuleCommandValidatorB()
    {
        RuleFor(x => x.Name).MinimumLength(3).WithMessage("Name too short");
    }
}
