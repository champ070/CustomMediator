# CustomMediator

A small, dependency-light implementation of the **mediator pattern** for .NET 10: requests with a single handler, notifications with many handlers, and a pipeline of behaviors for cross-cutting concerns.

It is a learning-oriented, lightweight library written to understand what mediator libraries such as MediatR do under the hood. It is **not** a drop-in replacement for MediatR and does not try to cover all of its features.

## Features

- **Requests / commands / queries** via `ISender.SendAsync` (`IRequest<TResponse>`; use `Unit` for commands with no result)
- **Notifications** via `IPublisher.PublishAsync`, zero or many handlers, with a pluggable strategy:
  - `ForeachAwaitPublisher` (default): sequential, stops at the first failure
  - `TaskWhenAllPublisher`: concurrent, reports *all* failures in one `AggregateException`
- **Pipeline behaviors** (`IPipelineBehavior<,>`), registered as open generics, first registered = outermost
- Built-in `ValidationBehavior` (with `IValidator<T>`) and `LoggingBehavior`
- **Assembly scanning** registration: one call instead of wiring every handler by hand
- Clear `HandlerNotFoundException` instead of a null reference

## Quick start

```csharp
builder.Services.AddCustomMediator(o => o
    .RegisterServicesFromAssembly(typeof(Program).Assembly)
    .AddOpenBehavior(typeof(LoggingBehavior<,>))
    .AddOpenBehavior(typeof(ValidationBehavior<,>)));

public record CreateOrder(string Customer, decimal Total) : IRequest<Guid>;

public class CreateOrderHandler : IRequestHandler<CreateOrder, Guid>
{
    public Task<Guid> HandleAsync(CreateOrder request, CancellationToken cancellationToken) => ...;
}

var id = await sender.SendAsync(new CreateOrder("Acme", 42));
```

## How it works

```
SendAsync(request)
  -> cached TypedRequestHandlerWrapper<TRequest,TResponse>   (reflection runs once per request/response pair)
       -> Behavior 1 -> Behavior 2 -> ... -> Handler
```

- `Mediator` keeps a static `ConcurrentDictionary` keyed by (request type, response type). Creating a `TypedRequestHandlerWrapper` uses `MakeGenericType` once per pair; after that every call is a normal typed call, with no per-call reflection `Invoke`. The response type is part of the key because one request type may implement `IRequest<>` more than once.
- The wrapper resolves the handler and behaviors from DI. With no behaviors registered it calls the handler directly; otherwise it folds the behaviors into a chain of `RequestHandlerDelegate<TResponse>` closures, inside-out, so registration order equals execution order.

## Repository layout

```
src/CustomMediator/              the library (single namespace: CustomMediator)
  Abstractions/                    contracts: requests, notifications, pipeline behaviors, ISender/IPublisher/IMediator
  Pipeline/                        built-in behaviors and the internal request wrappers that run the pipeline
  Notifications/                   publisher strategies (sequential, concurrent)
  DependencyInjection/             AddCustomMediator and MediatorOptions
  Exceptions/                      HandlerNotFoundException
  Mediator.cs                      the IMediator implementation
tests/CustomMediator.Tests/      xUnit v3 tests (33): unit tests plus end-to-end tests of Demo.Api
samples/Demo.Api/                minimal API: command, query, notification, validation, logging
  Orders/                          request/validator/handler per use case; event and its handlers; repository
  Infrastructure/                  exception handler that maps validation errors to HTTP 400
benchmarks/CustomMediator.Benchmarks/   BenchmarkDotNet: direct call vs mediator vs mediator + behaviors
```

```bash
dotnet test
dotnet run --project samples/Demo.Api
dotnet run -c Release --project benchmarks/CustomMediator.Benchmarks
```

## Try the demo

Start the sample API:

```bash
dotnet run --project samples/Demo.Api --urls http://localhost:5199
```

Create an order (command, then an `OrderCreated` notification handled by two handlers):

```bash
curl -i -X POST http://localhost:5199/orders -H "Content-Type: application/json" -d '{"customer":"Acme","total":42}'
```

```
HTTP/1.1 201 Created
Location: /orders/3f2b...
{"id":"3f2b..."}
```

Read it back (query):

```bash
curl http://localhost:5199/orders/<id-from-above>
```

Send an invalid order to see the validation behavior short-circuit the pipeline:

```bash
curl -i -X POST http://localhost:5199/orders -H "Content-Type: application/json" -d '{"customer":"","total":0}'
```

```
HTTP/1.1 400 Bad Request
{"errors":["Customer is required.","Total must be greater than zero."]}
```

The server console shows each request being logged and timed by `LoggingBehavior`. The same scenarios run as automated end-to-end tests in `tests/CustomMediator.Tests/DemoApiTests.cs`.

## Differences from MediatR (deliberately out of scope)

| | CustomMediator | MediatR |
|---|---|---|
| Requests, notifications, pipeline behaviors | yes | yes |
| Streaming requests (`IAsyncEnumerable`) | no | yes |
| Pre/post processors, exception handlers | no (use behaviors) | yes |
| Non-generic `Send(object)` | no | yes |
| Other DI containers | Microsoft DI only | many |
| License | MIT (this repo) | commercial license for recent versions |

## Benchmarks

BenchmarkDotNet 0.15.8, .NET 10.0.11, Release build, Windows 11, Intel Core i5-13500H (12 cores / 16 threads), x64. The handler is trivial on purpose, so these numbers show **mediator overhead only**, not real-world request cost.

| Method                    | Mean     | Allocated |
|-------------------------- |---------:|----------:|
| Direct handler call       |  ~2.0 ns |       0 B |
| `SendAsync`, no behaviors |  ~42-45 ns |      64 B |
| `SendAsync`, 2 behaviors  |  ~78-85 ns |     424 B |

- A call costs well under **100 ns**. Any handler that does I/O (database, HTTP) takes milliseconds, so the overhead is negligible next to it.
- Each pipeline behavior adds roughly 18 ns and about 180 B.
- Timings vary between runs and machines; allocation figures are stable. Re-run with
  `dotnet run -c Release --project benchmarks/CustomMediator.Benchmarks` to measure your own hardware.
- When no behaviors are registered, the mediator calls the handler directly. On .NET 8 that fast path cut allocations from 176 B to 64 B per call and time from about 95 ns to about 67-77 ns (two runs each).
- Moving the same code from .NET 8 to .NET 10 (no code changes) reduced time further, from roughly 67-77 ns to about 42-45 ns (no behaviors) and from roughly 131-141 ns to about 78-85 ns (two behaviors). Allocations were identical, so the gain comes from the runtime. The .NET 8 and .NET 10 runs used different BenchmarkDotNet versions.
- The "direct call" baseline uses a non-inlined handler; with inlining the JIT erases the call and the comparison is meaningless.

## License

[MIT](LICENSE) © 2026 Jatin Bansal

## Known limitations

- Handlers and validators are registered as **transient**; behaviors are open-generic only.
- Open-generic handlers are not scanned.
- Void commands must return `Unit`.
- No benchmark results are published here yet. Run the benchmarks on your own machine for numbers.
