using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Demo.Api.Orders;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using static CustomMediator.Tests.TestHost;

namespace CustomMediator.Tests;

/// <summary>
/// End-to-end tests: real HTTP requests into Demo.Api running in memory, through the full
/// mediator pipeline (logging, validation, handler, notification) and the exception handler.
/// </summary>
public class DemoApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    // One client per test; the in-memory repository is shared per factory instance, which is fine
    // because every test creates its own orders and looks them up by id.
    private HttpClient CreateClient() => factory.CreateClient();

    private static async Task<List<string?>> ReadErrorsAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return body.GetProperty("errors").EnumerateArray().Select(e => e.GetString()).ToList();
    }

    [Fact]
    public async Task Post_valid_order_returns_201_with_location_and_id()
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync("/orders", new CreateOrder("Acme", 42m), Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var id = body.GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal($"/orders/{id}", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Created_order_can_be_read_back()
    {
        using var client = CreateClient();
        var created = await client.PostAsJsonAsync("/orders", new CreateOrder("Globex", 99.5m), Ct);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();

        var response = await client.GetAsync($"/orders/{id}", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var order = await response.Content.ReadFromJsonAsync<Order>(Ct);
        Assert.Equal(new Order(id, "Globex", 99.5m), order);
    }

    [Fact]
    public async Task Get_unknown_order_returns_404()
    {
        using var client = CreateClient();

        var response = await client.GetAsync($"/orders/{Guid.NewGuid()}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_invalid_order_returns_400_with_all_validation_errors()
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync("/orders", new CreateOrder("", 0m), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(["Customer is required.", "Total must be greater than zero."], await ReadErrorsAsync(response));
    }

    [Theory]
    [InlineData("", 10, "Customer is required.")]
    [InlineData("   ", 10, "Customer is required.")]
    [InlineData("Acme", 0, "Total must be greater than zero.")]
    [InlineData("Acme", -5, "Total must be greater than zero.")]
    public async Task Post_order_with_one_invalid_field_reports_only_that_error(string customer, decimal total, string expectedError)
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync("/orders", new CreateOrder(customer, total), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal([expectedError], await ReadErrorsAsync(response));
    }

    [Fact]
    public async Task Invalid_order_is_not_stored()
    {
        using var client = CreateClient();
        var customer = $"rejected-{Guid.NewGuid():N}";

        var response = await client.PostAsJsonAsync("/orders", new CreateOrder(customer, -1m), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var repository = factory.Services.GetRequiredService<IOrderRepository>();
        Assert.DoesNotContain(repository.GetAll(), o => o.Customer == customer);
    }
}
