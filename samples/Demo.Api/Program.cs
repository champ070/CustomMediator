using CustomMediator;
using Demo.Api.Infrastructure;
using Demo.Api.Orders;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();
builder.Services.AddCustomMediator(o => o
    .RegisterServicesFromAssembly(typeof(Program).Assembly)
    .AddOpenBehavior(typeof(LoggingBehavior<,>))      // outermost: times everything below it
    .AddOpenBehavior(typeof(ValidationBehavior<,>))); // runs before the handler

builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddProblemDetails(); // fallback response for exceptions no handler claims (500)

var app = builder.Build();

app.UseExceptionHandler();
app.MapOrderEndpoints();

app.Run();

// Makes the top-level Program class visible to WebApplicationFactory<Program> in the integration tests.
public partial class Program;
