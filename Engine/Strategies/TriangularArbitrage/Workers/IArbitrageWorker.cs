using Engine.Strategies.TriangularArbitrage.Signals;

namespace Engine.Strategies.TriangularArbitrage.Workers;

// Loosely based on the Triangular Arbitrage strategy from Hummingbot
// https://github.com/hummingbot/hummingbot/blob/master/scripts/community/triangular_arbitrage.py

public enum StrategyMode
{
    Evaluating, // Only evaluate arbitrage opportunities without placing any orders.
    Dry,        // Simulate placing orders without actually executing any real trades.
    Live,       // Full trading mode. Place real orders and execute actual trades on the market.
}

public enum ArbitrageDirection 
{
    Direct, // buy ADA-USDT > sell ADA-BTC > sell BTC-USDT
    Reverse // buy BTC-USDT > buy ADA-BTC > sell ADA-USDT
}

public enum StrategyState
{
    Failed,                 // The strategy is in a failed state and needs to be stopped.
    Stopped,                // The strategy is not running and is in a stopped state.
    Initializing,           // The strategy is in the process of initialization and setting up necessary resources.
    Running,                // The strategy is running and waiting for new price updates to process.
    EvaluatingArbitrage,    // The strategy has locked the prices and is evaluating potential arbitrage opportunities.
    ArbitrageStarted        // An arbitrage opportunity has been identified, and the strategy is executing the arbitrage.
}

public interface IArbitrageWorker
{
    public StrategyState State { get; }
    IArbitrageSignals Signals { get; }

    Task Execute(CancellationToken ct);
    Task Terminate(CancellationToken ct);
    string GetState();
}
