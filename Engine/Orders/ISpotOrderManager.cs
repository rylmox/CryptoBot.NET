using CryptoExchange.Net.SharedApis;
using Engine.Trades;

namespace Engine.Orders;

public interface ISpotOrderManager
{
    Task<bool> Initialize(CancellationToken ct);
    IAsyncEnumerable<SharedSpotOrder> PlaceOrderAsync(SpotTradeBase trade, CancellationToken ct);
    void CancelAllOrders(CancellationToken ct);
    void CancelOrder(string clientOrderId, CancellationToken ct);
}