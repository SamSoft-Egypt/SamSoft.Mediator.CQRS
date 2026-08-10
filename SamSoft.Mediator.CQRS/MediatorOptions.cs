namespace SamSoft.Mediator.CQRS;

/// <summary>
/// Configuration for <see cref="ServiceCollectionExtensions.AddMediatorService"/>.
/// Every property is applied by <c>AddMediatorService</c>.
/// </summary>
public sealed class MediatorOptions
{
    /// <summary>
    /// Lifetime used when registering <see cref="IMediator"/>. Default is <see cref="ServiceLifetime.Scoped"/>.
    /// </summary>
    public ServiceLifetime Lifetime { get; set; } = ServiceLifetime.Scoped;

    /// <summary>
    /// Default publish strategy when a notification type has no
    /// <see cref="Abstractions.NotificationPublishStrategyAttribute"/>. Default is Parallel.
    /// </summary>
    public NotificationPublishStrategy DefaultNotificationPublishStrategy { get; set; } =
        NotificationPublishStrategy.Parallel;

    /// <summary>
    /// When true, registers <see cref="TimeoutBehavior{TRequest,TResponse}"/>.
    /// </summary>
    public bool RegisterTimeoutBehavior { get; set; }

    /// <summary>
    /// When true, registers <see cref="PrePostProcessorBehavior{TRequest,TResponse}"/>.
    /// </summary>
    public bool RegisterPrePostProcessorBehavior { get; set; }

    /// <summary>
    /// When true, registers <see cref="Pipelines.Validation.ValidationBehavior{TRequest,TResponse}"/> for commands and
    /// queries.
    /// </summary>
    public bool RegisterValidationBehavior { get; set; }

    /// <summary>
    /// When true, registers <see cref="LoggingPipelineBehavior{TRequest,TResponse}"/>. Full request payloads are only
    /// written at Debug level.
    /// </summary>
    public bool RegisterLoggingBehavior { get; set; }

    /// <summary>
    /// Assemblies scanned for handlers and FluentValidation validators.
    /// </summary>
    public IList<Assembly> AssembliesToRegister { get; } = [];

    /// <summary>
    /// When <see cref="AssembliesToRegister"/> is empty, scan the calling assembly. Set to <c>false</c> when handlers
    /// are registered manually.
    /// </summary>
    public bool RegisterHandlersFromCallingAssembly { get; set; } = true;

    /// <summary>
    /// Extra pipeline behaviors to register (typically open-generic <see cref="IPipelineBehavior{TRequest,TResponse}"/>
    /// implementations). Prefer <see cref="AddOpenBehavior"/>.
    /// </summary>
    public IList<ServiceDescriptor> BehaviorsToRegister { get; } = [];

    /// <summary>
    /// Request pre-processors to register. Prefer <see cref="AddRequestPreProcessor"/>.
    /// </summary>
    public IList<ServiceDescriptor> RequestPreProcessorsToRegister { get; } = [];

    /// <summary>
    /// Request post-processors to register. Prefer <see cref="AddRequestPostProcessor"/>.
    /// </summary>
    public IList<ServiceDescriptor> RequestPostProcessorsToRegister { get; } = [];

    /// <summary>
    /// Timeout used by <see cref="TimeoutBehavior{TRequest,TResponse}"/> when that behavior is registered.
    /// </summary>
    public TimeoutSettings TimeoutSettings { get; } = new();

    public void RegisterServicesFromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        AssembliesToRegister.Add(assembly);
    }

    public void RegisterServicesFromAssemblies(params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        foreach (var assembly in assemblies)
        {
            RegisterServicesFromAssembly(assembly);
        }
    }

    /// <summary>
    /// Registers an open-generic pipeline behavior (e.g. <c>typeof(AdvancedLoggingBehavior&lt;,&gt;)</c>).
    /// Default lifetime is <see cref="ServiceLifetime.Scoped"/>.
    /// </summary>
    public void AddOpenBehavior(Type openBehaviorType, ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        ArgumentNullException.ThrowIfNull(openBehaviorType);
        if (!openBehaviorType.IsGenericTypeDefinition)
        {
            throw new ArgumentException(
                $"{openBehaviorType.Name} must be an open generic type definition.",
                nameof(openBehaviorType));
        }

        BehaviorsToRegister.Add(
            new ServiceDescriptor(typeof(IPipelineBehavior<,>), openBehaviorType, lifetime));
    }

    /// <summary>
    /// Registers an open-generic <see cref="IRequestPreProcessor{TRequest}"/>.
    /// </summary>
    public void AddRequestPreProcessor(Type openPreProcessorType, ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        ArgumentNullException.ThrowIfNull(openPreProcessorType);
        if (!openPreProcessorType.IsGenericTypeDefinition)
        {
            throw new ArgumentException(
                $"{openPreProcessorType.Name} must be an open generic type definition.",
                nameof(openPreProcessorType));
        }

        RequestPreProcessorsToRegister.Add(
            new ServiceDescriptor(typeof(IRequestPreProcessor<>), openPreProcessorType, lifetime));
    }

    /// <summary>
    /// Registers an open-generic <see cref="IRequestPostProcessor{TRequest,TResponse}"/>.
    /// </summary>
    public void AddRequestPostProcessor(Type openPostProcessorType, ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        ArgumentNullException.ThrowIfNull(openPostProcessorType);
        if (!openPostProcessorType.IsGenericTypeDefinition)
        {
            throw new ArgumentException(
                $"{openPostProcessorType.Name} must be an open generic type definition.",
                nameof(openPostProcessorType));
        }

        RequestPostProcessorsToRegister.Add(
            new ServiceDescriptor(typeof(IRequestPostProcessor<,>), openPostProcessorType, lifetime));
    }
}
