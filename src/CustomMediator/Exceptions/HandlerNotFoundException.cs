namespace CustomMediator;

/// <summary>Thrown (through the returned task) when no handler is registered for a request type.</summary>
public sealed class HandlerNotFoundException(Type requestType)
    : InvalidOperationException($"No handler registered for request type '{requestType.Name}'.")
{
    public Type RequestType { get; } = requestType;
}
