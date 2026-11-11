namespace CustomMediator;

/// <summary>Marker for a request that returns <typeparamref name="TResponse"/>.</summary>
public interface IRequest<out TResponse>;

public interface IRequestHandler<in TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    Task<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken);
}

/// <summary>Return type for requests that have no meaningful result (commands).</summary>
public readonly record struct Unit
{
    public static readonly Unit Value = new();

    /// <summary>A completed task holding <see cref="Value"/>, for handlers that return no result.</summary>
    public static readonly Task<Unit> CompletedTask = Task.FromResult(Value);
}
