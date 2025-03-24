namespace Engine.Strategies.TriangularArbitrage.Signals;

using CryptoExchange.Net.SharedApis;
using Engine.Common;
using Engine.Trades;

public interface IArbitrageSignals
{
    SharedSymbol[] Symbols { get; set; }
    PairPrecisionLimits Precisions { get; set; }

    decimal Ratio { get; }
    decimal Spread { get; }

    void Initialize();
    void Update(Dictionary<string, SharedBookTicker> prices);
    SpotTradeBuilder GetOrderDataAt(int orderIdx);
}
