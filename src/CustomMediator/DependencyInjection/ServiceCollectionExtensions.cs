using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CustomMediator;

public static class ServiceCollectionExtensions
{
    private static readonly Type[] ScannedInterfaces =
        [typeof(IRequestHandler<,>), typeof(INotificationHandler<>), typeof(IValidator<>)];

    /// <summary>
    /// Registers the mediator, the behaviors and the handlers found in the configured assemblies.
    /// </summary>
    /// <remarks>
    /// Safe to call more than once (for example from several modules): a handler, validator or behavior that is
    /// already registered is not added again, so nothing runs twice. The notification publisher is the one
    /// registered first; a later <see cref="MediatorOptions.UseNotificationPublisher{TPublisher}"/> does not replace it.
    /// </remarks>
    public static IServiceCollection AddCustomMediator(this IServiceCollection services, Action<MediatorOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

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
        services.TryAddTransient<ISender>(serviceProvider => serviceProvider.GetRequiredService<IMediator>());
        services.TryAddTransient<IPublisher>(serviceProvider => serviceProvider.GetRequiredService<IMediator>());
    }

    private static void RegisterBehaviors(IServiceCollection services, MediatorOptions options)
    {
        foreach (var behavior in options.Behaviors)
        {
            services.TryAddEnumerable(ServiceDescriptor.Transient(typeof(IPipelineBehavior<,>), behavior));
        }
    }

    /// <summary>Registers every closed handler and validator implementation found in the given assemblies.</summary>
    private static void RegisterHandlers(IServiceCollection services, IEnumerable<Assembly> assemblies)
    {
        var concreteTypes = assemblies.Distinct()
            .SelectMany(GetLoadableTypes)
            .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false });

        foreach (var type in concreteTypes)
        {
            foreach (var serviceType in type.GetInterfaces().Where(IsScannedInterface))
            {
                services.TryAddEnumerable(ServiceDescriptor.Transient(serviceType, type));
            }
        }
    }

    private static bool IsScannedInterface(Type type) =>
        type.IsGenericType && ScannedInterfaces.Contains(type.GetGenericTypeDefinition());

    /// <summary>
    /// <see cref="Assembly.GetTypes"/> throws when a dependency of the assembly is missing; in that case use the
    /// types that did load instead of failing the whole registration.
    /// </summary>
    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.OfType<Type>();
        }
    }
}
