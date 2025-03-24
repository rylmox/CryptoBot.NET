using Binance.Net.Objects.Models;
using Binance.Net.Objects.Models.Spot;
using Binance.Net.Enums;
using Binance.Net.Objects.Models.Spot.Socket;
using Binance.Net.Interfaces;
using Microsoft.Extensions.Logging;
using Binance.Net.Interfaces.Clients;

namespace Engine.Clients;

using PairPrecisionList = Dictionary<string, (int @base, int quote)>;

public class BinanceClientDeprecated(ILogger<BinanceClientDeprecated> logger, IBinanceSocketClient client)
{
    public async Task UnsubscribeAll()
    {
        await client.SpotApi.UnsubscribeAllAsync();
    }

    public async Task<BinanceOrder?> GetOrder(string symbol, string clientOrderId)
    {
        var response = await client.SpotApi.Trading.GetOrderAsync(symbol, clientOrderId: clientOrderId);

        if (!response.Success)
        {
            logger.LogClientError(response.Error);
            return null;
        }

        return response.Data?.Result;
    }

    public async Task<BinanceOrder?> CancelOrder(string symbol, string clientOrderId)
    {
        var response = await client.SpotApi.Trading.CancelOrderAsync(symbol, clientOrderId: clientOrderId);

        if (!response.Success)
        {
            logger.LogClientError(response.Error);
            return null;
        }

        return response.Data?.Result;
    }

    public async Task<IEnumerable<BinanceOrder>> CancelAllOrders(string symbol)
    {
        var response = await client.SpotApi.Trading.CancelAllOrdersAsync(symbol);

        if (!response.Success)
        {
            logger.LogClientError(response.Error);
            return [];
        }

        return response.Data.Result;
    }

    public async Task<BinanceOrderBook?> GetOrderBook(string symbol, int limit)
    {
        var response = await client.SpotApi.ExchangeData.GetOrderBookAsync(symbol, limit);

        if (response.Success)
        {
            return response.Data.Result;
        }

        logger.LogClientError(response.Error);
        return null;
    }

    public async Task<bool> SubscribeToTickerUpdates(IEnumerable<string> pairs, Action<IBinanceTick> callback)
    {
        var response = await client!.SpotApi.ExchangeData.SubscribeToTickerUpdatesAsync(pairs, response => callback(response.Data));

        if (!response.Success)
        {
            logger.LogClientError(response.Error);
            return false;
        }

        return true;
    }

    public async Task<IEnumerable<Binance24HPrice>> FetchPriceForSymbols(IEnumerable<string> pairs)
    {
        var response = await client!.SpotApi.ExchangeData.GetTickersAsync(pairs);

        if (!response.Success)
        {
            logger.LogClientError(response.Error);
            return [];
        }

        return response.Data.Result;
    }

    public async Task<BinanceAccountInfo?> GetAccountInfo()
    {
        var response = await client.SpotApi.Account.GetAccountInfoAsync();

        if (!response.Success)
        {
            logger.LogClientError(response.Error);
            return null;
        }

        return response.Data.Result;
    }

    public async Task<IEnumerable<BinanceBalance>> GetBalances()
    {
        BinanceAccountInfo? account = await GetAccountInfo();
        return account?.Balances ?? [];
    }

    public async Task<BinanceExchangeInfo?> GetExhangeInfo()
    {
        var response = await client.SpotApi.ExchangeData.GetExchangeInfoAsync();
        
        if (!response.Success)
        {
            logger.LogClientError(response.Error);
            return null;
        }

        return response.Data.Result;
    }

    public async Task<BinanceBalance?> GetBalance(string symbol)
    {
        IEnumerable<BinanceBalance> balances = await GetBalances();
        
        return balances.ToList().Find(balance => balance.Asset == symbol);
    }

