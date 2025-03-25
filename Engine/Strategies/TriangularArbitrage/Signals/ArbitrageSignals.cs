using Engine.Trades;
using Microsoft.Extensions.Logging;
using CryptoExchange.Net.SharedApis;
using Engine.Utilities;
using Engine.Common;
using Engine.Extensions;

namespace Engine.Strategies.TriangularArbitrage.Signals;

using PairPrecision = (int @base, int quote);
using Engine.Clients;

public class ArbitrageSignals(IExchangeClient client, ILogger<ArbitrageSignals> logger) : IArbitrageSignals, IArbitrageSignaldata
{
    public SharedSymbol[] Symbols { get; set; } = [];
    public PairPrecisionLimits Precisions { get; set; } = [];
    public required string HoldingAsset { get; init; }
    public required decimal OrderAmount { get; init; }
    public required decimal Fee { get; init; }
    public int StepCount => Symbols.Length;

    public string[] Pairs { get; private set; } = [];
    public decimal Ratio { get; private set; } = 0;
    public decimal Spread { get; private set; } = 0;
    public SharedOrderSide[] Sides { get; private set; } = [];
    public decimal[] OrderQuantities { get; private set; } = [];
    public Dictionary<string, SharedBookTicker> Prices { get; private set; } = [];

    public void Initialize()
    {
        OrderQuantities = new decimal[StepCount];
        Pairs = Symbols.Select(symbol => symbol.GetSymbol(client)).ToArray();
        InitializeSharedOrderSides();
    }

    public void Update(Dictionary<string, SharedBookTicker> newPrices)
    {
        Prices = newPrices;
        ComputeCrossRateRatio();
        ComputeOrderQuantities();
        logger.LogArbitrageSignal(this);
    }

    public SpotTradeBuilder GetOrderDataAt(int orderIdx)
    {
        string symbol = Pairs[orderIdx];
        SharedOrderSide side = Sides[orderIdx];
        decimal quantity = OrderQuantities[orderIdx];
        decimal price = side == SharedOrderSide.Buy ? Prices[symbol].BestAskPrice : Prices[symbol].BestBidPrice;

        return new SpotTradeBuilder()
            .SetSymbol(symbol)
            .SetQuantity(quantity)
            .SetOrderPrice(price)
            .SetOrderSide(side);
    }

    public int GetOrderIndexFor(string symbol)
    {
        return Array.IndexOf(Pairs, symbol);
    }

    public void ComputeCrossRateRatio()
    {
        decimal ratio = 1;

        for (int stepIdx = 0; stepIdx < StepCount; ++stepIdx)
        {
            string symbol = Pairs[stepIdx];
            SharedOrderSide side = Sides[stepIdx];
            decimal price = GetPrice(symbol, side);

            ratio *= side == SharedOrderSide.Buy ? 1 / price : price;
        }

        decimal netRate = MathUtilities.Pow(1 - Fee, StepCount);
        Ratio = ratio * netRate;
    }

    // For trading fee see https://www.binance.com/en-JP/support/faq/how-to-use-bnb-to-pay-for-fees-and-earn-25-discount-115000583311

    // TODO Only support direct arbitrage for now
    public void ComputeOrderQuantities()
    {
        decimal orderAmount = OrderAmount;
        
        var (basePrecision0, quotePrecision0) = GetPrecision(0);
        var (basePrecision1, quotePrecision1) = GetPrecision(1);
        var (basePrecision2, quotePrecision2) = GetPrecision(2);
        
        // int precision = basePrecision2 > basePrecision1 ? basePrecision1 : basePrecision2;
        int precision = basePrecision2;
        orderAmount = MathUtilities.RoundUp(orderAmount / GetPrice(Pairs[2], SharedOrderSide.Sell), basePrecision1);

        OrderQuantities[2] = orderAmount;
        OrderQuantities[1] = orderAmount;

        // precision = basePrecision0 > quotePrecision1 ? basePrecision0 : quotePrecision1;
        precision = basePrecision0;
        OrderQuantities[0] = MathUtilities.RoundUp(orderAmount * GetPrice(Pairs[1], SharedOrderSide.Buy), precision);

        decimal startQuote = OrderQuantities[0] * GetPrice(Pairs[0], SharedOrderSide.Buy);
        decimal endQuote = OrderQuantities[2] * GetPrice(Pairs[2], SharedOrderSide.Sell);

        startQuote = MathUtilities.RoundUp(startQuote, quotePrecision0);
        endQuote = MathUtilities.RoundDown(endQuote, quotePrecision2);

        Spread = endQuote - startQuote;
    }

    private PairPrecision GetPrecision(int stepIdx)
    {
        string symbol = Pairs[stepIdx];
        return Precisions[symbol];
    }

    private decimal GetPrice(string symbol, SharedOrderSide side)
    {
        return side == SharedOrderSide.Buy ? Prices[symbol].BestAskPrice : Prices[symbol].BestBidPrice;
    }

    private void InitializeSharedOrderSides()
    {
        Sides = new SharedOrderSide[StepCount];
        string? previousBaseAsset = "";

        for (int pairIdx = 0; pairIdx < StepCount; ++pairIdx)
        {
            string baseAsset = Symbols[pairIdx].BaseAsset;

            SharedOrderSide side;
            if (pairIdx == 0)
            {
                side = baseAsset == HoldingAsset ? SharedOrderSide.Sell : SharedOrderSide.Buy;
            }
            else if (pairIdx == StepCount - 1)
            {
                side = baseAsset == HoldingAsset ? SharedOrderSide.Buy : SharedOrderSide.Sell;
            }
            else
            {
                side = baseAsset == previousBaseAsset ? SharedOrderSide.Sell : SharedOrderSide.Buy;
            }

            previousBaseAsset = baseAsset;
            Sides[pairIdx] = side;
        }

        // TODO logging
    }

}