using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace CustomMediator;

public sealed class Mediator(IServiceProvider serviceProvider, INotificationPublisher notificationPublisher) : IMediator
{
    // One cached wrapper per request type. Reflection (MakeGenericType/Activator) runs once per type;
    // every later call is a plain virtual call into strongly typed code.
    private static readonly ConcurrentDictionary<Type, object> Wrappers = new();

    public Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var requestType = request.GetType();
        var wrapper = (RequestHandlerWrapper<TResponse>)Wrappers.GetOrAdd(requestType, static (type, _) =>
            Activator.CreateInstance(typeof(RequestHandlerWrapperImpl<,>).MakeGenericType(type, typeof(TResponse)))!, 0);

        return wrapper.HandleAsync(request, serviceProvider, cancellationToken);
    }

    public Task PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        ArgumentNullException.ThrowIfNull(notification);
        cancellationToken.ThrowIfCancellationRequested();

        var handlers = serviceProvider.GetServices<INotificationHandler<TNotification>>();
        return notificationPublisher.PublishAsync(handlers, notification, cancellationToken);
    }
}

internal abstract class RequestHandlerWrapper<TResponse>
{
    public abstract Task<TResponse> HandleAsync(IRequest<TResponse> request, IServiceProvider sp, CancellationToken ct);
}

internal sealed class RequestHandlerWrapperImpl<TRequest, TResponse> : RequestHandlerWrapper<TResponse>
    where TRequest : IRequest<TResponse>
{
    public override Task<TResponse> HandleAsync(IRequest<TResponse> request, IServiceProvider sp, CancellationToken ct)
    {
        var handler = sp.GetService<IRequestHandler<TRequest, TResponse>>()
                      ?? throw new HandlerNotFoundException(typeof(TRequest));

        var typedRequest = (TRequest)request;
        RequestHandlerDelegate<TResponse> next = () => handler.HandleAsync(typedRequest, ct);

        // Build the chain inside-out so the first registered behavior ends up outermost.
        foreach (var behavior in sp.GetServices<IPipelineBehavior<TRequest, TResponse>>().Reverse())
        {
            var inner = next;
            next = () => behavior.HandleAsync(typedRequest, inner, ct);
        }

        return next();
    }
}
