using CryptoExchange.Net.SharedApis;
using Engine.Clients;
using Engine.Trades;
using Engine.Extensions;
using Engine.Strategies.TriangularArbitrage.Signals;
using Engine.Common;
using Engine.Orders;
using Microsoft.Extensions.Logging;

namespace Engine.Strategies.TriangularArbitrage.Workers;

public abstract class ArbitrageWorkerBase(
    IExchangeClient client, 
    ILogger<ArbitrageWorkerBase> logger, 
    IArbitrageSignals signals, 
    ISpotOrderManager orderManager, 
    ISymbolPriceManager pricesManager) : IArbitrageWorker
{
    public string Name { get; set; } = "Triangular Arbitrage Worker";
    public decimal MinProfitability { get; set; } = 0.002m;
    public string[] Pairs { get; set; } = [];
    public StrategyState State => _state;
    public IArbitrageSignals Signals => signals;

    private SharedSymbol[] _symbols = [];
    private PairPrecisionLimits _precisions = [];
    private StrategyState _state = StrategyState.Stopped;
    private CancellationToken _ct;

    public async Task Execute(CancellationToken ct)
    {
        _ct = ct;

        if (!await Initialize(ct))
        {
            TryUpdateState(StrategyState.Initializing, StrategyState.Failed);
            return;
        }

        TryUpdateState(StrategyState.Initializing, StrategyState.Running);

        while (true)
        {
            await pricesManager.WaitForPriceChange(_ct);
            await EvaluateArbitrageProfitability();
        }
    }

    private async Task PlaceOrder(int orderIdx)
    {
        SpotTradeBuilder builder = signals.GetOrderDataAt(orderIdx);

        SpotTradeBase trade = builder
            .SetStopPrice(50000m)
            .Build();

        await foreach (SharedSpotOrder order in orderManager.PlaceOrderAsync(trade, _ct))
        {
            switch (order.Status)
            {
                case SharedOrderStatus.Filled:
                    break;

                case SharedOrderStatus.Canceled:
                    break;

                case SharedOrderStatus.Open:
                    break;
            }
        }
    }

    protected virtual async Task<bool> Initialize(CancellationToken ct)
    {
        if (!TryUpdateState(StrategyState.Stopped, StrategyState.Initializing))
        {
            return false;
        }

        return InitializePairSymbols() &&
            await FetchPrecisionLimit(ct) &&
            InitializeSignals();
    }

    private bool InitializePairSymbols()
    {
        if (Pairs.Length != TriangularArbitrageStrategy.MaxPairs)
        {
            return false;
        }

        _symbols = [.. Pairs.Select(pair => pair.ToSharedSymbol(client))];
        pricesManager.Initialize(_symbols, _ct);

        return true;
    }

    private bool InitializeSignals()
    {
        signals.Symbols = _symbols;
        signals.Precisions = _precisions;
        signals.Initialize();

        return true;
    }

    private async Task<bool> FetchPrecisionLimit(CancellationToken ct)
    {
        _precisions = await client.FetchPrecisionLimits(_symbols, ct);
        return _precisions.Count == TriangularArbitrageStrategy.MaxPairs;
    }

    public async Task EvaluateArbitrageProfitability()
    {
        if (!TryUpdateState(StrategyState.Running, StrategyState.EvaluatingArbitrage))
        {
            return;
        }

        signals.Update(pricesManager.GetSnapshot());

        if (signals.Spread > 0)
        {
            await StartArbitrage();
            logger.LogArbitrageSignalResult(Pairs, signals.Ratio, signals.Spread);
        }
        else 
        {
            if (signals.Ratio >= MinProfitability && signals.Spread <= 0)
            {
                // TODO log warning
            }

            TryUpdateState(StrategyState.EvaluatingArbitrage, StrategyState.Running);
        }
    }

    protected async Task StartArbitrage()
    {
        if (!TryUpdateState(StrategyState.EvaluatingArbitrage, StrategyState.ArbitrageStarted))
        {
            return;
        }

        for (int orderIdx = 0; orderIdx < 3; ++orderIdx)
        {
            await PlaceOrder(orderIdx);
        }
    }

    private static Task TryReplaceOrder()
    {
        // TODO replace order until reached max retry
        return Task.CompletedTask;
    }

    private static Task ProcessNextOrder()
    {
        return Task.CompletedTask;
    }

    public abstract Task ProcessArbitrage();

    protected void RestartArbitrage()
    {
        if (TryUpdateState(StrategyState.EvaluatingArbitrage, StrategyState.Running))
        {
            return;
        } 
        else if (TryUpdateState(StrategyState.ArbitrageStarted, StrategyState.Running))
        {
            return;
        }
    }

    protected void StopWorker()
    {
        if (TryUpdateState(StrategyState.Initializing, StrategyState.Stopped))
        {
            return;
        }
        else if (TryUpdateState(StrategyState.Failed, StrategyState.Stopped))
        {
            return;
        }
        else if (TryUpdateState(StrategyState.Running, StrategyState.Stopped))
        {
            return;
        }
        else if (TryUpdateState(StrategyState.EvaluatingArbitrage, StrategyState.Stopped))
        {
            return;
        } 
        else if (TryUpdateState(StrategyState.ArbitrageStarted, StrategyState.Stopped))
        {
            return;
        }
    }

    public virtual async Task Terminate(CancellationToken ct)
    {
        // TODO Remove - simulate clean up
        await Task.Delay(TimeSpan.FromSeconds(2));

        StopWorker();
    }

    public bool TryUpdateState(StrategyState expectedStatus, StrategyState newStatus)
    {
        StrategyState originalValue = Interlocked.CompareExchange(ref _state, newStatus, expectedStatus);
        return originalValue == expectedStatus;
    }

    public string GetState()
    {
        // TODO
        return $"{Name} {_state}";
    }

}
