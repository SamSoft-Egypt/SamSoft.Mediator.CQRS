using SamSoft.Mediator.CQRS.Pipelines.Validation;

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

    private static readonly Type[] UniqueHandlerInterfaceDefinitions =
    [
        typeof(ICommandHandler<>),
        typeof(ICommandHandler<,>),
        typeof(IQueryHandler<,>),
        typeof(IRequestHandlerBase<,>)
    ];

    /// <summary>
    /// Registers the mediator and applies every <see cref="MediatorOptions"/> setting.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static IServiceCollection AddMediatorService(
        this IServiceCollection services,
        Action<MediatorOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Must capture before any further calls; GetCallingAssembly inside ApplyMediatorOptions
        // would return this library assembly, not the consumer.
        var callingAssembly = Assembly.GetCallingAssembly();

        var options = new MediatorOptions();
        configure?.Invoke(options);
        ApplyMediatorOptions(services, options, callingAssembly);
        return services;
    }

    /// <summary>
    /// Convenience overload that scans the given assemblies and enables timeout + pre/post processor behaviors.
    /// Mediator lifetime defaults to <see cref="ServiceLifetime.Scoped"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static IServiceCollection AddMediatorService(
        this IServiceCollection services,
        params Assembly[] assemblies)
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

            options.RegisterTimeoutBehavior = true;
            options.RegisterPrePostProcessorBehavior = true;
        });
    }

    /// <summary>
    /// Applies all <see cref="MediatorOptions"/> values to <paramref name="services"/>.
    /// </summary>
    private static void ApplyMediatorOptions(
        IServiceCollection services,
        MediatorOptions options,
        Assembly callingAssembly)
    {
        // --- AssembliesToRegister / RegisterHandlersFromCallingAssembly ---
        var assemblies = options.AssembliesToRegister.Count > 0
            ? options.AssembliesToRegister.Distinct().ToArray()
            : options.RegisterHandlersFromCallingAssembly
                ? [callingAssembly]
                : [];

        if (assemblies.Length > 0)
        {
            RegisterHandlers(services, assemblies);
            services.AddValidatorsFromAssemblies(assemblies, includeInternalTypes: true);
        }

        // --- Built-in behavior flags (order: Timeout → PrePost → Validation → Logging) ---
        // Reverse()+Aggregate makes the first registered behavior outermost.
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

        if (options.RegisterLoggingBehavior)
        {
            options.AddOpenBehavior(typeof(LoggingPipelineBehavior<,>));
        }

        // --- BehaviorsToRegister (custom + builtins added above) ---
        foreach (var behavior in options.BehaviorsToRegister)
        {
            services.TryAddEnumerable(behavior);
        }

        // --- RequestPreProcessorsToRegister / RequestPostProcessorsToRegister ---
        foreach (var pre in options.RequestPreProcessorsToRegister)
        {
            services.TryAddEnumerable(pre);
        }

        foreach (var post in options.RequestPostProcessorsToRegister)
        {
            services.TryAddEnumerable(post);
        }

        // --- TimeoutSettings ---
        var timeout = options.TimeoutSettings.Timeout;
        services.Configure<TimeoutSettings>(settings => settings.Timeout = timeout);

        // --- DefaultNotificationPublishStrategy ---
        var defaultPublishStrategy = options.DefaultNotificationPublishStrategy;
        services.TryAddSingleton<INotificationPublisher>(
            _ => new StrategyAwareNotificationPublisher(defaultPublishStrategy));

        // --- Lifetime ---
        services.TryAdd(new ServiceDescriptor(typeof(IMediator), typeof(Mediator), options.Lifetime));
        services.TryAddTransient<ISender>(sp => sp.GetRequiredService<IMediator>());
        services.TryAddTransient<IPublisher>(sp => sp.GetRequiredService<IMediator>());
    }

    /// <summary>
    /// Registers an open-generic pipeline behavior using
    /// <see cref="ServiceCollectionDescriptorExtensions.TryAddEnumerable"/>.
    /// </summary>
    public static IServiceCollection AddOpenBehavior(
        this IServiceCollection services,
        Type openBehaviorType,
        ServiceLifetime serviceLifetime = ServiceLifetime.Scoped)
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
        foreach (var type in EnumerateLoadableTypes(assemblies))
        {
            foreach (var handlerInterface in type.GetInterfaces()
                         .Where(static i =>
                             i.IsGenericType &&
                             HandlerInterfaceDefinitions.Contains(i.GetGenericTypeDefinition())))
            {
                var definition = handlerInterface.GetGenericTypeDefinition();
                if (UniqueHandlerInterfaceDefinitions.Contains(definition))
                {
                    var existing = services.FirstOrDefault(d => d.ServiceType == handlerInterface);
                    if (existing?.ImplementationType is { } existingType && existingType != type)
                    {
                        throw new InvalidOperationException(
                            $"Multiple handlers registered for '{handlerInterface}': '{existingType}' and '{type}'.");
                    }

                    services.TryAddTransient(handlerInterface, type);
                }
                else
                {
                    // Notifications (and any future multi-handler interfaces): register every implementation.
                    services.AddTransient(handlerInterface, type);
                }
            }
        }
    }

    private static IEnumerable<Type> EnumerateLoadableTypes(IReadOnlyList<Assembly> assemblies)
    {
        foreach (var assembly in assemblies)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(static t => t is not null).Cast<Type>().ToArray();
            }

            foreach (var type in types)
            {
                if (type is { IsAbstract: false, IsInterface: false })
                {
                    yield return type;
                }
            }
        }
    }
}
