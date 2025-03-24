using System.Collections.Concurrent;
using Binance.Net.Objects.Models.Spot;
using Binance.Net.Enums;
using Binance.Net.Objects.Models.Spot.Socket;
using Binance.Net.Interfaces;
using Microsoft.Extensions.Logging;
using Engine.Clients;

namespace Engine.Strategies.TriangularArbitrage.Workers;

#if false
public partial class BinanceWorkerDeprecated(ILogger<BinanceWorkerDeprecated> logger, IExchangeClient client) : ArbitrageWorkerBase(client)
{
    private enum DataLogType
    {
        Arbitrage,
        Order
    }

    public struct TradeStatistics
    {
        public decimal TotalProfit { get; set; }
        public int Evaluated { get; set; }         // Arbitrage evaluation
        public int Attempts { get; set; }           // Arbitrage attempts. Profitable arbitrage detected and started
        public int Successful { get; set; }            // Successful completed arbitrage
        public int ReverseAttemps { get; set; }     // TODO number of reverse completed arbitrage 
        public decimal LastRatio { get; set; }      // Last arbitrage ratio
        public decimal LastEstimetedProfit { get; set; }      // Last arbitrage ratio
        public int Pending { get; set; }
    }

    public int OrderTimeOut { get; init; } = 15000;
    public TradeStatistics TradeStats { get => _tradeStats; }
    public StrategyStatus Status { get => _status; }
    public StrategyMode Mode { get; init; } = StrategyMode.Live;
    public required string StrategyId { get; init; }
    public IEnumerable<string> DirectPairs => _trading.TradingPairs;
    public int ArbitrageLength => _trading.TradingPairs.Length;

    private StrategyStatus _status = StrategyStatus.Stopped;
    private TradeStatistics _tradeStats;
    private static int _arbitrageIdShared;
    private int _arbitrageId;
    private SemaphoreSlim _semaphore = new(1, 1);
    private readonly ConcurrentDictionary<string, (decimal ask, decimal bid)> _prices = [];
    private readonly Dictionary<ArbitrageDirection, OrderSide[]> _orderSides = [];
    private const int _placeOrderTrialsLimit = 0;
    private int _placeOrderTrialsCount;
    private string? _pendingOrderId;
    private ArbitrageDirection _profitableDirection = ArbitrageDirection.Direct;
    private BinanceStreamOrderUpdate?[] _orders = [];
    private CancellationTokenSource _orderTimoutTokenSource = new();
    private CancellationToken _orderTimoutToken => _orderTimoutTokenSource.Token;

    public void StartAsync()
    {
        Task.Run(Start);
    }

    public async Task Stop()
    {
        await StopArbitrage();
        await CancelAllOrders();
        _status = StrategyStatus.Stopped;
    }

    private async Task CancelAllOrders()
    {
        foreach (var order in _orders)
        {
            if (order!= null && order.Status != OrderStatus.Filled)
            {
                await Exchange.CancelOrder(order.Symbol, order.ClientOrderId);
            }
        }
    }

    public async Task<bool> Start()
    {
        if (!InitializeStrategy())
        {
            Logger.Error($"Cannot initialize strategy. Abort. - Id:{StrategyId} Name:{Name}");
            return false;
        }

        string pairs = string.Join(",", _trading.Pairs);
        Logger.Info($"Start strategy - Id:{StrategyId} Mode:{Mode} Pairs:{pairs}");

        return await StartListeningForChanges();
    }

    private bool InitializeStrategy()
    {
        if (!TryUpdateStatus(StrategyStatus.Stopped, StrategyStatus.Initializing))
        {
            return false;
        }

        _tradeStats = new()
        {
            Successful = 0,
            Attempts = 0,
            Evaluated = 0,
            LastRatio = 0,
            LastEstimetedProfit = 0,
            Pending = 0,
            TotalProfit = 0,
        };

        _orders = new BinanceStreamOrderUpdate[ArbitrageLength];

        ResetCancellationTokens();

        return true;
    }

    private void ResetCancellationTokens()
    {
        _orderTimoutTokenSource.Cancel();
        _orderTimoutTokenSource.Dispose();
        _orderTimoutTokenSource = new CancellationTokenSource();
    }

    private async Task<bool> StartListeningForChanges()
    {
        return TryUpdateStatus(StrategyStatus.Initializing, StrategyStatus.Running)
            && await FetchInitialPricesForAllSymbols() 
            && await FetchPrecisionLimits()
            && await SubscribeToPriceChanges() 
            && await SubscribeToAccountChanges();
    }

