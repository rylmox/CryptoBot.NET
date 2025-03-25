using CryptoExchange.Net.SharedApis;

namespace Engine.Trades;

public sealed class StopLossTrade(string symbol, decimal quantity, decimal price, decimal stopPrice) : SpotTradeBase(symbol, quantity, price, SharedOrderType.Other)
{
    public decimal StopPrice { get; init; } = stopPrice;
}
