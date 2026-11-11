using CustomMediator;

namespace Demo.Api.Orders;

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
