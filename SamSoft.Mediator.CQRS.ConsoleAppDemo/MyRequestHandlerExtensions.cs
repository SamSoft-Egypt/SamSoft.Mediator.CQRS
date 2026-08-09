using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SamSoft.Mediator.CQRS.Abstractions;
using SamSoft.Mediator.CQRS.Pipelines;

namespace SamSoft.Mediator.CQRS.ConsoleAppDemo;

public static class MyRequestHandlerExtensions
{
    public static ServiceProvider CreateServices()
    {
        var assemblies = new[] { typeof(MyRequestHandlerExtensions).Assembly };

        return new ServiceCollection()
            .AddLogging(configure =>
            {
                configure.AddConsole();
                configure.AddDebug();
            })
            .AddMediatorService(options =>
            {
                options.Lifetime = ServiceLifetime.Scoped;
                options.RegisterServicesFromAssemblies(assemblies);
                options.TimeoutSettings.Timeout = TimeSpan.FromSeconds(10);
                options.RegisterValidationBehavior = true;
                options.AddOpenBehavior(typeof(AdvancedLoggingBehavior<,>));
                options.AddOpenBehavior(typeof(LoggingPipelineBehavior<,>));
            })
            .BuildServiceProvider();
    }
}
