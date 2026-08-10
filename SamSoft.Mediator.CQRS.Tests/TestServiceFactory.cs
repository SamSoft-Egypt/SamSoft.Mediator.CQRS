using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace SamSoft.Mediator.CQRS.Tests;

internal static class TestServiceFactory
{
    public static ServiceProvider Create(
        Action<MediatorOptions>? configure = null,
        Action<IServiceCollection>? configureServices = null,
        bool scanTestAssembly = true)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddMediatorService(options =>
        {
            if (scanTestAssembly)
            {
                options.RegisterServicesFromAssembly(typeof(TestServiceFactory).Assembly);
            }
            else
            {
                options.RegisterHandlersFromCallingAssembly = false;
            }

            configure?.Invoke(options);
        });

        configureServices?.Invoke(services);
        return services.BuildServiceProvider();
    }

    public static ServiceProvider CreateWithAssemblies(params Assembly[] assemblies)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediatorService(assemblies);
        return services.BuildServiceProvider();
    }
}
