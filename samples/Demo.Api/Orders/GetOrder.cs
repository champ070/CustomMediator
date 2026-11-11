using CustomMediator;

namespace Demo.Api.Orders;

// Query: reads an order, or null when it does not exist.
public record GetOrder(Guid Id) : IRequest<Order?>;

public class GetOrderHandler(IOrderRepository orders) : IRequestHandler<GetOrder, Order?>
{
    public Task<Order?> HandleAsync(GetOrder request, CancellationToken cancellationToken)
        => Task.FromResult(orders.Find(request.Id));
}
