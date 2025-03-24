using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Engine.Common;
using CryptoExchange.Net.SharedApis;

namespace Engine;

internal interface IArbitrageSignaldata
{
    public PairPrecisionLimits Precisions { get; }
    public string[] Pairs { get; }
    public decimal Ratio { get; }
    public decimal Spread { get; }
    public SharedOrderSide[] Sides { get; }
    public decimal[] OrderQuantities { get; }
    public Dictionary<string, SharedBookTicker> Prices { get; }
}

internal static class Logger
{
    private static readonly Action<ILogger, string, Exception?> _logArbitrageSignal;
    private static readonly Action<ILogger, string[], decimal, decimal, Exception?> _logArbitrageSignalResult;
    private static readonly Action<ILogger, string, Exception?> _logBalances;
    private static readonly Action<ILogger, string, Exception?> _logPrecisionLimits;
    private static readonly Action<ILogger, string, string, Exception?> _logBookTicker;
    private static readonly Action<ILogger, CryptoExchange.Net.Objects.Error?, Exception?> _logClientError;
    private static readonly Action<ILogger, Exception?> _logException;

    static Logger()
    {
        _logArbitrageSignal = LoggerMessage.Define<string>(
            LogLevel.Debug,
            new EventId(0, "ArbitrageSignal"),
            "Arbitrage signal: {ratio}");

        _logArbitrageSignalResult = LoggerMessage.Define<string[], decimal, decimal>(
            LogLevel.Information,
            new EventId(0, "ArbitrageSignalResult"),
            "Arbitrage signal: {pairs} {ratio} {spread}");

        _logBalances = LoggerMessage.Define<string>(
            LogLevel.Debug,
            new EventId(0, "Balances"),
            "Balances fetched: {balances}");

        _logPrecisionLimits = LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(0, "PrecisionLimits"),
            "Precision limits fetched: {limits}");

        _logBookTicker = LoggerMessage.Define<string, string>(
            LogLevel.Debug,
            new EventId(0, "BookTicker"),
            "New prices for {pair}:\n{ticker}");

        _logClientError = LoggerMessage.Define<CryptoExchange.Net.Objects.Error?>(
            LogLevel.Error,
            new EventId(1, "ClientError"),
            "Client failed: {error}");

        _logException = LoggerMessage.Define(
            LogLevel.Error,
            new EventId(2, "Exception"),
            "An error occurred while executing the bot");
    }

    public static void LogArbitrageSignalResult(this ILogger logger, string[] pairs, decimal spread, decimal ratio) => _logArbitrageSignalResult(logger, pairs, spread, ratio, null);
    public static void LogArbitrageSignal(this ILogger logger, IArbitrageSignaldata data) => _logArbitrageSignal(logger, SerializeToJsonIndented(data), null);
    public static void LogBookTicker(this ILogger logger, string pair, SharedBookTicker ticker) => _logBookTicker(logger, pair, SerializeToJsonIndented(ticker), null);
    public static void LogBalances(this ILogger logger, IEnumerable<SharedBalance> balances) => _logBalances(logger, SerializeToJsonIndented(balances), null);
    public static void LogPrecisionLimits(this ILogger logger, PairPrecisionLimits limits) => _logPrecisionLimits(logger, SerializeToJson(limits), null);
    public static void LogClientError(this ILogger logger, CryptoExchange.Net.Objects.Error? error) => _logClientError(logger, error, null);
    public static void LogException(this ILogger logger, Exception ex) => _logException(logger, ex);

    private static readonly JsonSerializerOptions _jsonSerializer = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, 
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        IncludeFields = true,
    };

    private static readonly JsonSerializerOptions _jsonSerializerIndented = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, 
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // IncludeFields = true,
    };

    private static string SerializeToJson<T>(T obj) => JsonSerializer.Serialize(obj, _jsonSerializer);
    private static string SerializeToJsonIndented<T>(T obj) => JsonSerializer.Serialize(obj, _jsonSerializerIndented);
}
