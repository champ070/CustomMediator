namespace CustomMediator;

/// <summary>The next step in the pipeline: either another behavior or the handler itself.</summary>
public delegate Task<TResponse> RequestHandlerDelegate<TResponse>();

/// <summary>
/// Wraps a request handler (cross-cutting concerns: validation, logging, transactions...).
/// Behaviors run in registration order; the first registered is the outermost.
/// </summary>
public interface IPipelineBehavior<in TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}
