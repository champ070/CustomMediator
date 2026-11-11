using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CustomMediator;

/// <summary>Logs start, duration and failures of every request.</summary>
public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var name = typeof(TRequest).Name;
        var started = Stopwatch.GetTimestamp();
        LoggingBehaviorLog.Handling(logger, name);

        try
        {
            var response = await next().ConfigureAwait(false);
            LoggingBehaviorLog.Handled(logger, name, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            LoggingBehaviorLog.Failed(logger, ex, name, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            throw;
        }
    }
}

// Source-generated: each method checks IsEnabled for its level and caches the message template,
// so no manual guards or per-call string parsing are needed.
internal static partial class LoggingBehaviorLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Handling {Request}")]
    public static partial void Handling(ILogger logger, string request);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Handled {Request} in {Elapsed} ms")]
    public static partial void Handled(ILogger logger, string request, double elapsed);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "{Request} failed after {Elapsed} ms")]
    public static partial void Failed(ILogger logger, Exception exception, string request, double elapsed);
}
