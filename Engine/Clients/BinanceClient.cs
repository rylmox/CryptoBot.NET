using Engine.Trades;
using CryptoExchange.Net.SharedApis;
using Binance.Net.Interfaces.Clients;
using Binance.Net.Objects.Models.Spot;
using Microsoft.Extensions.Logging;
using Engine.Extensions;
using Engine.Utilities;
using Engine.Common;
using Binance.Net.Interfaces;

namespace Engine.Clients;

public class BinanceClient(ILogger<BinanceClient> logger, IBinanceSocketClient socketClient, IBinanceRestClient restClient) : IExchangeClient
{
    public string SymbolFormatter(string @base, string quote, TradingMode mode, DateTime? dateTime)
    {
        return $"{@base}{quote}";
    }

    private async Task<BinanceAccountInfo?> GetAccountInfo(CancellationToken ct)
    {
        var response = await socketClient.SpotApi.Account.GetAccountInfoAsync(omitZeroBalances: true, ct: ct);

        if (!response.Success)
        {
			logger.LogClientError(response.Error);
            return null;
        }

        return response.Data.Result;
    }

    public async Task<IEnumerable<SharedBalance>> GetBalances(CancellationToken ct)
    {
        BinanceAccountInfo? account = await GetAccountInfo(ct);

        return account?.Balances.Select(balance => balance.ToSharedBalance()) ?? [];
    }

	private async Task<BinanceExchangeInfo?> GetExhangeInfo(IEnumerable<string>? symbols, CancellationToken ct)
    {
        var response = await socketClient.SpotApi.ExchangeData.GetExchangeInfoAsync(symbols: symbols, ct: ct);
        
        if (!response.Success)
        {
			logger.LogClientError(response.Error);
			return null;
        }

        return response.Data.Result;
    }

    public async Task<PairPrecisionLimits> FetchPrecisionLimits(IEnumerable<SharedSymbol> symbols, CancellationToken ct)
	{
		PairPrecisionLimits precisions = [];
        IEnumerable<string> pairs = symbols.Select(symbol => symbol.GetSymbol(SymbolFormatter));

		if (await GetExhangeInfo(pairs, ct) is not BinanceExchangeInfo info)
        {
            return precisions;
        }

        foreach (BinanceSymbol symbolInfo in info.Symbols)
        {
            // stepSize: the precision for the quantity (baseAsset)
            // tickSize: the precision for the quoteQty (quoteAsset)
            // BaseAssetPrecision/quoteAssetPrecision: the amount of crypto that the exchange can handle (balance, commissions, etc.). 

            int basePrecision = MathUtilities.ExtractPrecision(symbolInfo.LotSizeFilter?.StepSize ?? 0);
            int quotePrecision = MathUtilities.ExtractPrecision(symbolInfo.PriceFilter?.TickSize ?? 0);
            precisions[symbolInfo.Name] = (basePrecision, quotePrecision);
        }

		logger.LogPrecisionLimits(precisions);

		return precisions;
    }

    public async Task<IDictionary<string, SharedBookTicker>> FetchPriceForSymbols(IEnumerable<SharedSymbol> symbols, CancellationToken ct)
    {
        IEnumerable<string> pairs = symbols.Select(symbol => symbol.GetSymbol(SymbolFormatter));

        var response = await socketClient.SpotApi.ExchangeData.GetTickersAsync(pairs, ct);

        if (!response.Success)
        {
            logger.LogClientError(response.Error);
            return new Dictionary<string, SharedBookTicker>();
        }

        return response.Data.Result.ToDictionary(
            price => price.Symbol, 
            price => new SharedBookTicker(price.BestAskPrice, price.BestAskQuantity, price.BestBidPrice, price.BestBidQuantity));
    }

    public async Task<SharedSpotOrder?> PlaceOrder(SpotTradeBase trade, CancellationToken ct)
    {
        return trade switch
        {
            StopLossTrade stopLoss => await PlaceStopLossOrder(stopLoss, ct),
            LimitTrade  limit => await PlaceLimitOrder(limit, ct),
            _ => throw new NotImplementedException()
        };
    }

    private static Task<SharedSpotOrder?> PlaceStopLossOrder(StopLossTrade trade, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    private async Task<SharedSpotOrder?> PlaceLimitOrder(LimitTrade trade, CancellationToken ct)
    {
        var response = await socketClient.SpotApi.Trading.PlaceOrderAsync(
            symbol: trade.Symbol,
            side: trade.Side.ToBinanceOrderSide(), 
            type: trade.Type.ToBinanceOrderType(), 
            quantity: trade.Quantity, 
            price: trade.Price, 
            timeInForce: trade.TimeInForce.ToBinanceTimeInForce(), 
            newClientOrderId: trade.OrderClientId,
            ct: ct
        );

        if (!response.Success)
        {
            logger.LogClientError(response.Error);
            return null;
        }

        return response.Data.Result.ToSharedSpotOrder();
    }

    public async Task<bool> SubscribeToPriceChanges(IEnumerable<SharedSymbol> symbols, Action<string, SharedBookTicker> onPriceChanged, CancellationToken ct)
    {
        IEnumerable<string> binanceSymbols = symbols.Select(symbol => $"{symbol.BaseAsset}{symbol.QuoteAsset}");

        var response = await socketClient.SpotApi.ExchangeData.SubscribeToTickerUpdatesAsync(binanceSymbols, response => 
        {
            IBinanceTick tick = response.Data;
            onPriceChanged(tick.Symbol, new(tick.BestAskPrice, tick.BestAskQuantity, tick.BestBidPrice, tick.BestBidQuantity));
        }, ct);

        // TODO test if the library trace the error for us
        if (!response.Success)
        {
            logger.LogClientError(response.Error);
            return false;
        }

        return true;
    }

    public async Task<bool> SubscribeToOrderUpdates(Action<SharedSpotOrder> onOrderUpdates, CancellationToken ct)
    {
        var response = await socketClient.SpotApi.Account.StartUserStreamAsync(ct);

        if (!response.Success)
        {
            logger.LogClientError(response.Error);
            return false;
        }

        string key = response.Data.Result;

        var subscriptionResult = await socketClient.SpotApi.Account.SubscribeToUserDataUpdatesAsync(
            key,
            onOrderUpdateMessage: response => onOrderUpdates?.Invoke(response.Data.ToSharedSpotOrder()),
            ct: ct
        );

        if (!subscriptionResult.Success)
        {
            logger.LogClientError(subscriptionResult.Error);
            return false;
        }
    
        StartKeyRefreshTimer(key);
        return true;
    }

    private void StartKeyRefreshTimer(string key)
    {
        Task.Run(async () =>
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromMinutes(30));
                await RefreshListenKey(key);
            }
        });
    }

    private async Task RefreshListenKey(string key)
    {
        var response = await socketClient.SpotApi.Account.KeepAliveUserStreamAsync(key);

        if (!response.Success)
        {
            logger.LogClientError(response.Error);
            return;
        }
    }
}
