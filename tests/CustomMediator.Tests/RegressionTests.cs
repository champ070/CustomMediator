using Microsoft.Extensions.DependencyInjection;
using static CustomMediator.Tests.TestHost;

namespace CustomMediator.Tests;

/// <summary>One group of tests per bug that was found and fixed, so it cannot silently come back.</summary>
public class RegressionTests
{
    // ---- Bug 1: cancellation was swallowed (empty AggregateException) by TaskWhenAllPublisher ----

    [Fact]
    public async Task WhenAll_publisher_propagates_cancellation_instead_of_an_empty_AggregateException()
    {
        using var sp = Build(o => o.UseNotificationPublisher<TaskWhenAllPublisher>());

        var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sp.GetRequiredService<IMediator>().PublishAsync(new CancelsEverything(), Ct));

        Assert.IsNotType<AggregateException>(ex);
    }

    // ---- Bug 2: synchronous throws escaped SendAsync instead of faulting the returned Task ----

    [Fact]
    public async Task Send_returns_faulted_task_when_handler_throws_synchronously()
    {
        using var sp = Build();

        var task = sp.GetRequiredService<IMediator>().SendAsync(new SyncThrows(), Ct); // must NOT throw here

        Assert.True(task.IsFaulted);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => task);
        Assert.Equal("boom", ex.Message);
    }

    [Fact]
    public async Task Send_returns_faulted_task_when_no_handler_is_registered()
    {
        using var sp = Build();

        var task = sp.GetRequiredService<IMediator>().SendAsync(new Unregistered(), Ct); // must NOT throw here

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

        var task = sp.GetRequiredService<IMediator>().PublishAsync(new Explodes(), Ct); // must NOT throw here

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

        Assert.Throws<ArgumentNullException>(() => { _ = mediator.SendAsync<int>(null!, Ct); });
        Assert.Throws<ArgumentNullException>(() => { _ = mediator.PublishAsync<Happened>(null!, Ct); });
    }

    // ---- Bug 3: a request implementing two IRequest<> interfaces threw InvalidCastException ----

    [Fact]
    public async Task Request_type_implementing_two_IRequest_interfaces_resolves_each_handler()
    {
        using var sp = Build();
        var mediator = sp.GetRequiredService<IMediator>();

        Assert.Equal(42, await mediator.SendAsync<int>(new Dual(), Ct));
        Assert.Equal("forty-two", await mediator.SendAsync<string>(new Dual(), Ct));
        // and again, now that both wrappers are cached
        Assert.Equal(42, await mediator.SendAsync<int>(new Dual(), Ct));
        Assert.Equal("forty-two", await mediator.SendAsync<string>(new Dual(), Ct));
    }

    [Fact]
    public async Task Concurrent_first_calls_for_the_same_request_type_all_succeed()
    {
        using var sp = Build();
        var mediator = sp.GetRequiredService<IMediator>();

        var results = await Task.WhenAll(Enumerable.Range(1, 100)
            .Select(i => Task.Run(() => mediator.SendAsync(new CountMe(i), Ct), Ct)));

        Assert.Equal(Enumerable.Range(1, 100).Select(i => i * 2), results);
    }
}