    private async Task<bool> FetchInitialPricesForAllSymbols()
    {
        _prices.Clear();

        foreach (Binance24HPrice price in await Exchange.FetchPriceForSymbols(DirectPairs.Distinct()))
        {
            _prices[price.Symbol] = (price.BestAskPrice, price.BestBidPrice);

            if (price.BestAskPrice <= 0m ||  price.BestBidPrice <= 0m)
            {
                // TODO start the strategy and wait for price changes instead of failing
                Logger.Error($"Invalid price - Id:{StrategyId} Symbol:{price.Symbol} Ask:{price.BestAskPrice:F4} Bid:{price.BestBidPrice:F4}");
                return false;
            }
            else
            {
                Logger.Debug($"Price found. - Id:{StrategyId} Symbol:{price.Symbol} Ask:{price.BestAskPrice:F4} Bid:{price.BestBidPrice:F4}");
            }
        }

        return _prices.Count == ArbitrageLength;
    }

    public async Task<bool> SubscribeToPriceChanges()
    {
        IEnumerable<string> pairs = DirectPairs.Distinct();
        Logger.Debug($"Subscribe to price changes - Id:{StrategyId} Pairs:{pairs}");

        return await Exchange.SubscribeToTickerUpdates(pairs, async response => await OnPriceChanges(response));
    }

    private async Task OnPriceChanges(IBinanceTick response)
    {
        // TODO We should instanciate a new Trading object and spawn a new task if a bew arbitrage is found.
        Logger.Debug($"New price - Id:{StrategyId} Symbol:{response.Symbol} Ask:{response.BestAskPrice:F6} Bid:{response.BestBidPrice:F6}");

        var newPrice = (ask: response.BestAskPrice, bid: response.BestBidPrice);

        if (newPrice.ask > 0 && newPrice.bid > 0)
        {
            _prices.AddOrUpdate(response.Symbol, newPrice, (key, value) => newPrice);
            await ComputeArbitrageProfitability();
        }
        else
        {
            Logger.Warn($"Invalid price spread - Id:{StrategyId} Symbol:{response.Symbol} Ask:{newPrice.ask} Bid:{newPrice.bid}");
        }
    }

    private async Task ComputeArbitrageProfitability()
    {
        if (!TryUpdateStatus(StrategyStatus.Running, StrategyStatus.EvaluatingArbitrage))
        {
            return;
        }

        _tradeStats.Evaluated += 1;

        var lockedPrices = _prices.ToDictionary(entry => entry.Key, entry => entry.Value);
        _trading.Update(lockedPrices);

        // TODO get ratio and quantities for Reverse arbitrage
        _profitableDirection = ArbitrageDirection.Direct;

        // TODO keep track of min and max ratio/spread
        _tradeStats.LastRatio = _trading.Ratio;
        _tradeStats.LastEstimetedProfit = _trading.Spread;

        await ProcessProfitability(_trading.Ratio, _trading.Spread);
    }

    private async Task ProcessProfitability(decimal ratio, decimal spread)
    {
        // TODO evaluate if we need to wait for both ratio and spread in order to start arbitrage
        // if (ratio >= MinProfitability || spread > 1)
        if (spread > 0)
        {
            Logger.Info($"Profitable arbitrage found - Id:{StrategyId} ArbitrageId:{_arbitrageId} Ratio:{ratio:F5} Spread:{spread}");
            await StartArbitrage(_profitableDirection);
        }
        else if (ratio >= MinProfitability && spread <= 0)
        {
            Logger.Warn($"Gross arbitrage spread negative - Id:{StrategyId} ArbitrageId:{_arbitrageId} Ratio:{ratio} Spread:{spread}");
            TryUpdateStatus(StrategyStatus.EvaluatingArbitrage, StrategyStatus.Running);
        }
        else
        {
            Logger.Debug($"Discarding arbitrage - Id:{StrategyId} ArbitrageId:{_arbitrageId} Ratio:{ratio:F5} Spread:{spread}");
            TryUpdateStatus(StrategyStatus.EvaluatingArbitrage, StrategyStatus.Running);
        }
    }

    private async Task StartArbitrage(ArbitrageDirection direction)
    {
        if (Mode == StrategyMode.Evaluating)
        {
            await StopArbitrage();
            return;
        }

        if (!TryUpdateStatus(StrategyStatus.EvaluatingArbitrage, StrategyStatus.ArbitrageStarted))
        {
            return;
        }

        _tradeStats.Attempts += 1;
        _arbitrageId = Interlocked.Increment(ref _arbitrageIdShared);

        Logger.Info($"Starting new arbitrage - Id:{StrategyId}");

        await PlaceOrder(0);
    }

