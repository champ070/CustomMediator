using CustomMediator;

namespace Demo.Api.Orders;

// Command: creates an order, then publishes an OrderCreated notification.
public record CreateOrder(string Customer, decimal Total) : IRequest<Guid>;

// Picked up by ValidationBehavior and run before the handler.
public class CreateOrderValidator : IValidator<CreateOrder>
{
    public IEnumerable<string> Validate(CreateOrder request)
    {
        if (string.IsNullOrWhiteSpace(request.Customer)) yield return "Customer is required.";
        if (request.Total <= 0) yield return "Total must be greater than zero.";
    }
}

public class CreateOrderHandler(IOrderRepository orders, IPublisher publisher) : IRequestHandler<CreateOrder, Guid>
{
    public async Task<Guid> HandleAsync(CreateOrder request, CancellationToken cancellationToken)
    {
        var order = new Order(Guid.NewGuid(), request.Customer, request.Total);
        orders.Add(order);
        await publisher.PublishAsync(new OrderCreated(order.Id, order.Customer), cancellationToken);
        return order.Id;
    }
}
