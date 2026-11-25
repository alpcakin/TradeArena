using TradeArena.Contracts;

namespace TradeArena.LeaderboardService;

// Read model of the current round, built only from events: the last portfolio snapshot of
// every player and the last price of every stock. It is kept in memory on purpose, because
// it is a copy of data owned by other services.
public class Standings
{
    private readonly Dictionary<Guid, PortfolioUpdated> _portfolios = new();
    private readonly Dictionary<string, decimal> _prices = new();

    public int RoundNumber { get; private set; }

    public void StartRound(int roundNumber)
    {
        RoundNumber = roundNumber;
        _portfolios.Clear();
    }

    public void Update(PortfolioUpdated portfolio)
    {
        if (portfolio.RoundNumber < RoundNumber) return;                       // late message from an old round
        if (portfolio.RoundNumber > RoundNumber) StartRound(portfolio.RoundNumber); // RoundStarted was missed
        _portfolios[portfolio.PlayerId] = portfolio;
    }

    public void UpdatePrices(IEnumerable<StockPrice> prices)
    {
        foreach (var p in prices) _prices[p.Symbol] = p.Price;
    }

    public LeaderboardEntry[] Rank() => Ranking.Build(_portfolios.Values, _prices);
}
