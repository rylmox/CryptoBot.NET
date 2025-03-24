using CryptoExchange.Net.SharedApis;

namespace Engine.Trades;

public abstract class SpotTradeBase(string symbol, decimal quantity, SharedOrderType type)
{
    public string Symbol { get; init; } = symbol;
    public string OrderClientId { get; init; } = Guid.NewGuid().ToString();
    public decimal Quantity { get; init; } = quantity;
    public SharedOrderType Type { get; init; } = type;
}
