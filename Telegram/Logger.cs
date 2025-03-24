namespace Telegram;
using Microsoft.Extensions.Logging;

public class Logger
{
    private static readonly Action<ILogger, Exception?> _logInvalidToken = LoggerMessage.Define(
        LogLevel.Error,
        new EventId(1, "InvalidToken"),
        "Invalid Telegram Api token");

    private static readonly Action<ILogger, Exception?> _logException = LoggerMessage.Define(
        LogLevel.Error,
        new EventId(2, "Exception"),
        "An error occurred while executing the bot");

    public static void LogInvalidToken(ILogger logger) => _logInvalidToken(logger, null);
    public static void LogException(ILogger logger, Exception ex) => _logException(logger, ex);
}
