using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CustomMediator;

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

    public MediatorOptions UseNotificationPublisher<TPublisher>() where TPublisher : class, INotificationPublisher
    {
        NotificationPublisherType = typeof(TPublisher);
        return this;
    }
}

public static class ServiceCollectionExtensions
{
    private static readonly Type[] ScannedInterfaces =
        [typeof(IRequestHandler<,>), typeof(INotificationHandler<>), typeof(IValidator<>)];

    public static IServiceCollection AddCustomMediator(this IServiceCollection services, Action<MediatorOptions> configure)
    {
        var options = new MediatorOptions();
        configure(options);

        services.TryAddSingleton(typeof(INotificationPublisher), options.NotificationPublisherType);
        services.TryAddTransient<IMediator, Mediator>();
        services.TryAddTransient<ISender>(sp => sp.GetRequiredService<IMediator>());
        services.TryAddTransient<IPublisher>(sp => sp.GetRequiredService<IMediator>());

        foreach (var behavior in options.Behaviors)
        {
            services.AddTransient(typeof(IPipelineBehavior<,>), behavior);
        }

        foreach (var type in options.Assemblies.Distinct().SelectMany(a => a.GetTypes()))
        {
            if (type is not { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }) continue;

            foreach (var iface in type.GetInterfaces())
            {
                if (iface.IsGenericType && ScannedInterfaces.Contains(iface.GetGenericTypeDefinition()))
                {
                    services.AddTransient(iface, type);
                }
            }
        }

        return services;
    }
}
