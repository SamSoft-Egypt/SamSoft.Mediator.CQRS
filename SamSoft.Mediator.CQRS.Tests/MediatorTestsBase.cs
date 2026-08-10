using Microsoft.Extensions.DependencyInjection;
using SamSoft.Mediator.CQRS.Tests.TestObjects;

namespace SamSoft.Mediator.CQRS.Tests;

public class MediatorTestsBase
{
    public static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddMediatorService(assemblies: [typeof(MediatorTests).Assembly])
            .AddPipelineBehaviors([typeof(DummyValidationBehavior<,>), typeof(DummyLoggingBehavior<,>)]);
        return services.BuildServiceProvider();
    }
}
