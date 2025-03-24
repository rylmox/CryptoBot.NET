using CryptoExchange.Net.SharedApis;

namespace Engine.Trades;

public sealed class StopLossTrade(string symbol, decimal quantity, decimal stopPrice) : SpotTradeBase(symbol, quantity, SharedOrderType.Other)
{
    public decimal StopPrice { get; init; } = stopPrice;
}
