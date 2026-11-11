using System.Collections.Concurrent;
using CustomMediator;
using Demo.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<OrderStore>();
builder.Services.AddCustomMediator(o => o
    .RegisterServicesFromAssembly(typeof(Program).Assembly)
    .AddOpenBehavior(typeof(LoggingBehavior<,>))      // outermost: times everything below it
    .AddOpenBehavior(typeof(ValidationBehavior<,>))); // runs before the handler

var app = builder.Build();

// Command: creates an order and publishes an OrderCreated notification.
app.MapPost("/orders", async (CreateOrder command, ISender sender) =>
{
    var id = await sender.SendAsync(command);
    return Results.Created($"/orders/{id}", new { id });
});

// Query: reads an order.
app.MapGet("/orders/{id:guid}", async (Guid id, ISender sender) =>
    await sender.SendAsync(new GetOrder(id)) is { } order ? Results.Ok(order) : Results.NotFound());

// Validation failures from the pipeline become 400 responses.
app.Use(async (ctx, next) =>
{
    try { await next(); }
    catch (RequestValidationException ex)
    {
        ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
        await ctx.Response.WriteAsJsonAsync(new { errors = ex.Errors });
    }
});

app.Run();

namespace Demo.Api
{
    public record Order(Guid Id, string Customer, decimal Total);

    public sealed class OrderStore
    {
        public ConcurrentDictionary<Guid, Order> Orders { get; } = new();
    }

    public record CreateOrder(string Customer, decimal Total) : IRequest<Guid>;
    public record GetOrder(Guid Id) : IRequest<Order?>;
    public record OrderCreated(Guid OrderId, string Customer) : INotification;

    public class CreateOrderValidator : IValidator<CreateOrder>
    {
        public IEnumerable<string> Validate(CreateOrder r)
        {
            if (string.IsNullOrWhiteSpace(r.Customer)) yield return "Customer is required.";
            if (r.Total <= 0) yield return "Total must be greater than zero.";
        }
    }

    public class CreateOrderHandler(OrderStore store, IPublisher publisher) : IRequestHandler<CreateOrder, Guid>
    {
        public async Task<Guid> HandleAsync(CreateOrder request, CancellationToken ct)
        {
            var order = new Order(Guid.NewGuid(), request.Customer, request.Total);
            store.Orders[order.Id] = order;
            await publisher.PublishAsync(new OrderCreated(order.Id, order.Customer), ct);
            return order.Id;
        }
    }

    public class GetOrderHandler(OrderStore store) : IRequestHandler<GetOrder, Order?>
    {
        public Task<Order?> HandleAsync(GetOrder request, CancellationToken ct)
            => Task.FromResult(store.Orders.GetValueOrDefault(request.Id));
    }

    // Two independent reactions to the same event.
    public class SendConfirmationEmail(ILogger<SendConfirmationEmail> logger) : INotificationHandler<OrderCreated>
    {
        public Task HandleAsync(OrderCreated n, CancellationToken ct)
        {
            logger.LogInformation("Pretending to email {Customer} about order {OrderId}", n.Customer, n.OrderId);
            return Task.CompletedTask;
        }
    }

    public class UpdateAnalytics(ILogger<UpdateAnalytics> logger) : INotificationHandler<OrderCreated>
    {
        public Task HandleAsync(OrderCreated n, CancellationToken ct)
        {
            logger.LogInformation("Analytics: order {OrderId} created", n.OrderId);
            return Task.CompletedTask;
        }
    }
}
