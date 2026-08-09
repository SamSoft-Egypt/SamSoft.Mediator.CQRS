using SamSoft.Mediator.CQRS.Abstractions.Requests;
using SamSoft.Mediator.CQRS.Handlers.Notifications;
using SamSoft.Mediator.CQRS.Pipelines;

namespace SamSoft.Mediator.CQRS;

public static class ServiceCollectionExtensions
{
    private static readonly Type[] HandlerInterfaceDefinitions =
    [
        typeof(ICommandHandler<>),
        typeof(ICommandHandler<,>),
        typeof(IQueryHandler<,>),
        typeof(INotificationHandler<>),
        typeof(IRequestHandlerBase<,>)
    ];

    /// <summary>
    /// Registers the mediator, scans assemblies for handlers/validators, and applies <see cref="MediatorOptions"/>.
    /// </summary>
    public static IServiceCollection AddMediatorService(
        this IServiceCollection services,
        Action<MediatorOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new MediatorOptions();
        configure?.Invoke(options);

        var assemblies = options.AssembliesToRegister.Count > 0
            ? options.AssembliesToRegister.Distinct().ToArray()
            : options.RegisterHandlersFromCallingAssembly
                ? [Assembly.GetCallingAssembly()]
                : [];

        if (assemblies.Length > 0)
        {
            RegisterHandlers(services, assemblies);
            services.AddValidatorsFromAssemblies(assemblies, includeInternalTypes: true);
        }

        if (options.RegisterTimeoutBehavior)
        {
            options.AddOpenBehavior(typeof(TimeoutBehavior<,>));
        }

        if (options.RegisterPrePostProcessorBehavior)
        {
            options.AddOpenBehavior(typeof(PrePostProcessorBehavior<,>));
        }

        if (options.RegisterValidationBehavior)
        {
            options.AddOpenBehavior(typeof(ValidationBehavior<,>));
        }

        foreach (var behavior in options.BehaviorsToRegister)
        {
            services.TryAddEnumerable(behavior);
        }

        foreach (var pre in options.RequestPreProcessorsToRegister)
        {
            services.TryAddEnumerable(pre);
        }

        foreach (var post in options.RequestPostProcessorsToRegister)
        {
            services.TryAddEnumerable(post);
        }

        var timeout = options.TimeoutSettings.Timeout;
        services.Configure<TimeoutSettings>(settings => settings.Timeout = timeout);

        var defaultPublishStrategy = options.DefaultNotificationPublishStrategy;
        services.TryAddSingleton<INotificationPublisher>(
            _ => new StrategyAwareNotificationPublisher(defaultPublishStrategy));

        services.TryAdd(new ServiceDescriptor(typeof(IMediator), typeof(Mediator), options.Lifetime));
        services.TryAddTransient<ISender>(sp => sp.GetRequiredService<IMediator>());
        services.TryAddTransient<IPublisher>(sp => sp.GetRequiredService<IMediator>());

        return services;
    }

    /// <summary>
    /// Convenience overload that scans the given assemblies and enables timeout + pre/post processor behaviors.
    /// Mediator lifetime defaults to <see cref="ServiceLifetime.Scoped"/>.
    /// </summary>
    public static IServiceCollection AddMediatorService(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Capture calling assembly before any further calls rewrite the stack.
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

            options.RegisterTimeoutBehavior = true;
            options.RegisterPrePostProcessorBehavior = true;
        });
    }

    /// <summary>
    /// Registers an open-generic pipeline behavior using <see cref="ServiceCollectionDescriptorExtensions.TryAddEnumerable"/>.
    /// </summary>
    public static IServiceCollection AddOpenBehavior(
        this IServiceCollection services,
        Type openBehaviorType,
        ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(openBehaviorType);

        if (!openBehaviorType.IsGenericTypeDefinition)
        {
            throw new InvalidOperationException($"{openBehaviorType.Name} must be an open generic type definition.");
        }

        var implementsPipeline = openBehaviorType
            .GetInterfaces()
            .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IPipelineBehavior<,>));

        if (!implementsPipeline)
        {
            throw new InvalidOperationException(
                $"{openBehaviorType.Name} must implement {typeof(IPipelineBehavior<,>).FullName}.");
        }

        services.TryAddEnumerable(
            new ServiceDescriptor(typeof(IPipelineBehavior<,>), openBehaviorType, serviceLifetime));

        return services;
    }

    /// <summary>
    /// Registers one or more open-generic pipeline behaviors.
    /// </summary>
    public static IServiceCollection AddPipelineBehaviors(
        this IServiceCollection services,
        params Type[] pipelineBehaviors)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(pipelineBehaviors);

        foreach (var behavior in pipelineBehaviors)
        {
            services.AddOpenBehavior(behavior);
        }

        return services;
    }

    private static void RegisterHandlers(IServiceCollection services, IReadOnlyList<Assembly> assemblies)
    {
        var types = assemblies
            .SelectMany(static a => a.GetTypes())
            .Where(static t => !t.IsAbstract && !t.IsInterface);

        foreach (var type in types)
        {
            foreach (var handlerInterface in type.GetInterfaces()
                         .Where(static i =>
                             i.IsGenericType &&
                             HandlerInterfaceDefinitions.Contains(i.GetGenericTypeDefinition())))
            {
                // AddTransient (not TryAdd): multiple INotificationHandler<T> implementations must all register.
                services.AddTransient(handlerInterface, type);
            }
        }
    }
}
