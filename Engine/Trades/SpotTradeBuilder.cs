using CryptoExchange.Net.SharedApis;

namespace Engine.Trades;

public sealed class SpotTradeBuilder
{
    private string _symbol = string.Empty;
    private decimal _quantity;
    private SharedOrderType _type;
    private decimal? _orderPrice;
    private decimal? _stopPrice;
    private SharedOrderSide _orderSide = SharedOrderSide.Buy;

    public SpotTradeBuilder SetSymbol(string symbol)
    {
        _symbol = symbol;
        return this;
    }

    public SpotTradeBuilder SetQuantity(decimal quantity)
    {
        _quantity = quantity;
        return this;
    }

    public SpotTradeBuilder SetOrderSide(SharedOrderSide side)
    {
        _orderSide = side;
        return this;
    }


    public SpotTradeBuilder SetOrderPrice(decimal price)
    {
        _orderPrice = price;
        return this;
    }

    public SpotTradeBuilder SetStopPrice(decimal stopPrice)
    {
        _stopPrice = stopPrice;
        _type = SharedOrderType.Other;
        return this;
    }

    public SpotTradeBase Build()
    {
        return _type switch
        {
            SharedOrderType.Other when _stopPrice.HasValue => new StopLossTrade(_symbol, _quantity, _stopPrice.Value),
            SharedOrderType.Market => new MarketTrade(_symbol, _quantity),
            _ => throw new InvalidOperationException($"Invalid order type - {_type}")
        };
    }
}
