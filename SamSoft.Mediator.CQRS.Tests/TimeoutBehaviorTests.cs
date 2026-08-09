using Microsoft.Extensions.DependencyInjection;
using SamSoft.Mediator.CQRS.Abstractions;
using SamSoft.Mediator.CQRS.Pipelines;
using SamSoft.Mediator.CQRS.Tests.TestObjects;

namespace SamSoft.Mediator.CQRS.Tests;

[Collection(nameof(NonParallelCollection))]
public class TimeoutBehaviorTests
{
    private static ServiceProvider BuildServices(TimeSpan timeout)
    {
        var services = new ServiceCollection();
        services.AddMediatorService(options =>
        {
            options.RegisterServicesFromAssembly(typeof(SlowCommandHandler).Assembly);
            options.RegisterTimeoutBehavior = true;
            options.TimeoutSettings.Timeout = timeout;
        });
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task SlowCommand_Should_ThrowTimeoutException_AndCancelHandler()
    {
        SlowCommandHandler.WasCancelled = false;

        await using var sp = BuildServices(TimeSpan.FromMilliseconds(100));
        var mediator = sp.GetRequiredService<IMediator>();

        await Assert.ThrowsAsync<TimeoutException>(async () =>
            await mediator.Send(new SlowCommand(), TestCancel.Token));

        Assert.True(SlowCommandHandler.WasCancelled);
    }
}
