using Engine.Trades;
using CryptoExchange.Net.SharedApis;
using Bybit.Net.Interfaces.Clients;
using Microsoft.Extensions.Logging;
using Engine.Common;
using Bybit.Net.Objects.Models.V5;

namespace Engine.Clients;

public class ByBitClient(ILogger<BinanceClient> logger, IBybitSocketClient socketClient, IBybitRestClient restClient) : IExchangeClient
{
    public async Task<SharedSpotOrder?> PlaceOrder(SpotTradeBase trade, CancellationToken ct)
    {
        return trade switch
        {
            StopLossTrade stopLoss => await PlaceStopLossOrder(stopLoss),
            _ => throw new NotImplementedException()
        };
    }

    public Task<IDictionary<string, SharedBookTicker>> FetchPriceForSymbols(IEnumerable<SharedSymbol> symbols, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    private Task<SharedSpotOrder?> PlaceStopLossOrder(StopLossTrade trade)
    {
        throw new NotImplementedException();
    }

    public string SymbolFormatter(string @base, string quote, TradingMode mode, DateTime? dateTime)
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<SharedBalance>> GetBalances(CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task<PairPrecisionLimits> FetchPrecisionLimits(IEnumerable<SharedSymbol> pairs, CancellationToken ct)
	{
        throw new NotImplementedException();
    }

    public async Task<bool> SubscribeToPriceChanges(IEnumerable<SharedSymbol> symbols, Action<string, SharedBookTicker> onPriceChanged, CancellationToken ct)
    {
        // TODO check bybit symbol format
        IEnumerable<string> bybitSymbols = symbols.Select(symbol => $"{symbol.BaseAsset}{symbol.QuoteAsset}");

        var response = await socketClient.V5SpotApi.SubscribeToOrderbookUpdatesAsync(bybitSymbols, 1, response =>
        {
            if (response.Data.Asks.FirstOrDefault() is not BybitOrderbookEntry ask)
            {
                return;
            }

            if (response.Data.Bids.FirstOrDefault() is not BybitOrderbookEntry bid)
            {
                return;
            }

            onPriceChanged(response.Data.Symbol, new(ask.Price, ask.Quantity, bid.Price, bid.Quantity));
        }, ct);

        return response.Success;
    }

    public Task<bool> SubscribeToOrderUpdates(Action<SharedSpotOrder> onOrderUpdates, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}
