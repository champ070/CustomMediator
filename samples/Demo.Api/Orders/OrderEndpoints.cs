using CustomMediator;

namespace Demo.Api.Orders;

public static class OrderEndpoints
{
    /// <summary>Each endpoint is a single SendAsync call; all logic lives in the handlers.</summary>
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var orders = app.MapGroup("/orders");

        orders.MapPost("/", async (CreateOrder command, ISender sender, CancellationToken cancellationToken) =>
        {
            var id = await sender.SendAsync(command, cancellationToken);
            return Results.Created($"/orders/{id}", new { id });
        });

        orders.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
            await sender.SendAsync(new GetOrder(id), cancellationToken) is { } order ? Results.Ok(order) : Results.NotFound());

        return app;
    }
}
