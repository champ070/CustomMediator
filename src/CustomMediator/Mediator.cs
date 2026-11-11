using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace CustomMediator;

public sealed class Mediator(IServiceProvider serviceProvider, INotificationPublisher notificationPublisher) : IMediator
{
    // One cached wrapper per request type. Reflection (MakeGenericType/Activator) runs once per type;
    // every later call is a plain virtual call into strongly typed code.
    // The key includes TResponse because one request type may implement IRequest<> more than once
    // (e.g. IRequest<int> and IRequest<string>); each pairing needs its own wrapper.
    private static readonly ConcurrentDictionary<(Type Request, Type Response), object> Wrappers = new();

    // Contract: argument errors (null request) throw synchronously, like any guard clause.
    // Everything else, including a cancelled token, a missing handler, and exceptions thrown by handlers
    // or behaviors before they return a Task, is reported through the returned Task.
    public Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (cancellationToken.IsCancellationRequested) return Task.FromCanceled<TResponse>(cancellationToken);

        try
        {
            var wrapper = (RequestHandlerWrapper<TResponse>)Wrappers.GetOrAdd((request.GetType(), typeof(TResponse)), static key =>
                Activator.CreateInstance(typeof(TypedRequestHandlerWrapper<,>).MakeGenericType(key.Request, key.Response))!);

            return wrapper.HandleAsync(request, serviceProvider, cancellationToken);
        }
        catch (Exception ex)
        {
            return Task.FromException<TResponse>(ex);
        }
    }

    public Task PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        ArgumentNullException.ThrowIfNull(notification);
        if (cancellationToken.IsCancellationRequested) return Task.FromCanceled(cancellationToken);

        try
        {
            var handlers = serviceProvider.GetServices<INotificationHandler<TNotification>>();
            return notificationPublisher.PublishAsync(handlers, notification, cancellationToken);
        }
        catch (Exception ex)
        {
            return Task.FromException(ex);
        }
    }
}
