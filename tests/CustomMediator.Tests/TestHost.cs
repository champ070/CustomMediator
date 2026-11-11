using Microsoft.Extensions.DependencyInjection;

namespace CustomMediator.Tests;

/// <summary>Shared test setup, so every test class builds its container the same way.</summary>
public static class TestHost
{
    /// <summary>The token xunit cancels when a test run is aborted; pass it to every awaited call.</summary>
    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    /// Builds a container with the mediator, every test double in this assembly (found by scanning),
    /// and a shared <c>List&lt;string&gt;</c> that test doubles use to record what happened.
    /// </summary>
    public static ServiceProvider Build(Action<MediatorOptions>? options = null, Action<IServiceCollection>? extra = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new List<string>());
        services.AddCustomMediator(o =>
        {
            o.RegisterServicesFromAssembly(typeof(TestHost).Assembly);
            options?.Invoke(o);
        });
        extra?.Invoke(services);
        return services.BuildServiceProvider();
    }
}
