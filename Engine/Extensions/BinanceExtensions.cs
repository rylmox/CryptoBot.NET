using Binance.Net.Enums;
using Binance.Net.Objects.Models.Spot;
using Binance.Net.Objects.Models.Spot.Socket;
using CryptoExchange.Net.SharedApis;

namespace Engine.Extensions;

public static class BinanceExtensions
{
    public static SharedSymbol ToSharedSymbol(this BinanceSymbol symbol)
    {
        return new(tradingMode: TradingMode.Spot, baseAsset: symbol.BaseAsset, quoteAsset: symbol.QuoteAsset);
    }

    public static SharedBalance ToSharedBalance(this BinanceBalance balance)
    {
        return new(asset: balance.Asset, available: balance.Available, total: balance.Total);
    }

    public static TimeInForce ToBinanceTimeInForce(this SharedTimeInForce timeInForce)
    {
        return timeInForce switch
        {
            SharedTimeInForce.FillOrKill => TimeInForce.FillOrKill,
            SharedTimeInForce.GoodTillCanceled => TimeInForce.GoodTillCanceled,
            SharedTimeInForce.ImmediateOrCancel => TimeInForce.ImmediateOrCancel,
            _ => throw new NotImplementedException()
        };
    }

    public static SpotOrderType ToBinanceOrderType(this SharedOrderType type)
    {
        return type switch
        {
            SharedOrderType.Limit => SpotOrderType.Limit,
            SharedOrderType.Market => SpotOrderType.Market,
            SharedOrderType.LimitMaker => SpotOrderType.LimitMaker,
            _ => throw new NotImplementedException()
        };
    }

    public static SharedOrderType ToSharedOrderType(this SpotOrderType type)
    {
        return type switch
        {
            SpotOrderType.Limit => SharedOrderType.Limit,
            SpotOrderType.Market => SharedOrderType.Market,
            SpotOrderType.LimitMaker => SharedOrderType.LimitMaker,
            _ => throw new NotImplementedException()
        };
    }

    public static SharedOrderSide ToSharedOrderSide(this OrderSide side)
    {
        return side == OrderSide.Buy ? SharedOrderSide.Buy : SharedOrderSide.Sell;
    }

    public static OrderSide ToBinanceOrderSide(this SharedOrderSide side)
    {
        return side == SharedOrderSide.Buy ? OrderSide.Buy : OrderSide.Sell;
    }

    public static SharedOrderStatus ToSharedOrderStatus(this OrderStatus status)
    {
        return status switch
        {
            OrderStatus.New => SharedOrderStatus.Open,
            OrderStatus.Filled => SharedOrderStatus.Filled,
            OrderStatus.Expired => SharedOrderStatus.Canceled,
            OrderStatus.Rejected  => SharedOrderStatus.Canceled,
            OrderStatus.Canceled => SharedOrderStatus.Canceled,
            _ => throw new NotImplementedException()
        };
    }

    public static SharedSpotOrder ToSharedSpotOrder(this BinanceStreamOrderUpdate order)
    {
        return new(
            symbol: order.Symbol,
            orderId: order.Id.ToString(),
            orderType: order.Type.ToSharedOrderType(),
            orderSide: order.Side.ToSharedOrderSide(),
            orderStatus: order.Status.ToSharedOrderStatus(),
            createTime: order.CreateTime
        )
        {
            Quantity = order.Quantity,
            QuantityFilled = order.QuantityFilled,
            QuoteQuantity = order.QuoteQuantity,
            QuoteQuantityFilled = order.QuoteQuantityFilled,
            OrderPrice = order.Price,
            Fee = order.Fee,
            ClientOrderId = order.ClientOrderId
        };
    }
}