using CryptoExchange.Net.SharedApis;

namespace Engine.Trades;

public sealed class LimitTrade(string symbol, decimal quantity, decimal price) : SpotTradeBase(symbol, quantity, price, SharedOrderType.Limit)
{
}