    private async Task PlaceOrder(int orderIdx)
    {
        OrderData orderData = _trading.GetOrderDataAt(orderIdx);

        _pendingOrderId = Guid.NewGuid().ToString();

        Logger.Info($"Placing new order - Id:{StrategyId} OrderIndex:{orderIdx} OrderId:{_pendingOrderId} Symbol:{orderData.Symbol} Quantity:{orderData.Quantity} Price:{orderData.Price} Side:{orderData.Side}");

        // TODO use more restrictive time in force (PlaceOrKill or ImmediateOrCancel) and retry if fails.

        var newOrder = await Exchange.PlaceOrder
        (
            orderData.Symbol,
            orderData.Side,
            orderData.Quantity,
            orderData.Price,
            _pendingOrderId,
            SpotOrderType.Limit,
            TimeInForce.GoodTillCanceled
        );

        // TODO Should we try again when placing the order fails (maybe check the error type. e.g. we should not try again if balance is unsufficient?)

        await CancelOrderOnTimeout(newOrder);
    }

    private async Task CancelOrderOnTimeout(BinancePlacedOrder? order)
    {
        if (order == null)
        {
            await StopArbitrage();
            return;
        }

        try
        {
            if (OrderTimeOut <= 0)
            {
                return;
            }

            await Task.Delay(OrderTimeOut, _orderTimoutToken);
            await CancelOrderIfPlaced(order.Symbol, order.ClientOrderId);
            await StopArbitrage();
        }
        catch(TaskCanceledException)
        {
            Logger.Debug($"Order timout cancelled - Id:{StrategyId} Symbol:{order.Symbol} ClientOrderId:{order.ClientOrderId} Quantity:{order.Quantity}");
        }
        catch(Exception ex)
        {
            Logger.Error($"{ex.Message} - ${StrategyId}");
        }
    }
    
    private async Task CancelOrderIfPlaced(string symbol, string clientOrderId)
    {
        Logger.Info($"Cancelling order - Id:{StrategyId} Symbol:{symbol} OrderId:{clientOrderId} TimeOut:{OrderTimeOut}");

        if (await Exchange.GetOrder(symbol, clientOrderId) is not null)
        {
            await Exchange.CancelOrder(symbol, clientOrderId);
        }

        // TODO replace order?
    }

    private async Task ReplaceOrder(BinanceStreamOrderUpdate orderUpdate)
    {
        LogOrder("Replace order", orderUpdate);

        await _semaphore.WaitAsync();
        _placeOrderTrialsCount += 1;
        _semaphore.Release();

        if (_placeOrderTrialsCount >= _placeOrderTrialsLimit)
        {
            Logger.Warn($"Order trial limit reached - Id:{StrategyId} OrderId:{orderUpdate.ClientOrderId} Symbol:{orderUpdate.Symbol}");
            await StopArbitrage();
            return;
        }

        decimal newOrderQuantity = orderUpdate.Quantity - orderUpdate.QuantityFilled;

        var newOrder = await Exchange.ReplaceOrder(
            symbol: orderUpdate.Symbol, 
            side: orderUpdate.Side, 
            quantity: newOrderQuantity, 
            price: orderUpdate.Price, 
            clientId: orderUpdate.ClientOrderId);

        if (newOrder == null)
        {
            await StopArbitrage();
        }
    }

    private async Task ProcessNextOrder(BinanceStreamOrderUpdate order)
    {
        int orderIndex = _trading.GetOrderIndexFor(order.Symbol);

        if (orderIndex < 0)
        {
            Logger.Error($"Invalid order index - Id:{StrategyId} OrderIndex{orderIndex}");
            await StopArbitrage();
            return;
        }

        if (order.Status != OrderStatus.Filled)
        {
            Logger.Error($"Process next order. Invalid order status - Id:{StrategyId} Status{order.Status}");
            await StopArbitrage();
            return;
        }

        _orderTimoutTokenSource.Cancel();

        await ProcessOrder(orderIndex, order);

        if (orderIndex < 2)
        {
            await PlaceOrder(orderIndex + 1);
        }
        else
        {
            await FinalizeArbitrage();
            await StopArbitrage();
        }
    }

