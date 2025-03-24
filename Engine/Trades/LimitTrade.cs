using CryptoExchange.Net.SharedApis;

namespace Engine.Trades;

public sealed class LimitTrade(string symbol, decimal quantity) : SpotTradeBase(symbol, quantity, SharedOrderType.Limit)
{
}
