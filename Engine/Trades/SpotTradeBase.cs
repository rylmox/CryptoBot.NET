using CryptoExchange.Net.SharedApis;

namespace Engine.Trades;

public abstract class SpotTradeBase(string symbol, decimal quantity, decimal price, SharedOrderType type)
{
    public string Symbol { get; init; } = symbol;
    public string OrderClientId { get; init; } = Guid.NewGuid().ToString();
    public decimal Price { get; init; } = price;
    public decimal Quantity { get; init; } = quantity;
    public SharedOrderType Type { get; init; } = type;
    public SharedOrderSide Side { get; init; } = SharedOrderSide.Buy;
    public SharedTimeInForce TimeInForce { get; init; } = SharedTimeInForce.GoodTillCanceled;
}
