using Engine.Strategies;
using Nito.AsyncEx;
using Microsoft.Extensions.Hosting;

namespace Engine.Service;

public class StrategyService(IStrategy strategy) : BackgroundService
{
    private CancellationTokenSource? _strategyCts;
    private CancellationToken _stoppingAppToken = CancellationToken.None;
    private bool _strategyStarted;
    private bool _strategyInitialized;
    private readonly AsyncLock _mutex = new();
    public IStrategy Strategy => strategy;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using (await _mutex.LockAsync(stoppingToken))
        {
            _stoppingAppToken = stoppingToken;
            await StartStrategyInternal();
        }
    }

    public async Task StartStrategy()
    {
        using (await _mutex.LockAsync(_stoppingAppToken))
        {
            if (_strategyStarted)
            {
                return;
            }

            await StartStrategyInternal();
        }
    }
    
    public async Task StopStrategy()
    {
        using (await _mutex.LockAsync(_stoppingAppToken))
        {
            _strategyCts?.Cancel();
            _strategyCts?.Dispose();
            _strategyCts = null;
        }
    }

    private async Task StartStrategyInternal()
    {
        _strategyCts = CancellationTokenSource.CreateLinkedTokenSource(_stoppingAppToken);
        CancellationToken ct = _strategyCts.Token;

        ct.Register(TerminateStrategy);

        if (await InitializeStrategy(ct))
        {
            _strategyStarted = true;
            await strategy.Execute(ct);
        }
    }

    private async Task<bool> InitializeStrategy(CancellationToken ct)
    {
        if (_strategyInitialized)
        {
            return true;
        }

        if (!await strategy.Initialize(ct))
        {
            return false;
        }

        _strategyInitialized = true;

        return true;
    }

    private void TerminateStrategy()
    {
        try
        {
            strategy.Terminate(_stoppingAppToken).GetAwaiter().GetResult();
            _strategyStarted = false;
        }
        catch (OperationCanceledException)
        {
        }
    }
}