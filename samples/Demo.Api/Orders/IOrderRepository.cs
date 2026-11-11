namespace Demo.Api.Orders;

/// <summary>
/// What the handlers need from storage. Handlers depend on this abstraction, so the in-memory
/// implementation can be swapped for a real database without touching them.
/// </summary>
public interface IOrderRepository
{
    void Add(Order order);

    Order? Find(Guid id);

    IReadOnlyCollection<Order> GetAll();
}
