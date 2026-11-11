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
                Activator.CreateInstance(typeof(RequestHandlerWrapperImpl<,>).MakeGenericType(key.Request, key.Response))!);

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

        // Microsoft DI returns an array here, so the cast avoids an extra copy.
        var behaviors = sp.GetServices<IPipelineBehavior<TRequest, TResponse>>();
        var list = behaviors as IReadOnlyList<IPipelineBehavior<TRequest, TResponse>> ?? [.. behaviors];

        // Fast path: no behaviors registered, so call the handler directly (no closures, no delegates).
        if (list.Count == 0) return handler.HandleAsync(typedRequest, ct);

        RequestHandlerDelegate<TResponse> next = () => handler.HandleAsync(typedRequest, ct);

        // Build the chain inside-out so the first registered behavior ends up outermost.
        for (var i = list.Count - 1; i >= 0; i--)
        {
            var behavior = list[i];
            var inner = next;
            next = () => behavior.HandleAsync(typedRequest, inner, ct);
        }

        return next();
    }
}
