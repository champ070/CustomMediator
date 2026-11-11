using Microsoft.Extensions.DependencyInjection;
using static CustomMediator.Tests.TestHost;

namespace CustomMediator.Tests;

public class ValidationBehaviorTests
{
    private static ServiceProvider BuildWithValidation() => Build(o => o.AddOpenBehavior(typeof(ValidationBehavior<,>)));

    [Fact]
    public async Task Errors_from_every_validator_of_a_request_are_reported_together()
    {
        using var provider = BuildWithValidation();

        var ex = await Assert.ThrowsAsync<RequestValidationException>(
            () => provider.GetRequiredService<IMediator>().SendAsync(new Signup("", "not-an-email"), Ct));

        // Validators are discovered by assembly scan, so their order is not guaranteed.
        Assert.Equal(["Email must contain '@'.", "Name is required."], ex.Errors.Order());
    }

    [Fact]
    public async Task A_valid_request_reaches_the_handler()
    {
        using var provider = BuildWithValidation();

        var result = await provider.GetRequiredService<IMediator>().SendAsync(new Signup("Ann", "ann@example.com"), Ct);

        Assert.Equal("welcome Ann", result);
    }

    [Fact]
    public async Task A_request_without_any_validator_passes_through()
    {
        using var provider = BuildWithValidation();

        var result = await provider.GetRequiredService<IMediator>().SendAsync(new CountMe(2), Ct);

        Assert.Equal(4, result);
    }

    [Fact]
    public async Task A_rejected_request_never_reaches_the_handler()
    {
        using var provider = BuildWithValidation();
        var log = provider.GetRequiredService<List<string>>();

        await Assert.ThrowsAsync<RequestValidationException>(
            () => provider.GetRequiredService<IMediator>().SendAsync(new Signup("", ""), Ct));

        Assert.DoesNotContain("signup-handled", log);
    }
}
