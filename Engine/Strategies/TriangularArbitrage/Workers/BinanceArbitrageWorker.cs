using Microsoft.Extensions.Logging;
using Engine.Clients;
using Engine.Strategies.TriangularArbitrage.Signals;
using Engine.Orders;

namespace Engine.Strategies.TriangularArbitrage.Workers;

public class BinanceArbitrageWorker(
    IExchangeClient client, 
    ILogger<BinanceArbitrageWorker> logger, 
    ISpotOrderManager orderManager, 
    IArbitrageSignals signals, 
    ISymbolPriceManager symbolManager) : ArbitrageWorkerBase(client, logger, signals, orderManager, symbolManager)
{
    public override Task ProcessArbitrage()
    {
        throw new NotImplementedException();
    }
}
