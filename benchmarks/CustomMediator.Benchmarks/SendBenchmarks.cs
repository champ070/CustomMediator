using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;

namespace CustomMediator.Benchmarks;

public record Ping(int Value) : IRequest<int>;

public class PingHandler : IRequestHandler<Ping, int>
{
    // NoInlining stops the JIT from erasing the handler, otherwise the "direct call" baseline measures nothing.
    [MethodImpl(MethodImplOptions.NoInlining)]
    public Task<int> HandleAsync(Ping request, CancellationToken ct) => Task.FromResult(request.Value + 1);
}

public class PassThroughBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    public Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct) => next();
}

[MemoryDiagnoser]
public class SendBenchmarks
{
    private IMediator _plain = null!;
    private IMediator _withBehaviors = null!;
    private PingHandler _direct = null!;
    private readonly Ping _request = new(1);

    [GlobalSetup]
    public void Setup()
    {
        _plain = Build(_ => { });
        _withBehaviors = Build(o => o.AddOpenBehavior(typeof(PassThroughBehavior<,>)).AddOpenBehavior(typeof(PassThroughBehavior<,>)));
        _direct = new PingHandler();

        static IMediator Build(Action<MediatorOptions> configure)
        {
            var services = new ServiceCollection();
            services.AddCustomMediator(o =>
            {
                o.RegisterServicesFromAssembly(typeof(PingHandler).Assembly);
                configure(o);
            });
            return services.BuildServiceProvider().GetRequiredService<IMediator>();
        }
    }

    [Benchmark(Baseline = true)]
    public Task<int> DirectHandlerCall() => _direct.HandleAsync(_request, default);

    [Benchmark]
    public Task<int> MediatorSend() => _plain.SendAsync(_request);

    [Benchmark]
    public Task<int> MediatorSendTwoBehaviors() => _withBehaviors.SendAsync(_request);
}
