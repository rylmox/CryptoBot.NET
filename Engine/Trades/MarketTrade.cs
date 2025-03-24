using CryptoExchange.Net.SharedApis;

namespace Engine.Trades;

public sealed class MarketTrade(string symbol, decimal quantity) : SpotTradeBase(symbol, quantity, SharedOrderType.Market)
{
}
