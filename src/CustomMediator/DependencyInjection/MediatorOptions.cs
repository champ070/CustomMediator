using System.Reflection;

namespace CustomMediator;

/// <summary>Fluent configuration for <see cref="ServiceCollectionExtensions.AddCustomMediator"/>.</summary>
public sealed class MediatorOptions
{
    internal List<Assembly> Assemblies { get; } = [];
    internal List<Type> Behaviors { get; } = [];
    internal Type NotificationPublisherType { get; private set; } = typeof(ForeachAwaitPublisher);

    /// <summary>Scan this assembly for handlers and validators.</summary>
    public MediatorOptions RegisterServicesFromAssembly(Assembly assembly)
    {
        Assemblies.Add(assembly);
        return this;
    }

    /// <summary>Add an open-generic behavior, e.g. <c>typeof(LoggingBehavior&lt;,&gt;)</c>. Order = registration order.</summary>
    public MediatorOptions AddOpenBehavior(Type openBehaviorType)
    {
        if (!openBehaviorType.IsGenericTypeDefinition ||
            !openBehaviorType.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IPipelineBehavior<,>)))
        {
            throw new ArgumentException($"{openBehaviorType.Name} must be an open generic IPipelineBehavior<,>.", nameof(openBehaviorType));
        }

        Behaviors.Add(openBehaviorType);
        return this;
    }

    /// <summary>Choose how the handlers of one notification are run (default: <see cref="ForeachAwaitPublisher"/>).</summary>
    public MediatorOptions UseNotificationPublisher<TPublisher>() where TPublisher : class, INotificationPublisher
    {
        NotificationPublisherType = typeof(TPublisher);
        return this;
    }
}