    private async Task ProcessOrder(int orderIndex, BinanceStreamOrderUpdate order)
    {
        try
        {
            await _semaphore.WaitAsync();
            _orders[orderIndex] = order;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task FinalizeArbitrage()
    {
        try
        {
            await _semaphore.WaitAsync();

            decimal firstFilled = _orders.FirstOrDefault()?.QuoteQuantityFilled ?? 0;
            decimal lastFilled = _orders.LastOrDefault()?.QuoteQuantityFilled ?? 0;
            decimal profit = lastFilled - firstFilled;
            _tradeStats.TotalProfit += profit;
            _tradeStats.Successful += 1;

            Logger.Info($"Arbitrage finalized - Id:{StrategyId} Profit:{profit} Total:{_tradeStats.TotalProfit}");
            Analytics.Trace($"{DataLogType.Arbitrage},{_arbitrageId},{Name},{firstFilled},{lastFilled},{profit},{_tradeStats.TotalProfit},{_tradeStats.Attempts},{_tradeStats.Evaluated},{_tradeStats.Successful},{_tradeStats.LastRatio:F6}");
        }
        finally
        {
            _semaphore.Release();
        }

        // TODO Immediately check for new arbitrage opportunities, as updated prices may have arrived.
    }

    private async Task StopArbitrage()
    {
        try
        {
            await _semaphore.WaitAsync();

            _placeOrderTrialsCount = 0;
            _pendingOrderId = null;
            _status = StrategyStatus.Running;

            ResetCancellationTokens();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<bool> SubscribeToAccountChanges()
    {
        return await Exchange.SubscribeToAccountChanges(
            onBalanceUpdate: OnBalanceUpdates,
            onOrderUpdates: async order => await OnOrderUpdates(order)
        );
    }

    private async Task<bool> FetchPrecisionLimits()
    {
        Logger.Debug($"Fetching precision limits - Id:{StrategyId}");

        _trading.Precisions = await Exchange.FetchPrecisionLimits(_trading.TradingPairs);

        bool success = _trading.Precisions.Count == ArbitrageLength;
        
        if (!success)
        {
            Logger.Error($"Didn't find precision for all assets - Id:{StrategyId}");
        }

        return success;
    }

    private void OnBalanceUpdates(BinanceStreamBalanceUpdate balance)
    {
        // TODO
        Logger.Debug($"Balance update - Id:{StrategyId} Asset:{balance.Asset} Delta:{balance.BalanceDelta}");
    }

    private async Task OnOrderUpdates(BinanceStreamOrderUpdate order)
    {
        if (_pendingOrderId == null || order.ClientOrderId != _pendingOrderId)
        {
            return;
        }

        LogOrderAnalytics(order);

        switch (order.Status)
        {
            case OrderStatus.Filled:
                LogOrder("Order filled", order);
                await ProcessNextOrder(order);
                _tradeStats.Pending = 0;
                break;

            case OrderStatus.Expired:
            case OrderStatus.ExpiredInMatch:
                LogOrder("Order expired", order);
                // await ReplaceOrder(order);
                break;

            case OrderStatus.New:
                LogOrder("New order placed", order);
                _tradeStats.Pending = 1;
                break;

            case OrderStatus.PendingNew:
                LogOrder("Order pending new", order);
                break;

            case OrderStatus.PartiallyFilled:
                LogOrder("Order partially filled", order);
                break;

            case OrderStatus.Canceled:
            case OrderStatus.Rejected:
            case OrderStatus.PendingCancel:
                LogOrder("Order canceled", order);
                await StopArbitrage();
                _tradeStats.Pending = 0;
                break;

            default:
                LogOrder("Order update ignored", order);
                break;
        }
    }

    private void LogOrderAnalytics(BinanceStreamOrderUpdate order)
    {
        Analytics.Trace($"{DataLogType.Order},{StrategyId},{_arbitrageId},{order.Symbol},{order.ClientOrderId},{order.Quantity},{order.QuantityFilled},{order.QuoteQuantityFilled},{order.Price},{order.Status}");
    }

    private void LogOrder(string message, BinanceStreamOrderUpdate order)
    {
        Logger.Info($"{message} - StrategyId:{StrategyId} ArbitrageId:{_arbitrageId} Symbol:{order.Symbol} ClientOrderId:{order.ClientOrderId} Quantity:{order.Quantity} QuantityFilled:{order.QuantityFilled} QuoteQuantityFilled:{order.QuoteQuantityFilled} Price:{order.Price} Status:{order.Status}");
    }
}

#endif