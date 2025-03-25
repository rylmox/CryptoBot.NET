using CryptoExchange.Net.SharedApis;

namespace Engine.Trades;

public sealed class MarketTrade(string symbol, decimal quantity, decimal price) : SpotTradeBase(symbol, quantity, price, SharedOrderType.Market)
{
}
