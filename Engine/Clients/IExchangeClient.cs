using CryptoExchange.Net.SharedApis;
using Engine.Common;
using Engine.Trades;

namespace Engine.Clients;

// FIXME this interface accept SharedSymbol (abstraction for asset pairs). The client implementation
// then convert the SharedSymbol (using GetSymbol and a formatter) to the exchange format. 
// This adds overload and we could accept string instead and the interface user should 
// convert the symbols first (and cache them by setting SymbolName in SharedSymbol). 

public interface IExchangeClient
{
    Task<IEnumerable<SharedBalance>> GetBalances(CancellationToken ct);
    Task<SharedSpotOrder?> PlaceOrder(SpotTradeBase trade, CancellationToken ct);
    Task<IDictionary<string, SharedBookTicker>> FetchPriceForSymbols(IEnumerable<SharedSymbol> symbols, CancellationToken ct);

    // Store pair precision limits in a dictionary for quick lookup. The key is the symbol in no-separator format (e.g. "BTCUSDT").
    Task<PairPrecisionLimits> FetchPrecisionLimits(IEnumerable<SharedSymbol> pairs, CancellationToken ct);

    Task<bool> SubscribeToPriceChanges(IEnumerable<SharedSymbol> symbols, Action<string, SharedBookTicker> onPriceChangeda, CancellationToken ct);
    Task<bool> SubscribeToOrderUpdates(Action<SharedSpotOrder> onOrderUpdates, CancellationToken ct);

    string SymbolFormatter(string @base, string quote, TradingMode mode, DateTime? dateTime);
}
