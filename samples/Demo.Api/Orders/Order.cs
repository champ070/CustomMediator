namespace Demo.Api.Orders;

public record Order(Guid Id, string Customer, decimal Total);
