using Microsoft.Extensions.DependencyInjection;

namespace CustomMediator.Tests;

// Types used only by the regression tests below. They are picked up by the assembly scan.
public record CancelsEverything : INotification;
public record SyncThrows : IRequest<int>;
public record Dual : IRequest<int>, IRequest<string>;
public record CountMe(int N) : IRequest<int>;

public class CancelledHandler1 : INotificationHandler<CancelsEverything>
{
    public Task HandleAsync(CancelsEverything n, CancellationToken ct) => Task.FromCanceled(new CancellationToken(true));
}

public class CancelledHandler2 : INotificationHandler<CancelsEverything>
{
    public Task HandleAsync(CancelsEverything n, CancellationToken ct) => Task.FromCanceled(new CancellationToken(true));
}

// Throws BEFORE returning a Task (not inside an async method).
public class SyncThrowsHandler : IRequestHandler<SyncThrows, int>
{
    public Task<int> HandleAsync(SyncThrows request, CancellationToken ct) => throw new InvalidOperationException("boom");
}

public class DualIntHandler : IRequestHandler<Dual, int>
{
    public Task<int> HandleAsync(Dual request, CancellationToken ct) => Task.FromResult(42);
}

public class DualStringHandler : IRequestHandler<Dual, string>
{
    public Task<string> HandleAsync(Dual request, CancellationToken ct) => Task.FromResult("forty-two");
}

public class CountMeHandler : IRequestHandler<CountMe, int>
{
    public async Task<int> HandleAsync(CountMe request, CancellationToken ct)
    {
        await Task.Yield();
        return request.N * 2;
    }
}

public class RegressionTests
{
    private static ServiceProvider Build(Action<MediatorOptions>? options = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new List<string>());
        services.AddCustomMediator(o =>
        {
            o.RegisterServicesFromAssembly(typeof(RegressionTests).Assembly);
            options?.Invoke(o);
        });
        return services.BuildServiceProvider();
    }

    // ---- Bug 1: cancellation was swallowed (empty AggregateException) by TaskWhenAllPublisher ----

    [Fact]
    public async Task WhenAll_publisher_propagates_cancellation_instead_of_an_empty_AggregateException()
    {
        using var sp = Build(o => o.UseNotificationPublisher<TaskWhenAllPublisher>());

        var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sp.GetRequiredService<IMediator>().PublishAsync(new CancelsEverything()));

        Assert.IsNotType<AggregateException>(ex);
    }

    [Fact]
    public async Task WhenAll_publisher_still_reports_failures_when_some_handlers_fault()
    {
        using var sp = Build(o => o.UseNotificationPublisher<TaskWhenAllPublisher>());

        var ex = await Assert.ThrowsAsync<AggregateException>(
            () => sp.GetRequiredService<IMediator>().PublishAsync(new Explodes()));

        Assert.Equal(2, ex.InnerExceptions.Count);
    }

    // ---- Bug 2: synchronous throws escaped SendAsync instead of faulting the returned Task ----

    [Fact]
    public async Task Send_returns_faulted_task_when_handler_throws_synchronously()
    {
        using var sp = Build();

        var task = sp.GetRequiredService<IMediator>().SendAsync(new SyncThrows()); // must NOT throw here

        Assert.True(task.IsFaulted);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => task);
        Assert.Equal("boom", ex.Message);
    }

    [Fact]
    public async Task Send_returns_faulted_task_when_no_handler_is_registered()
    {
        using var sp = Build();

        var task = sp.GetRequiredService<IMediator>().SendAsync(new Unregistered()); // must NOT throw here

        Assert.True(task.IsFaulted);
        await Assert.ThrowsAsync<HandlerNotFoundException>(() => task);
    }

    [Fact]
    public async Task Send_returns_cancelled_task_for_an_already_cancelled_token()
    {
        using var sp = Build();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var task = sp.GetRequiredService<IMediator>().SendAsync(new Ping("x"), cts.Token);

        Assert.True(task.IsCanceled);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public async Task Publish_returns_faulted_task_when_a_handler_throws_synchronously()
    {
        using var sp = Build(); // default sequential publisher

        var task = sp.GetRequiredService<IMediator>().PublishAsync(new Explodes()); // must NOT throw here

        Assert.True(task.IsFaulted);
        await Assert.ThrowsAsync<InvalidOperationException>(() => task);
    }

    [Fact]
    public async Task Publish_returns_cancelled_task_for_an_already_cancelled_token()
    {
        using var sp = Build();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var task = sp.GetRequiredService<IMediator>().PublishAsync(new Happened("e"), cts.Token);

        Assert.True(task.IsCanceled);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public void Null_arguments_are_still_rejected_synchronously()
    {
        using var sp = Build();
        var mediator = sp.GetRequiredService<IMediator>();

        Assert.Throws<ArgumentNullException>(() => { _ = mediator.SendAsync<int>(null!); });
        Assert.Throws<ArgumentNullException>(() => { _ = mediator.PublishAsync<Happened>(null!); });
    }

    // ---- Bug 3: a request implementing two IRequest<> interfaces threw InvalidCastException ----

    [Fact]
    public async Task Request_type_implementing_two_IRequest_interfaces_resolves_each_handler()
    {
        using var sp = Build();
        var mediator = sp.GetRequiredService<IMediator>();

        Assert.Equal(42, await mediator.SendAsync<int>(new Dual()));
        Assert.Equal("forty-two", await mediator.SendAsync<string>(new Dual()));
        // and again, now that both wrappers are cached
        Assert.Equal(42, await mediator.SendAsync<int>(new Dual()));
        Assert.Equal("forty-two", await mediator.SendAsync<string>(new Dual()));
    }

    [Fact]
    public async Task Concurrent_first_calls_for_the_same_request_type_all_succeed()
    {
        using var sp = Build();
        var mediator = sp.GetRequiredService<IMediator>();

        var results = await Task.WhenAll(Enumerable.Range(1, 100)
            .Select(i => Task.Run(() => mediator.SendAsync(new CountMe(i)))));

        Assert.Equal(Enumerable.Range(1, 100).Select(i => i * 2), results);
    }
}
