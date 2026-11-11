namespace CustomMediator;

/// <summary>Validates a request before its handler runs. Register any number of validators per request.</summary>
public interface IValidator<in TRequest>
{
    /// <summary>Returns error messages; an empty sequence means the request is valid.</summary>
    IEnumerable<string> Validate(TRequest request);
}

public sealed class RequestValidationException(IReadOnlyList<string> errors)
    : Exception("Request validation failed: " + string.Join("; ", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}

/// <summary>Short-circuits with <see cref="RequestValidationException"/> when any validator reports errors.</summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    public Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        // The list is only created when a validator actually reports an error, so a valid request
        // (or a request with no validators at all) costs no allocation here.
        List<string>? errors = null;
        foreach (var validator in validators)
        {
            foreach (var error in validator.Validate(request))
            {
                (errors ??= []).Add(error);
            }
        }

        if (errors is not null) throw new RequestValidationException(errors);
        return next();
    }
}
