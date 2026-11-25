using TradeArena.Contracts;

namespace TradeArena.LeaderboardService;

public static class Ranking
{
    public static decimal NetWorth(PortfolioUpdated portfolio, IReadOnlyDictionary<string, decimal> prices) =>
        portfolio.Cash + portfolio.Holdings.Sum(h => h.Quantity * prices.GetValueOrDefault(h.Symbol));

    // Sorts the players by net worth. Players with the same net worth share a rank (1, 1, 3, ...).
    public static LeaderboardEntry[] Build(IEnumerable<PortfolioUpdated> portfolios, IReadOnlyDictionary<string, decimal> prices)
    {
        var rows = portfolios
            .Select(p => (p.Nickname, NetWorth: NetWorth(p, prices)))
            .OrderByDescending(r => r.NetWorth)
            .ThenBy(r => r.Nickname, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var entries = new LeaderboardEntry[rows.Count];
        for (var i = 0; i < rows.Count; i++)
        {
            var (nickname, netWorth) = rows[i];
            var rank = i > 0 && netWorth == entries[i - 1].NetWorth ? entries[i - 1].Rank : i + 1;
            var profit = Math.Round((netWorth - GameRules.StartingCash) / GameRules.StartingCash * 100, 2);
            entries[i] = new LeaderboardEntry(rank, nickname, netWorth, profit);
        }
        return entries;
    }
}
