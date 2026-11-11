using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using static CustomMediator.Tests.TestHost;

namespace CustomMediator.Tests;

public class MediatorTests
{
    [Fact]
    public async Task Send_returns_handler_result()
    {
        using var sp = Build();
        var result = await sp.GetRequiredService<IMediator>().SendAsync(new Ping("hi"), Ct);
        Assert.Equal("Pong: hi", result);
    }

    [Fact]
    public async Task Send_without_handler_throws_HandlerNotFoundException()
    {
        using var sp = Build();
        var ex = await Assert.ThrowsAsync<HandlerNotFoundException>(() => sp.GetRequiredService<IMediator>().SendAsync(new Unregistered(), Ct));
        Assert.Equal(typeof(Unregistered), ex.RequestType);
    }

    [Fact]
    public async Task Command_with_Unit_response_runs_handler()
    {
        using var sp = Build();
        var result = await sp.GetRequiredService<IMediator>().SendAsync(new DoWork(), Ct);
        Assert.Equal(Unit.Value, result);
        Assert.Contains("work", sp.GetRequiredService<List<string>>());
    }

    [Fact]
    public async Task Send_with_null_request_throws()
    {
        using var sp = Build();
        await Assert.ThrowsAsync<ArgumentNullException>(() => sp.GetRequiredService<IMediator>().SendAsync<string>(null!, Ct));
    }

    [Fact]
    public async Task Send_with_cancelled_token_throws()
    {
        using var sp = Build();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sp.GetRequiredService<IMediator>().SendAsync(new Ping("x"), cts.Token));
    }

    [Fact]
    public async Task Behaviors_run_in_registration_order_outermost_first()
    {
        using var sp = Build(o => o.AddOpenBehavior(typeof(OuterBehavior<,>)).AddOpenBehavior(typeof(InnerBehavior<,>)));
        await sp.GetRequiredService<IMediator>().SendAsync(new Ping("x"), Ct);
        Assert.Equal(["before:outer", "before:inner", "after:inner", "after:outer"], sp.GetRequiredService<List<string>>());
    }

    [Fact]
    public async Task Behavior_can_short_circuit_the_handler()
    {
        using var sp = Build(extra: s => s.AddTransient<IPipelineBehavior<Ping, string>, ShortCircuitBehavior>());
        Assert.Equal("cached", await sp.GetRequiredService<IMediator>().SendAsync(new Ping("x"), Ct));
    }

    [Fact]
    public async Task Validation_behavior_rejects_invalid_requests()
    {
        using var sp = Build(o => o.AddOpenBehavior(typeof(ValidationBehavior<,>)));
        var mediator = sp.GetRequiredService<IMediator>();

        var ex = await Assert.ThrowsAsync<RequestValidationException>(() => mediator.SendAsync(new Ping(" "), Ct));
        Assert.Equal(["Message is required"], ex.Errors);
        Assert.Equal("Pong: ok", await mediator.SendAsync(new Ping("ok"), Ct));
    }

    [Fact]
    public async Task Logging_behavior_passes_result_through_and_rethrows_failures()
    {
        using var sp = Build(
            o => o.AddOpenBehavior(typeof(LoggingBehavior<,>)),
            s => s.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>)));
        var mediator = sp.GetRequiredService<IMediator>();

        Assert.Equal("Pong: a", await mediator.SendAsync(new Ping("a"), Ct));
        await Assert.ThrowsAsync<HandlerNotFoundException>(() => mediator.SendAsync(new Unregistered(), Ct));
    }

    [Fact]
    public async Task Publish_runs_every_handler_sequentially_by_default()
    {
        using var sp = Build();
        await sp.GetRequiredService<IMediator>().PublishAsync(new Happened("e"), Ct);
        Assert.Equal(["A:e", "B:e"], sp.GetRequiredService<List<string>>());
    }

    [Fact]
    public async Task Publish_with_no_handlers_does_nothing()
    {
        using var sp = Build();
        await sp.GetRequiredService<IMediator>().PublishAsync(new NoHandlers(), Ct);
    }

    [Fact]
    public async Task Sequential_publisher_stops_at_first_failure()
    {
        using var sp = Build();
        var log = sp.GetRequiredService<List<string>>();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sp.GetRequiredService<IMediator>().PublishAsync(new Explodes(), Ct));

        // Assembly scan order is not guaranteed, so either failing handler may run first,
        // but exactly one may run: the first failure must stop the rest.
        var ranHandler = Assert.Single(log);
        Assert.Equal(ranHandler, ex.Message);
    }

    [Fact]
    public async Task WhenAll_publisher_runs_every_handler_and_reports_all_failures()
    {
        using var sp = Build(o => o.UseNotificationPublisher<TaskWhenAllPublisher>());
        var log = sp.GetRequiredService<List<string>>();

        var ex = await Assert.ThrowsAsync<AggregateException>(() => sp.GetRequiredService<IMediator>().PublishAsync(new Explodes(), Ct));

        Assert.Equal(2, ex.InnerExceptions.Count);
        Assert.Equal(["one", "two"], log.Order());
    }

    [Fact]
    public void AddOpenBehavior_rejects_non_behavior_types()
    {
        Assert.Throws<ArgumentException>(() => new MediatorOptions().AddOpenBehavior(typeof(List<>)));
        Assert.Throws<ArgumentException>(() => new MediatorOptions().AddOpenBehavior(typeof(PingHandler)));
    }

    [Fact]
    public void ISender_and_IPublisher_resolve_to_the_mediator()
    {
        using var sp = Build();
        Assert.IsType<Mediator>(sp.GetRequiredService<ISender>());
        Assert.IsType<Mediator>(sp.GetRequiredService<IPublisher>());
    }
}
