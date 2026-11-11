using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CustomMediator.Tests;

public record Ping(string Message) : IRequest<string>;
public record Unregistered : IRequest<int>;
public record DoWork : IRequest<Unit>;
public record Happened(string Name) : INotification;
public record Explodes : INotification;

public class PingHandler : IRequestHandler<Ping, string>
{
    public Task<string> HandleAsync(Ping request, CancellationToken ct) => Task.FromResult($"Pong: {request.Message}");
}

public class DoWorkHandler(List<string> log) : IRequestHandler<DoWork, Unit>
{
    public Task<Unit> HandleAsync(DoWork request, CancellationToken ct)
    {
        log.Add("work");
        return Unit.Task;
    }
}

public class HappenedA(List<string> log) : INotificationHandler<Happened>
{
    public Task HandleAsync(Happened n, CancellationToken ct) { log.Add("A:" + n.Name); return Task.CompletedTask; }
}

public class HappenedB(List<string> log) : INotificationHandler<Happened>
{
    public Task HandleAsync(Happened n, CancellationToken ct) { log.Add("B:" + n.Name); return Task.CompletedTask; }
}

public class FailingHandler1 : INotificationHandler<Explodes>
{
    public Task HandleAsync(Explodes n, CancellationToken ct) => throw new InvalidOperationException("one");
}

public class FailingHandler2 : INotificationHandler<Explodes>
{
    public Task HandleAsync(Explodes n, CancellationToken ct) => throw new InvalidOperationException("two");
}

public class PingValidator : IValidator<Ping>
{
    public IEnumerable<string> Validate(Ping r) => string.IsNullOrWhiteSpace(r.Message) ? ["Message is required"] : [];
}

public class OuterBehavior<TRequest, TResponse>(List<string> log) : TaggingBehaviorBase<TRequest, TResponse>(log, "outer")
    where TRequest : IRequest<TResponse>;

public class InnerBehavior<TRequest, TResponse>(List<string> log) : TaggingBehaviorBase<TRequest, TResponse>(log, "inner")
    where TRequest : IRequest<TResponse>;

public abstract class TaggingBehaviorBase<TRequest, TResponse>(List<string> log, string tag)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        log.Add("before:" + tag);
        var result = await next();
        log.Add("after:" + tag);
        return result;
    }
}

public class ShortCircuitBehavior : IPipelineBehavior<Ping, string>
{
    public Task<string> HandleAsync(Ping request, RequestHandlerDelegate<string> next, CancellationToken ct)
        => Task.FromResult("cached");
}

public class MediatorTests
{
    private static ServiceProvider Build(Action<IServiceCollection>? extra = null, Action<MediatorOptions>? options = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new List<string>());
        services.AddCustomMediator(o =>
        {
            o.RegisterServicesFromAssembly(typeof(MediatorTests).Assembly);
            options?.Invoke(o);
        });
        extra?.Invoke(services);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Send_returns_handler_result()
    {
        using var sp = Build();
        var result = await sp.GetRequiredService<IMediator>().SendAsync(new Ping("hi"), TestContext.Current.CancellationToken);
        Assert.Equal("Pong: hi", result);
    }

    [Fact]
    public async Task Send_without_handler_throws_HandlerNotFoundException()
    {
        using var sp = Build();
        var ex = await Assert.ThrowsAsync<HandlerNotFoundException>(() => sp.GetRequiredService<IMediator>().SendAsync(new Unregistered(), TestContext.Current.CancellationToken));
        Assert.Equal(typeof(Unregistered), ex.RequestType);
    }

    [Fact]
    public async Task Command_with_Unit_response_runs_handler()
    {
        using var sp = Build();
        var result = await sp.GetRequiredService<IMediator>().SendAsync(new DoWork(), TestContext.Current.CancellationToken);
        Assert.Equal(Unit.Value, result);
        Assert.Contains("work", sp.GetRequiredService<List<string>>());
    }

    [Fact]
    public async Task Send_with_null_request_throws()
    {
        using var sp = Build();
        await Assert.ThrowsAsync<ArgumentNullException>(() => sp.GetRequiredService<IMediator>().SendAsync<string>(null!, TestContext.Current.CancellationToken));
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
        using var sp = Build(options: o => o.AddOpenBehavior(typeof(OuterBehavior<,>)).AddOpenBehavior(typeof(InnerBehavior<,>)));
        await sp.GetRequiredService<IMediator>().SendAsync(new Ping("x"), TestContext.Current.CancellationToken);
        Assert.Equal(["before:outer", "before:inner", "after:inner", "after:outer"], sp.GetRequiredService<List<string>>());
    }

    [Fact]
    public async Task Behavior_can_short_circuit_the_handler()
    {
        using var sp = Build(extra: s => s.AddTransient<IPipelineBehavior<Ping, string>, ShortCircuitBehavior>());
        Assert.Equal("cached", await sp.GetRequiredService<IMediator>().SendAsync(new Ping("x"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Validation_behavior_rejects_invalid_requests()
    {
        using var sp = Build(options: o => o.AddOpenBehavior(typeof(ValidationBehavior<,>)));
        var mediator = sp.GetRequiredService<IMediator>();

        var ex = await Assert.ThrowsAsync<RequestValidationException>(() => mediator.SendAsync(new Ping(" "), TestContext.Current.CancellationToken));
        Assert.Equal(["Message is required"], ex.Errors);
        Assert.Equal("Pong: ok", await mediator.SendAsync(new Ping("ok"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Logging_behavior_passes_result_through_and_rethrows_failures()
    {
        using var sp = Build(
            extra: s => s.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>)),
            options: o => o.AddOpenBehavior(typeof(LoggingBehavior<,>)));
        var mediator = sp.GetRequiredService<IMediator>();

        Assert.Equal("Pong: a", await mediator.SendAsync(new Ping("a"), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<HandlerNotFoundException>(() => mediator.SendAsync(new Unregistered(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Publish_runs_every_handler_sequentially_by_default()
    {
        using var sp = Build();
        await sp.GetRequiredService<IMediator>().PublishAsync(new Happened("e"), TestContext.Current.CancellationToken);
        Assert.Equal(["A:e", "B:e"], sp.GetRequiredService<List<string>>());
    }

    [Fact]
    public async Task Publish_with_no_handlers_does_nothing()
    {
        using var sp = Build();
        await sp.GetRequiredService<IMediator>().PublishAsync(new NoHandlers(), TestContext.Current.CancellationToken);
    }

    private static readonly string[] collection = ["one", "two"];

    [Fact]
    public async Task Sequential_publisher_stops_at_first_failure()
    {
        using var sp = Build();
        // Assembly scan order is not guaranteed, so either failing handler may run first; the second must never run.
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sp.GetRequiredService<IMediator>().PublishAsync(new Explodes(), TestContext.Current.CancellationToken));
        Assert.Contains(ex.Message, collection);
    }

    [Fact]
    public async Task WhenAll_publisher_reports_all_failures()
    {
        using var sp = Build(options: o => o.UseNotificationPublisher<TaskWhenAllPublisher>());

        var ex = await Assert.ThrowsAsync<AggregateException>(() => sp.GetRequiredService<IMediator>().PublishAsync(new Explodes(), TestContext.Current.CancellationToken));
        Assert.Equal(2, ex.InnerExceptions.Count);
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

public record NoHandlers : INotification;
