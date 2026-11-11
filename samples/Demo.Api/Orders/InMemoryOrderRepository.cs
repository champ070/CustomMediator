using System.Collections.Concurrent;

namespace Demo.Api.Orders;

/// <summary>Thread-safe in-memory storage so the sample needs no database. The dictionary stays private.</summary>
public sealed class InMemoryOrderRepository : IOrderRepository
{
    private readonly ConcurrentDictionary<Guid, Order> _orders = new();

    public void Add(Order order) => _orders[order.Id] = order;

    public Order? Find(Guid id) => _orders.GetValueOrDefault(id);

    public IReadOnlyCollection<Order> GetAll() => _orders.Values.ToArray();
}
