using Microsoft.Extensions.DependencyInjection;

namespace CustomMediator;

/// <summary>
/// Lets <see cref="Mediator"/> cache one wrapper per request type and call it knowing only TResponse;
/// the concrete request type is handled inside <see cref="TypedRequestHandlerWrapper{TRequest, TResponse}"/>.
/// </summary>
internal abstract class RequestHandlerWrapper<TResponse>
{
    public abstract Task<TResponse> HandleAsync(IRequest<TResponse> request, IServiceProvider serviceProvider, CancellationToken cancellationToken);
}

/// <summary>Resolves the handler and behaviors for one request type and runs them as a pipeline.</summary>
internal sealed class TypedRequestHandlerWrapper<TRequest, TResponse> : RequestHandlerWrapper<TResponse>
    where TRequest : IRequest<TResponse>
{
    public override Task<TResponse> HandleAsync(IRequest<TResponse> request, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var handler = serviceProvider.GetService<IRequestHandler<TRequest, TResponse>>()
                      ?? throw new HandlerNotFoundException(typeof(TRequest));

        var typedRequest = (TRequest)request;

        // Microsoft DI returns an array here, so the cast avoids an extra copy.
        var behaviors = serviceProvider.GetServices<IPipelineBehavior<TRequest, TResponse>>();
        var list = behaviors as IReadOnlyList<IPipelineBehavior<TRequest, TResponse>> ?? [.. behaviors];

        // Fast path: no behaviors registered, so call the handler directly (no closures, no delegates).
        if (list.Count == 0) return handler.HandleAsync(typedRequest, cancellationToken);

        RequestHandlerDelegate<TResponse> next = () => handler.HandleAsync(typedRequest, cancellationToken);

        // Build the chain inside-out so the first registered behavior ends up outermost.
        for (var i = list.Count - 1; i >= 0; i--)
        {
            var behavior = list[i];
            var inner = next;
            next = () => behavior.HandleAsync(typedRequest, inner, cancellationToken);
        }

        return next();
    }
}
