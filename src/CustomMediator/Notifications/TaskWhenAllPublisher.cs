namespace CustomMediator;

/// <summary>
/// Starts all handlers concurrently and waits for all of them. If several fail, every failure is
/// reported through one <see cref="AggregateException"/> (plain <c>await Task.WhenAll</c> would only surface the first).
/// </summary>
public sealed class TaskWhenAllPublisher : INotificationPublisher
{
    public async Task PublishAsync<TNotification>(
        IEnumerable<INotificationHandler<TNotification>> handlers,
        TNotification notification,
        CancellationToken cancellationToken) where TNotification : INotification
    {
        var tasks = handlers.Select(handler => Invoke(handler, notification, cancellationToken)).ToArray();
        if (tasks.Length == 0) return;

        var all = Task.WhenAll(tasks);
        try
        {
            await all.ConfigureAwait(false);
        }
        catch when (!all.IsCanceled)
        {
            // Faulted: surface every failure. (If every handler was merely cancelled, all.Exception is null,
            // so that case is deliberately not caught here and the cancellation propagates as-is.)
            throw all.Exception!;
        }
    }

    // A handler may throw before it returns a Task; the async wrapper turns that into a faulted Task
    // so it is collected by Task.WhenAll like any other failure.
    private static async Task Invoke<TNotification>(
        INotificationHandler<TNotification> handler, TNotification notification, CancellationToken cancellationToken)
        where TNotification : INotification
        => await handler.HandleAsync(notification, cancellationToken).ConfigureAwait(false);
}
