using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CustomMediator;

public static class ServiceCollectionExtensions
{
    private static readonly Type[] ScannedInterfaces =
        [typeof(IRequestHandler<,>), typeof(INotificationHandler<>), typeof(IValidator<>)];

    public static IServiceCollection AddCustomMediator(this IServiceCollection services, Action<MediatorOptions> configure)
    {
        var options = new MediatorOptions();
        configure(options);

        RegisterCoreServices(services, options);
        RegisterBehaviors(services, options);
        RegisterHandlers(services, options.Assemblies);

        return services;
    }

    private static void RegisterCoreServices(IServiceCollection services, MediatorOptions options)
    {
        services.TryAddSingleton(typeof(INotificationPublisher), options.NotificationPublisherType);
        services.TryAddTransient<IMediator, Mediator>();
        services.TryAddTransient<ISender>(sp => sp.GetRequiredService<IMediator>());
        services.TryAddTransient<IPublisher>(sp => sp.GetRequiredService<IMediator>());
    }

    private static void RegisterBehaviors(IServiceCollection services, MediatorOptions options)
    {
        foreach (var behavior in options.Behaviors)
        {
            services.AddTransient(typeof(IPipelineBehavior<,>), behavior);
        }
    }

    /// <summary>Registers every closed handler and validator implementation found in the given assemblies.</summary>
    private static void RegisterHandlers(IServiceCollection services, IEnumerable<Assembly> assemblies)
    {
        var concreteTypes = assemblies.Distinct()
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false });

        foreach (var type in concreteTypes)
        {
            foreach (var serviceType in type.GetInterfaces().Where(IsScannedInterface))
            {
                services.AddTransient(serviceType, type);
            }
        }
    }

    private static bool IsScannedInterface(Type type) =>
        type.IsGenericType && ScannedInterfaces.Contains(type.GetGenericTypeDefinition());
}
