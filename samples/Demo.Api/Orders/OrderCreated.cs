using CustomMediator;

namespace Demo.Api.Orders;

// Notification: one event, several independent handlers.
public record OrderCreated(Guid OrderId, string Customer) : INotification;

public class SendConfirmationEmail(ILogger<SendConfirmationEmail> logger) : INotificationHandler<OrderCreated>
{
    public Task HandleAsync(OrderCreated notification, CancellationToken cancellationToken)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Pretending to email {Customer} about order {OrderId}", notification.Customer, notification.OrderId);
        }

        return Task.CompletedTask;
    }
}

public class UpdateAnalytics(ILogger<UpdateAnalytics> logger) : INotificationHandler<OrderCreated>
{
    public Task HandleAsync(OrderCreated notification, CancellationToken cancellationToken)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Analytics: order {OrderId} created", notification.OrderId);
        }

        return Task.CompletedTask;
    }
}
