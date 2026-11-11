namespace CustomMediator;

/// <summary>Marker for a request that returns <typeparamref name="TResponse"/>.</summary>
public interface IRequest<out TResponse>;

/// <summary>Marker for a notification that can have zero or many handlers.</summary>
public interface INotification;

/// <summary>Return type for requests that have no meaningful result (commands).</summary>
public readonly record struct Unit
{
    public static readonly Unit Value = new();
    public static readonly Task<Unit> Task = System.Threading.Tasks.Task.FromResult(Value);
}

public interface IRequestHandler<in TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    Task<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken);
}

public interface INotificationHandler<in TNotification> where TNotification : INotification
{
    Task HandleAsync(TNotification notification, CancellationToken cancellationToken);
}

/// <summary>The next step in the pipeline: either another behavior or the handler itself.</summary>
public delegate Task<TResponse> RequestHandlerDelegate<TResponse>();

/// <summary>
/// Wraps a request handler (cross-cutting concerns: validation, logging, transactions...).
/// Behaviors run in registration order; the first registered is the outermost.
/// </summary>
public interface IPipelineBehavior<in TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}

public interface ISender
{
    Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
}

public interface IPublisher
{
    Task PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification;
}

public interface IMediator : ISender, IPublisher;

/// <summary>Strategy for running the handlers of one notification.</summary>
public interface INotificationPublisher
{
    Task PublishAsync<TNotification>(
        IEnumerable<INotificationHandler<TNotification>> handlers,
        TNotification notification,
        CancellationToken cancellationToken) where TNotification : INotification;
}

public sealed class HandlerNotFoundException(Type requestType)
    : InvalidOperationException($"No handler registered for request type '{requestType.Name}'.")
{
    public Type RequestType { get; } = requestType;
}
