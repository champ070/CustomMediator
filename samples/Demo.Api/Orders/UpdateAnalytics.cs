using CustomMediator;

namespace Demo.Api.Orders;

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
