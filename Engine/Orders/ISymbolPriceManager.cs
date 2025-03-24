using CryptoExchange.Net.SharedApis;
namespace Engine.Orders;

// TODO move the Symbol Price manager to a better location

public interface ISymbolPriceManager
{
    Task<bool> Initialize(IEnumerable<SharedSymbol> symbols, CancellationToken ct);
    Task WaitForPriceChange(CancellationToken ct);
    Dictionary<string, SharedBookTicker> GetSnapshot();
}