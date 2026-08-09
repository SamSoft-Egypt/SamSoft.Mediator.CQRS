using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SamSoft.Mediator.CQRS.Abstractions;
using SamSoft.Mediator.CQRS.Tests.TestObjects;

namespace SamSoft.Mediator.CQRS.Tests;

[Collection(nameof(NonParallelCollection))]
public class ValidationBehaviorTests
{
    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediatorService(options =>
        {
            options.RegisterServicesFromAssembly(typeof(TestCommand).Assembly);
            options.RegisterValidationBehavior = true;
        });
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task ValidCommand_ReturnsSuccess()
    {
        await using var sp = BuildServices();
        var mediator = sp.GetRequiredService<IMediator>();

        var result = await mediator.Send(new TestCommand("ok"), TestCancel.Token);

        Assert.True(result.IsSuccess);
        Assert.Equal("ok_handled", result.Value);
    }

    [Fact]
    public async Task InvalidCommand_ReturnsValidationFailureResult()
    {
        await using var sp = BuildServices();
        var mediator = sp.GetRequiredService<IMediator>();

        var result = await mediator.Send(new TestCommand("fail"), TestCancel.Token);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.Failed", result.Error.Code);
        Assert.Contains("Always fails", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidQuery_ReturnsSuccess()
    {
        await using var sp = BuildServices();
        var mediator = sp.GetRequiredService<IMediator>();

        var result = await mediator.Send(new TestQuery("ok"), TestCancel.Token);

        Assert.True(result.IsSuccess);
        Assert.Equal("ok_queried", result.Value);
    }

    [Fact]
    public async Task InvalidQuery_ReturnsValidationFailureResult()
    {
        await using var sp = BuildServices();
        var mediator = sp.GetRequiredService<IMediator>();

        var result = await mediator.Send(new TestQuery("fail"), TestCancel.Token);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.Failed", result.Error.Code);
        Assert.Contains("Query validation failed", result.Error.Message, StringComparison.Ordinal);
    }
}

/// <summary>
/// Fails when the command value contains "fail" (case-insensitive).
/// </summary>
public sealed class TestCommandValidator : AbstractValidator<TestCommand>
{
    public TestCommandValidator()
    {
        RuleFor(x => x.Value)
            .Must(value => !value.Contains("fail", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Always fails");
    }
}

/// <summary>
/// Fails when the query value contains "fail" (case-insensitive).
/// </summary>
public sealed class TestQueryValidator : AbstractValidator<TestQuery>
{
    public TestQueryValidator()
    {
        RuleFor(x => x.Value)
            .Must(value => !value.Contains("fail", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Query validation failed");
    }
}
