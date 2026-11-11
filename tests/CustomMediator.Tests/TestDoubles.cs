namespace CustomMediator.Tests;

// Requests, notifications, handlers and behaviors used by the tests. They are registered by the
// assembly scan in TestHost.Build. Most record what they did into the shared List<string> "log".

// ---- requests and notifications ----
public record Ping(string Message) : IRequest<string>;
public record Unregistered : IRequest<int>;                 // deliberately has no handler
public record DoWork : IRequest<Unit>;
public record SyncThrows : IRequest<int>;
public record Dual : IRequest<int>, IRequest<string>;       // implements IRequest<> twice
public record CountMe(int N) : IRequest<int>;

public record Happened(string Name) : INotification;
public record Explodes : INotification;                     // every handler throws
public record CancelsEverything : INotification;            // every handler returns a cancelled task
public record NoHandlers : INotification;                   // deliberately has no handler

// ---- request handlers ----
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

// ---- notification handlers ----
public class HappenedA(List<string> log) : INotificationHandler<Happened>
{
    public Task HandleAsync(Happened n, CancellationToken ct) { log.Add("A:" + n.Name); return Task.CompletedTask; }
}

public class HappenedB(List<string> log) : INotificationHandler<Happened>
{
    public Task HandleAsync(Happened n, CancellationToken ct) { log.Add("B:" + n.Name); return Task.CompletedTask; }
}

// Both record that they ran, then throw synchronously.
public class FailingHandler1(List<string> log) : INotificationHandler<Explodes>
{
    public Task HandleAsync(Explodes n, CancellationToken ct)
    {
        log.Add("one");
        throw new InvalidOperationException("one");
    }
}

public class FailingHandler2(List<string> log) : INotificationHandler<Explodes>
{
    public Task HandleAsync(Explodes n, CancellationToken ct)
    {
        log.Add("two");
        throw new InvalidOperationException("two");
    }
}

public class CancelledHandler1 : INotificationHandler<CancelsEverything>
{
    public Task HandleAsync(CancelsEverything n, CancellationToken ct) => Task.FromCanceled(new CancellationToken(true));
}

public class CancelledHandler2 : INotificationHandler<CancelsEverything>
{
    public Task HandleAsync(CancelsEverything n, CancellationToken ct) => Task.FromCanceled(new CancellationToken(true));
}

// ---- validators and behaviors ----
public class PingValidator : IValidator<Ping>
{
    public IEnumerable<string> Validate(Ping r) => string.IsNullOrWhiteSpace(r.Message) ? ["Message is required"] : [];
}

/// <summary>Records "before:tag" and "after:tag" around the rest of the pipeline.</summary>
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

public class OuterBehavior<TRequest, TResponse>(List<string> log) : TaggingBehaviorBase<TRequest, TResponse>(log, "outer")
    where TRequest : IRequest<TResponse>;

public class InnerBehavior<TRequest, TResponse>(List<string> log) : TaggingBehaviorBase<TRequest, TResponse>(log, "inner")
    where TRequest : IRequest<TResponse>;

public class ShortCircuitBehavior : IPipelineBehavior<Ping, string>
{
    public Task<string> HandleAsync(Ping request, RequestHandlerDelegate<string> next, CancellationToken ct)
        => Task.FromResult("cached");
}