    public async Task<bool> SubscribeToAccountChanges(Action<BinanceStreamOrderUpdate>? onOrderUpdates, Action<BinanceStreamBalanceUpdate>? onBalanceUpdate)
    {
        var response = await client.SpotApi.Account.StartUserStreamAsync();

        if (!response.Success)
        {
            logger.LogClientError(response.Error);
            return false;
        }

        string key = response.Data.Result;

        var subscriptionResult = await client.SpotApi.Account.SubscribeToUserDataUpdatesAsync(
            key,
            onAccountBalanceUpdate: response => onBalanceUpdate?.Invoke(response.Data),
            onOrderUpdateMessage: response => onOrderUpdates?.Invoke(response.Data),
            onListenKeyExpired: response => OnListenKeyExpired(response.Data)
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
        var response = await client.SpotApi.Account.KeepAliveUserStreamAsync(key);

        if (!response.Success)
        {
            logger.LogClientError(response.Error);
            return;
        }
    }

    private static void OnListenKeyExpired(BinanceStreamEvent e)
    {
        // TODO
    }

    public async Task<Dictionary<string, List<string>>> GetCoinsInfo(string[] quotes)
    {
        Dictionary<string, List<string>> symbols = [];
        quotes.ToList().ForEach(asset => symbols[asset] = []);

        if (await GetExhangeInfo() is not BinanceExchangeInfo info)
        {
            return symbols;
        }

        foreach (var symbolInfo in info.Symbols)
        {
            foreach (string quote in quotes)
            {
                if (symbolInfo.Name.EndsWith(quote))
                {
                    symbols[quote].Add(symbolInfo.Name[..^quote.Length]);
                }
            }
        }

        return symbols;
    }

    public async Task<BinancePlacedOrder?> ReplaceOrder(string symbol, OrderSide side, decimal quantity, decimal price, string clientId, SpotOrderType type = SpotOrderType.Limit, TimeInForce timeInForce = TimeInForce.ImmediateOrCancel)
    {
        var response = await client.SpotApi.Trading.ReplaceOrderAsync(
            symbol:symbol,
            side: side,
            quantity: quantity,
            price: price,
            type: type,
            timeInForce: timeInForce,
            cancelReplaceMode: CancelReplaceMode.AllowFailure,
            cancelClientOrderId: clientId,
            newCancelClientOrderId: Guid.NewGuid().ToString(),
            newClientOrderId: clientId
        );

        if (response.Success && response.Data.Result.NewOrderResponse != null)
        {
            if (response.Data.Result.NewOrderResult == OrderOperationResult.Success)
            {
                return response.Data.Result.NewOrderResponse;
            }
            else
            {
                // TODO
                // Logger.Error(response.Data.Result.NewOrderResponse.Message);
            }
        }

        logger.LogClientError(response.Error);
        return null;
    }

    public async Task<BinancePlacedOrder?> PlaceOrder(string symbol, OrderSide side, decimal quantity, decimal price, string? clientId = null, SpotOrderType type = SpotOrderType.Limit, TimeInForce timeInForce = TimeInForce.FillOrKill, CancellationToken ct = default)
    {
        var response = await client.SpotApi.Trading.PlaceOrderAsync(
            symbol: symbol, 
            side: side, 
            type: type, 
            quantity: quantity, 
            price: price, 
            timeInForce: timeInForce, 
            ct: ct,
            newClientOrderId: clientId);

        if (!response.Success)
        {
            logger.LogClientError(response.Error);
            return null;
        }

        return response.Data.Result;
    }

    public async Task<BinancePlacedOrder?> PlaceMarketOrder(string symbol, OrderSide side, decimal quantity)
    {
        var response = await client.SpotApi.Trading.PlaceOrderAsync(symbol: symbol, side: side, quantity: quantity, type: SpotOrderType.Market);

        if (!response.Success)
        {
            logger.LogClientError(response.Error);
            return null;
        }

        return response.Data.Result;
    }

    public async Task<PairPrecisionList> FetchPrecisionLimits(string[] pairs)
    {
        PairPrecisionList precisions = [];

        if (await GetExhangeInfo() is not BinanceExchangeInfo info)
        {
            return precisions; 
        }

        foreach (var symbolInfo in info.Symbols)
        {
            if (!pairs.Contains(symbolInfo.Name))
            {
                continue;
            }

            // stepSize: the precision for the quantity (baseAsset)
            // tickSize: the precision for the quoteQty (quoteAsset)
            // BaseAssetPrecision/quoteAssetPrecision: the amount of crypto that the exchange can handle (balance, commissions, etc.). 

            int basePrecision = ExtractPrecision(symbolInfo.LotSizeFilter?.StepSize ?? 0);
            int quotePrecision = ExtractPrecision(symbolInfo.PriceFilter?.TickSize ?? 0);
            precisions[symbolInfo.Name] = (basePrecision, quotePrecision);
        }

        return precisions;
    }

    private static int ExtractPrecision(decimal value)
    {
        string valueStr = value.ToString().TrimEnd('0');
        int decimalIndex = valueStr.IndexOf('.');
        return decimalIndex == -1 ? 0 : valueStr.Length - decimalIndex - 1;
    }

}
