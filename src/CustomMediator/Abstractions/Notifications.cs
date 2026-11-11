namespace CustomMediator;

/// <summary>Marker for a notification that can have zero or many handlers.</summary>
public interface INotification;

public interface INotificationHandler<in TNotification> where TNotification : INotification
{
    Task HandleAsync(TNotification notification, CancellationToken cancellationToken);
}

/// <summary>
/// Strategy for running the handlers of one notification.
/// </summary>
/// <remarks>
/// The contract is deliberately loose: implementations decide the <b>execution order</b> (sequential or
/// concurrent) and the <b>failure policy</b> (stop at the first failure, or run everything and report all
/// failures). Callers must not depend on either. What every implementation must guarantee:
/// <list type="bullet">
/// <item>Each handler is offered the notification at most once.</item>
/// <item>The returned task completes only after every started handler has finished.</item>
/// <item>Failures and cancellation are reported through the returned task, never thrown synchronously.</item>
/// <item>An empty handler list completes successfully.</item>
/// </list>
/// Built-in implementations: <see cref="ForeachAwaitPublisher"/> (sequential, stops at the first failure) and
/// <see cref="TaskWhenAllPublisher"/> (concurrent, aggregates all failures).
/// </remarks>
public interface INotificationPublisher
{
    Task PublishAsync<TNotification>(
        IEnumerable<INotificationHandler<TNotification>> handlers,
        TNotification notification,
        CancellationToken cancellationToken) where TNotification : INotification;
}
