using System.Collections.Concurrent;

namespace Demo.Api.Orders;

public record Order(Guid Id, string Customer, decimal Total);

/// <summary>In-memory storage so the sample needs no database.</summary>
public sealed class OrderStore
{
    public ConcurrentDictionary<Guid, Order> Orders { get; } = new();
}
