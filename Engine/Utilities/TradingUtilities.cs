using System.Diagnostics.CodeAnalysis;
using CryptoExchange.Net.SharedApis;

namespace Engine.Utilities;

public static class TradingUtilities
{
    public const string PairSeparator = "/";

    public static SharedOrderSide GetOppositeOrderSide(SharedOrderSide side)
    {
        return side == SharedOrderSide.Buy ? SharedOrderSide.Sell : SharedOrderSide.Buy;
    }

    public static bool SplitSymbol(string pair, [NotNullWhen(true)] out string? @base, [NotNullWhen(true)] out string? quote)
    {
        string[] r = pair.Split(PairSeparator);
        @base = r.FirstOrDefault();
        quote = r.LastOrDefault();

        return @base != null && quote != null;
    }

    public static (string? @base, string? quote) SplitSymbol(this string pair)
    {
        string[] r = pair.Split(PairSeparator);
        return (r.FirstOrDefault(), r.LastOrDefault());
    }

    public static bool IsPriceValid(SharedBookTicker ticker)
    {
        return ticker.BestAskPrice > 0 && ticker.BestAskQuantity > 0 && ticker.BestBidPrice > 0 && ticker.BestBidQuantity > 0;
    }
}