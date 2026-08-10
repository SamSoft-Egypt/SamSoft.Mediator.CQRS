using Microsoft.Extensions.DependencyInjection;
using SamSoft.Mediator.CQRS.Abstractions;
using SamSoft.Mediator.CQRS.Tests.TestObjects;

namespace SamSoft.Mediator.CQRS.Tests;

[Collection(nameof(NonParallelCollection))]
public class QueryHandlerTests
{
    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        // Explicit assembly — do not rely on GetCallingAssembly() alone in tests.
        services.AddMediatorService(options =>
        {
            options.RegisterServicesFromAssembly(typeof(MyQuery).Assembly);
            options.RegisterTimeoutBehavior = false;
            options.RegisterPrePostProcessorBehavior = false;
            options.RegisterValidationBehavior = false;
            options.RegisterLoggingBehavior = false;
        });
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task MyQueryHandler_Returns_Success_For_Valid_Id()
    {
        await using var provider = BuildServices();
        var mediator = provider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new MyQuery { Id = 1 }, TestCancel.Token);

        Assert.True(result.IsSuccess);
        Assert.Equal("Value for 1", result.Value);
    }

    [Fact]
    public async Task MyQueryHandler_Returns_Failure_For_Invalid_Id()
    {
        await using var provider = BuildServices();
        var mediator = provider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new MyQuery { Id = 0 }, TestCancel.Token);

        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid Id", result.Error.Message);
    }
}
