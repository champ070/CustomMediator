using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using static CustomMediator.Tests.TestHost;

namespace CustomMediator.Tests;

/// <summary>Tests for AddCustomMediator and MediatorOptions: what gets registered, and how bad input is handled.</summary>
public class RegistrationTests
{
    private static void Register(IServiceCollection services, Action<MediatorOptions>? configure = null) =>
        services.AddCustomMediator(options =>
        {
            options.RegisterServicesFromAssembly(typeof(TestHost).Assembly);
            configure?.Invoke(options);
        });

    // ---- registering twice (for example from two modules) must not duplicate anything ----

    [Fact]
    public async Task Calling_AddCustomMediator_twice_runs_each_notification_handler_once()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new List<string>());
        Register(services);
        Register(services);
        using var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<IMediator>().PublishAsync(new Happened("e"), Ct);

        Assert.Equal(["A:e", "B:e"], provider.GetRequiredService<List<string>>());
    }

    [Fact]
    public async Task Calling_AddCustomMediator_twice_runs_each_behavior_once()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new List<string>());
        Register(services, o => o.AddOpenBehavior(typeof(OuterBehavior<,>)));
        Register(services, o => o.AddOpenBehavior(typeof(OuterBehavior<,>)));
        using var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<IMediator>().SendAsync(new Ping("x"), Ct);

        Assert.Equal(["before:outer", "after:outer"], provider.GetRequiredService<List<string>>());
    }

    [Fact]
    public async Task Requests_still_resolve_after_registering_twice()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new List<string>());
        Register(services);
        Register(services);
        using var provider = services.BuildServiceProvider();

        Assert.Equal("Pong: hi", await provider.GetRequiredService<IMediator>().SendAsync(new Ping("hi"), Ct));
    }

    // ---- an assembly that only partly loads must not break registration ----

    [Fact]
    public async Task Assembly_that_only_partly_loads_still_registers_the_types_that_did_load()
    {
        var services = new ServiceCollection();
        services.AddCustomMediator(o => o.RegisterServicesFromAssembly(new PartiallyLoadableAssembly()));
        using var provider = services.BuildServiceProvider();

        Assert.Equal("Pong: ok", await provider.GetRequiredService<IMediator>().SendAsync(new Ping("ok"), Ct));
    }

    /// <summary>Simulates an assembly whose dependencies are missing: GetTypes() throws, but some types did load.</summary>
    private sealed class PartiallyLoadableAssembly : Assembly
    {
        public override Type[] GetTypes() => throw new ReflectionTypeLoadException(
            [typeof(PingHandler), null],
            [new FileNotFoundException("a dependency of the assembly is missing")]);
    }

    // ---- public entry points reject null with a clear exception ----

    [Fact]
    public void AddCustomMediator_rejects_null_services()
    {
        IServiceCollection services = null!;
        Assert.Throws<ArgumentNullException>(() => services.AddCustomMediator(_ => { }));
    }

    [Fact]
    public void AddCustomMediator_rejects_null_configure()
    {
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddCustomMediator(null!));
    }

    [Fact]
    public void RegisterServicesFromAssembly_rejects_null()
    {
        Assert.Throws<ArgumentNullException>(() => new MediatorOptions().RegisterServicesFromAssembly(null!));
    }

    [Fact]
    public void AddOpenBehavior_rejects_null()
    {
        Assert.Throws<ArgumentNullException>(() => new MediatorOptions().AddOpenBehavior(null!));
    }

    [Fact]
    public void AddOpenBehavior_rejects_types_that_are_not_open_generic_behaviors()
    {
        Assert.Throws<ArgumentException>(() => new MediatorOptions().AddOpenBehavior(typeof(List<>)));
        Assert.Throws<ArgumentException>(() => new MediatorOptions().AddOpenBehavior(typeof(PingHandler)));
    }

    [Fact]
    public async Task ForeachAwaitPublisher_rejects_null_arguments()
    {
        await AssertPublisherRejectsNulls(new ForeachAwaitPublisher());
    }

    [Fact]
    public async Task TaskWhenAllPublisher_rejects_null_arguments()
    {
        await AssertPublisherRejectsNulls(new TaskWhenAllPublisher());
    }

    private static async Task AssertPublisherRejectsNulls(INotificationPublisher publisher)
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => publisher.PublishAsync<Happened>(null!, new Happened("e"), Ct));
        await Assert.ThrowsAsync<ArgumentNullException>(() => publisher.PublishAsync<Happened>([], null!, Ct));
    }
}
