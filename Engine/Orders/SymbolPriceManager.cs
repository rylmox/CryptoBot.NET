using System.Collections.Concurrent;
using CryptoExchange.Net.SharedApis;
using Engine.Clients;
using Engine.Utilities;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;

namespace Engine.Orders;

public sealed class SymbolPriceManager(ILogger<SymbolPriceManager> logger, IExchangeClient client) : ISymbolPriceManager
{
    private readonly AsyncAutoResetEvent _priceUpdatedEvent = new();
    private readonly ConcurrentDictionary<string, SharedBookTicker> _tickers = [];

    public async Task<bool> Initialize(IEnumerable<SharedSymbol> symbols, CancellationToken ct)
    {
        return RegisterSymbols(symbols) &&
            await FetchInitialPrices(symbols, ct) &&
            await SubscribeToPriceChanges(symbols, ct);
    }

    private async Task<bool> FetchInitialPrices(IEnumerable<SharedSymbol> symbols, CancellationToken ct)
    {
        foreach (var kvp in await client.FetchPriceForSymbols(symbols, ct))
        {
            _tickers.AddOrUpdate(kvp.Key, kvp.Value, (key, value) => kvp.Value);
        }

        return symbols.Count() == _tickers.Count;
    }

    public async Task<bool> SubscribeToPriceChanges(IEnumerable<SharedSymbol> symbols, CancellationToken ct)
    {
        return await client.SubscribeToPriceChanges(symbols, OnPriceChanged, ct);
    }

    private void OnPriceChanged(string name, SharedBookTicker ticker)
    {
        if (TradingUtilities.IsPriceValid(ticker))
        {
            logger.LogBookTicker(name, ticker);
            _tickers[name] = ticker;
            _priceUpdatedEvent.Set();
        }
    }

    private bool RegisterSymbols(IEnumerable<SharedSymbol> names)
    {
        return names.All(RegisterNewSymbol);
    }

    private bool RegisterNewSymbol(SharedSymbol symbol)
    {
        return _tickers.TryAdd(symbol.GetSymbol(client.SymbolFormatter), new(0, 0, 0, 0));
    }

    public async Task WaitForPriceChange(CancellationToken ct = default)
    {
        await _priceUpdatedEvent.WaitAsync(ct);
    }

    public Dictionary<string, SharedBookTicker> GetSnapshot()
    {
        return _tickers.ToDictionary();
    }
}