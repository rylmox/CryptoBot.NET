using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Engine.Strategies.TriangularArbitrage.Workers;

namespace Engine.Strategies.TriangularArbitrage;

public class TriangularArbitrageStrategy(IConfiguration configuration, IServiceProvider provider) : IStrategy
{
    public const int MaxPairs = 3;

    private List<IArbitrageWorker> _workers = [];

    public Task<bool> Initialize(CancellationToken ct)
    {
        _workers = configuration.GetSection("Strategy:Workers").GetChildren().Select(CreateWorker).ToList();
        return Task.FromResult(true);
    }

    private IArbitrageWorker CreateWorker(IConfiguration workerConfig)
    {
        IConfigurationSection strategyConfig = configuration.GetSection("Strategy");

        IArbitrageWorker worker = provider.GetRequiredService<IArbitrageWorker>();
        strategyConfig.Bind(worker);    // Bind default strategy settings first
        workerConfig.Bind(worker);      // Bind worker specific settings

        strategyConfig.Bind(worker.Signals);
        workerConfig.Bind(worker.Signals);

        return worker;
    }

    public async Task Execute(CancellationToken ct)
    {
        await Task.WhenAll(_workers.Select(op => op.Execute(ct)));
    }

    public async Task Terminate(CancellationToken ct)
    {
        await Task.WhenAll(_workers.Select(op => op.Terminate(ct)));
    }

    public string GetState()
    {
        return string.Join("\n", _workers.Select(op => op.GetState()));
    }
}
