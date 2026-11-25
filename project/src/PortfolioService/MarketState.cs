using System.Collections.Concurrent;
using TradeArena.Contracts;

namespace TradeArena.PortfolioService;

// The latest price of every stock, taken from the MarketTick events.
// Orders are executed at these prices, the same ones the players see on screen.
public class PriceBook
{
    private readonly ConcurrentDictionary<string, decimal> _prices = new();

    public bool IsEmpty => _prices.IsEmpty;

    public void Update(IEnumerable<StockPrice> prices)
    {
        foreach (var p in prices) _prices[p.Symbol] = p.Price;
    }

    public bool TryGetPrice(string symbol, out decimal price) => _prices.TryGetValue(symbol, out price);
}

// Which round is running, taken from the RoundStarted and RoundEnded events.
public class RoundState
{
    public int CurrentRound { get; private set; }
    public bool IsOpen { get; private set; } = true;

    public void Restore(int lastKnownRound) => CurrentRound = lastKnownRound;

    public void Start(int roundNumber)
    {
        CurrentRound = roundNumber;
        IsOpen = true;
    }

    public void End(int roundNumber)
    {
        if (roundNumber < CurrentRound) return;
        CurrentRound = roundNumber;
        IsOpen = false;
    }
}
