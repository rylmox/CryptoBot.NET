using CryptoExchange.Net.SharedApis;
using Engine.Clients;
using Engine.Utilities;

namespace Engine.Extensions;

public static class CryptoExchangeExtensions
{
    public static SharedSymbol ToSharedSymbol(this string pair, IExchangeClient client)
    {
        TradingUtilities.SplitSymbol(pair, out string? baseAsset, out string? quoteAsset);

        if (baseAsset is null || quoteAsset is null)
        {
            throw new ArgumentException("Invalid trading pair.");
        }

        SharedSymbol symbol = new(tradingMode: TradingMode.Spot, baseAsset: baseAsset, quoteAsset: quoteAsset);
        symbol.SymbolName = symbol.GetSymbol(client.SymbolFormatter);

        return symbol;
    }

    public static string GetSymbol(this SharedSymbol symbol, IExchangeClient client)
    {
        return symbol.GetSymbol(client.SymbolFormatter);
    }
}