namespace NodeAgent.Utils;

public static class LoggerExtensions
{
    public static void LogError(this ILogger logger, Exception ex, int jobId, int? taskId, int? requeue, string fmt, params object?[] args)
    {
        if (logger.IsEnabled(LogLevel.Error))
        {
            var msg = $"Job '{jobId}', Task '{taskId}.{requeue}': {fmt}";
            logger.LogError(ex, msg, args);
        }
    }

    public static void LogError(this ILogger logger, int jobId, int? taskId, int? requeue, string fmt, params object?[] args)
    {
        if (logger.IsEnabled(LogLevel.Error))
        {
            var msg = $"Job '{jobId}', Task '{taskId}.{requeue}': {fmt}";
            logger.LogError(msg, args);
        }
    }

    public static void LogWarning(this ILogger logger, Exception ex, int jobId, int? taskId, int? requeue, string fmt, params object?[] args)
    {
        if (logger.IsEnabled(LogLevel.Warning))
        {
            var msg = $"Job '{jobId}', Task '{taskId}.{requeue}': {fmt}";
            logger.LogWarning(ex, msg, args);
        }
    }

    public static void LogWarning(this ILogger logger, int jobId, int? taskId, int? requeue, string fmt, params object?[] args)
    {
        if (logger.IsEnabled(LogLevel.Warning))
        {
            var msg = $"Job '{jobId}', Task '{taskId}.{requeue}': {fmt}";
            logger.LogWarning(msg, args);
        }
    }

    public static void LogInformation(this ILogger logger, Exception ex, int jobId, int? taskId, int? requeue, string fmt, params object?[] args)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            var msg = $"Job '{jobId}', Task '{taskId}.{requeue}': {fmt}";
            logger.LogInformation(ex, msg, args);
        }
    }

    public static void LogInformation(this ILogger logger, int jobId, int? taskId, int? requeue, string fmt, params object?[] args)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            var msg = $"Job '{jobId}', Task '{taskId}.{requeue}': {fmt}";
            logger.LogInformation(msg, args);
        }
    }

    public static void LogDebug(this ILogger logger, Exception ex, int jobId, int? taskId, int? requeue, string fmt, params object?[] args)
    {
        if (logger.IsEnabled(LogLevel.Debug))
        {
            var msg = $"Job '{jobId}', Task '{taskId}.{requeue}': {fmt}";
            logger.LogDebug(ex, msg, args);
        }
    }

    public static void LogDebug(this ILogger logger, int jobId, int? taskId, int? requeue, string fmt, params object?[] args)
    {
        if (logger.IsEnabled(LogLevel.Debug))
        {
            var msg = $"Job '{jobId}', Task '{taskId}.{requeue}': {fmt}";
            logger.LogDebug(msg, args);
        }
    }
}
