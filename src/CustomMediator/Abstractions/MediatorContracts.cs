namespace CustomMediator;

/// <summary>Sends a request to its single handler. Depend on this when you only need to send.</summary>
public interface ISender
{
    Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
}

/// <summary>Publishes a notification to all of its handlers. Depend on this when you only need to publish.</summary>
public interface IPublisher
{
    Task PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification;
}

public interface IMediator : ISender, IPublisher;
