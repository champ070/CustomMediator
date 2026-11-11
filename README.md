# CustomMediator

A small, dependency-light implementation of the **mediator pattern** for .NET 8: requests with a single handler, notifications with many handlers, and a pipeline of behaviors for cross-cutting concerns.

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
    public Task<Guid> HandleAsync(CreateOrder request, CancellationToken ct) => ...;
}

var id = await sender.SendAsync(new CreateOrder("Acme", 42));
```

## How it works

```
SendAsync(request)
  -> cached RequestHandlerWrapper<TRequest,TResponse>   (reflection runs once per request type)
       -> Behavior 1 -> Behavior 2 -> ... -> Handler
```

- `Mediator` keeps a static `ConcurrentDictionary<Type, wrapper>`. Creating the wrapper uses `MakeGenericType` once per request type; after that every call is a normal typed call, with no per-call reflection `Invoke`.
- The wrapper resolves the handler and behaviors from DI and folds the behaviors into a chain of `RequestHandlerDelegate<TResponse>` closures, inside-out, so registration order equals execution order.

## Repository layout

```
src/CustomMediator/              the library
tests/CustomMediator.Tests/      xUnit tests (15)
samples/Demo.Api/                minimal API: command, query, notification, validation, logging
benchmarks/CustomMediator.Benchmarks/   BenchmarkDotNet: direct call vs mediator vs mediator + behaviors
```

```bash
dotnet test
dotnet run --project samples/Demo.Api
dotnet run -c Release --project benchmarks/CustomMediator.Benchmarks
```

## Differences from MediatR (deliberately out of scope)

| | CustomMediator | MediatR |
|---|---|---|
| Requests, notifications, pipeline behaviors | yes | yes |
| Streaming requests (`IAsyncEnumerable`) | no | yes |
| Pre/post processors, exception handlers | no (use behaviors) | yes |
| Non-generic `Send(object)` | no | yes |
| Other DI containers | Microsoft DI only | many |
| License | MIT (this repo) | commercial license for recent versions |

## License

[MIT](LICENSE) © 2026 Jatin Bansal

## Known limitations

- Handlers and validators are registered as **transient**; behaviors are open-generic only.
- Open-generic handlers are not scanned.
- Void commands must return `Unit`.
- No benchmark results are published here yet. Run the benchmarks on your own machine for numbers.
