using Microsoft.Extensions.DependencyInjection;
using SamSoft.Mediator.CQRS.Pipelines;
using System.Reflection;

namespace SamSoft.Mediator.CQRS;

/// <summary>
/// Configuration for <see cref="ServiceCollectionExtensions.AddMediatorService"/>.
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
    /// When true, registers <see cref="ValidationBehavior{TRequest,TResponse}"/> for commands and queries.
    /// </summary>
    public bool RegisterValidationBehavior { get; set; }

    /// <summary>
    /// Assemblies scanned for handlers and FluentValidation validators.
    /// </summary>
    public IList<Assembly> AssembliesToRegister { get; } = new List<Assembly>();

    /// <summary>
    /// When <see cref="AssembliesToRegister"/> is empty, scan the calling assembly.
    /// Set to <c>false</c> when handlers are registered manually.
    /// </summary>
    public bool RegisterHandlersFromCallingAssembly { get; set; } = true;

    /// <summary>
    /// Extra pipeline behaviors to register (typically open-generic <see cref="IPipelineBehavior{TRequest,TResponse}"/> implementations).
    /// </summary>
    public IList<ServiceDescriptor> BehaviorsToRegister { get; } = new List<ServiceDescriptor>();

    /// <summary>
    /// Request pre-processors to register.
    /// </summary>
    public IList<ServiceDescriptor> RequestPreProcessorsToRegister { get; } = new List<ServiceDescriptor>();

    /// <summary>
    /// Request post-processors to register.
    /// </summary>
    public IList<ServiceDescriptor> RequestPostProcessorsToRegister { get; } = new List<ServiceDescriptor>();

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
    /// Registers an open-generic pipeline behavior (e.g. <c>typeof(LoggingPipelineBehavior&lt;,&gt;)</c>).
    /// </summary>
    public void AddOpenBehavior(Type openBehaviorType, ServiceLifetime lifetime = ServiceLifetime.Transient)
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
}
