using System.Collections.Concurrent;
using CryptoExchange.Net.SharedApis;
using System.Threading.Channels;
using Engine.Clients;
using Engine.Trades;
using System.Runtime.CompilerServices;

namespace Engine.Orders;

public sealed class SpotOrderManager(IExchangeClient client) : ISpotOrderManager, IDisposable
{
    public bool CancelOrdersOnDispose { get; set; } = true;

    private readonly ConcurrentDictionary<string, Channel<SharedSpotOrder>> _channels = new();
    private readonly ConcurrentDictionary<string, SharedSpotOrder> _orders = [];

    public async Task<bool> Initialize(CancellationToken ct)
    {
        return await client.SubscribeToOrderUpdates(async order => await OnOrderUpdates(order), ct);
    }

    private async Task OnOrderUpdates(SharedSpotOrder order)
    {
        if (!_channels.TryGetValue(order.OrderId, out Channel<SharedSpotOrder>? channel))
        {
            return;
        }

        await channel.Writer.WriteAsync(order);

        if (order.Status == SharedOrderStatus.Filled || order.Status == SharedOrderStatus.Canceled)
        {
            channel.Writer.TryComplete();
        }
    }

    public void Dispose()
    {
        if (CancelOrdersOnDispose)
        {
            // TODO 
            CancelAllOrders(CancellationToken.None);
        }

        _orders.Clear();
        GC.SuppressFinalize(this);
    }

    public void CancelAllOrders(CancellationToken ct)
    {
        foreach (SharedSpotOrder order in _orders.Values)
        {
            // TODO cancel order
        }
    }

    public async IAsyncEnumerable<SharedSpotOrder> PlaceOrderAsync(SpotTradeBase trade, [EnumeratorCancellation] CancellationToken ct)
    {
        if (string.IsNullOrEmpty(trade.OrderClientId))
        {
            yield break;
        }

        if (await client.PlaceOrder(trade, ct) is not SharedSpotOrder order)
        {
            yield break;
        }

        Channel<SharedSpotOrder> channel = Channel.CreateUnbounded<SharedSpotOrder>();

        _orders.TryAdd(trade.OrderClientId, order);
        _channels.TryAdd(trade.OrderClientId, channel);

        await foreach (SharedSpotOrder orderUpdate in channel.Reader.ReadAllAsync(ct))
        {
            yield return orderUpdate;
        }
    }

    public void CancelOrder(string clientOrderId, CancellationToken ct)
    {
        if (_orders.TryGetValue(clientOrderId, out SharedSpotOrder? order))
        {
            // TODO cancel order
        }
    }
}
