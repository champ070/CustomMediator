using CustomMediator;
using Demo.Api.Infrastructure;
using Demo.Api.Orders;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<OrderStore>();
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
