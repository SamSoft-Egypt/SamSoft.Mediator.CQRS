using SamSoft.Mediator.CQRS.Pipelines;

namespace SamSoft.Mediator.CQRS.Extensions;

/// <summary>
/// Compatibility aliases for older registration APIs. Prefer
/// <see cref="ServiceCollectionExtensions.AddMediatorService(IServiceCollection, Action{MediatorOptions}?)"/>.
/// </summary>
public static class MediatorCqrsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the mediator by scanning the given assemblies (or the calling assembly).
    /// Prefer <see cref="ServiceCollectionExtensions.AddMediatorService(IServiceCollection, Action{MediatorOptions}?)"/>.
    /// </summary>
    public static IServiceCollection AddMediatorCQRS(
        this IServiceCollection services,
        Assembly[]? assemblies = null,
        bool addDefaultLogging = true)
    {
        ArgumentNullException.ThrowIfNull(services);

        var fallbackAssembly = Assembly.GetCallingAssembly();

        return services.AddMediatorService(options =>
        {
            if (assemblies is { Length: > 0 })
            {
                options.RegisterServicesFromAssemblies(assemblies);
            }
            else
            {
                options.RegisterServicesFromAssembly(fallbackAssembly);
            }

            // Preserved for signature compatibility; logging is opt-in via pipeline behaviors.
            _ = addDefaultLogging;
        });
    }
}
