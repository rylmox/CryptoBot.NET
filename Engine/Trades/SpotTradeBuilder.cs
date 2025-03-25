using CryptoExchange.Net.SharedApis;

namespace Engine.Trades;

public sealed class SpotTradeBuilder
{
    private string _symbol = string.Empty;
    private decimal _quantity = 0;
    private SharedOrderType _type = SharedOrderType.Limit;
    private decimal _orderPrice = 0;
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
            SharedOrderType.Other when _stopPrice.HasValue => CreateStopLossTrade(),
            SharedOrderType.Limit => CreateLimitTrade(),
            SharedOrderType.Market => CreateMarketTrade(),
            _ => throw new NotImplementedException()
        };
    }

    private LimitTrade CreateLimitTrade()
    {
        return new LimitTrade(_symbol, _quantity, _orderPrice) { Side = _orderSide };
    }

    private MarketTrade CreateMarketTrade()
    {
        return new MarketTrade(_symbol, _quantity, _orderPrice) { Side = _orderSide };
    }

    private StopLossTrade CreateStopLossTrade()
    {
        return new StopLossTrade(_symbol, _quantity, _orderPrice, _stopPrice!.Value) { Side = _orderSide };
    }
}
