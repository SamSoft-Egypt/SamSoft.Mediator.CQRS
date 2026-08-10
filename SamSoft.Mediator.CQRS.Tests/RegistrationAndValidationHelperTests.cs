using Microsoft.Extensions.DependencyInjection;
using SamSoft.Common.Results;
using SamSoft.Mediator.CQRS.Abstractions;
using SamSoft.Mediator.CQRS.Pipelines;
using SamSoft.Mediator.CQRS.Pipelines.Validation;
using SamSoft.Mediator.CQRS.Tests.DuplicateHandlers;
using SamSoft.Mediator.CQRS.Tests.TestObjects;

namespace SamSoft.Mediator.CQRS.Tests;

[Collection(nameof(NonParallelCollection))]
public class DuplicateHandlerRegistrationTests
{
    [Fact]
    public void DuplicateCommandHandlers_ThrowOnRegistration()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddMediatorService(options =>
            {
                options.RegisterServicesFromAssembly(typeof(DuplicateProbeCommand).Assembly);
            }));

        Assert.Contains("Multiple handlers", ex.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(DuplicateProbeCommand), ex.Message, StringComparison.Ordinal);
    }
}

[Collection(nameof(NonParallelCollection))]
public class LoggingRegistrationTests
{
    [Fact]
    public void RegisterLoggingBehavior_True_RegistersLoggingPipelineBehavior()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediatorService(options =>
        {
            options.RegisterServicesFromAssembly(typeof(PingCommand).Assembly);
            options.RegisterLoggingBehavior = true;
            options.RegisterTimeoutBehavior = false;
            options.RegisterPrePostProcessorBehavior = false;
        });

        using var sp = services.BuildServiceProvider();
        var behaviors = sp.GetServices<IPipelineBehavior<PingCommand, Result>>()
            .Select(b => b.GetType().GetGenericTypeDefinition())
            .ToList();

        Assert.Contains(typeof(LoggingPipelineBehavior<,>), behaviors);
    }

    [Fact]
    public void RegisterLoggingBehavior_False_DoesNotRegisterLoggingPipelineBehavior()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediatorService(options =>
        {
            options.RegisterServicesFromAssembly(typeof(PingCommand).Assembly);
            options.RegisterLoggingBehavior = false;
            options.RegisterTimeoutBehavior = false;
            options.RegisterPrePostProcessorBehavior = false;
        });

        using var sp = services.BuildServiceProvider();
        Assert.DoesNotContain(
            sp.GetServices<IPipelineBehavior<PingCommand, Result>>(),
            b => b.GetType().GetGenericTypeDefinition() == typeof(LoggingPipelineBehavior<,>));
    }
}

[Collection(nameof(NonParallelCollection))]
public class ValidationErrorsTryGetTests
{
    [Fact]
    public void TryGet_WrongCode_ReturnsFalse()
    {
        var error = Error.Validation("Other.Code", "msg", new Dictionary<string, object?>
        {
            ["Name"] = new[] { "required" }
        });

        Assert.False(ValidationErrors.TryGet(error, out var errors));
        Assert.Empty(errors);
    }

    [Fact]
    public void TryGet_NullOrEmptyMetadata_ReturnsFalse()
    {
        var noMeta = Error.Validation(ValidationBehaviorConstants.ValidationFailureErrorCode, "msg");
        Assert.False(ValidationErrors.TryGet(noMeta, out _));

        var empty = Error.Validation(
            ValidationBehaviorConstants.ValidationFailureErrorCode,
            "msg",
            new Dictionary<string, object?>());
        Assert.False(ValidationErrors.TryGet(empty, out _));
    }

    [Fact]
    public void TryGet_SupportsStringAndEnumerableShapes()
    {
        var error = Error.Validation(
            ValidationBehaviorConstants.ValidationFailureErrorCode,
            "msg",
            new Dictionary<string, object?>
            {
                ["Email"] = "required",
                ["Age"] = new List<string> { "too young", "invalid" },
                ["Ignored"] = 42
            });

        Assert.True(ValidationErrors.TryGet(error, out var errors));
        Assert.Contains(errors, e => e is { PropertyName: "Email", ErrorMessage: "required" });
        Assert.Contains(errors, e => e is { PropertyName: "Age", ErrorMessage: "too young" });
        Assert.Contains(errors, e => e is { PropertyName: "Age", ErrorMessage: "invalid" });
        Assert.DoesNotContain(errors, e => e.PropertyName == "Ignored");
    }
}
