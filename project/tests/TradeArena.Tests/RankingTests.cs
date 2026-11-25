using TradeArena.Contracts;
using TradeArena.LeaderboardService;

namespace TradeArena.Tests;

public class RankingTests
{
    private static readonly Dictionary<string, decimal> Prices = new() { ["TECH"] = 100m, ["GAME"] = 20m };

    private static PortfolioUpdated Portfolio(string nickname, decimal cash, params Holding[] holdings) =>
        new(Guid.NewGuid(), nickname, 1, cash, holdings);

    [Fact]
    public void Net_worth_is_cash_plus_shares_at_the_latest_price()
    {
        var portfolio = Portfolio("alice", 5_000m, new Holding("TECH", 30), new Holding("GAME", 10));

        Assert.Equal(5_000m + 3_000m + 200m, Ranking.NetWorth(portfolio, Prices));
    }

    [Fact]
    public void Players_are_sorted_by_net_worth_with_profit_in_percent()
    {
        var entries = Ranking.Build(
        [
            Portfolio("bob", 9_000m),
            Portfolio("alice", 2_000m, new Holding("TECH", 100)),
            Portfolio("carol", 10_000m),
        ], Prices);

        Assert.Equal(["alice", "carol", "bob"], entries.Select(e => e.Nickname));
        Assert.Equal([1, 2, 3], entries.Select(e => e.Rank));
        Assert.Equal(20m, entries[0].ProfitPercent);
        Assert.Equal(-10m, entries[2].ProfitPercent);
    }

    [Fact]
    public void Players_with_the_same_net_worth_share_a_rank()
    {
        var entries = Ranking.Build(
        [
            Portfolio("alice", 10_000m),
            Portfolio("bob", 10_000m),
            Portfolio("carol", 9_000m),
        ], Prices);

        Assert.Equal([1, 1, 3], entries.Select(e => e.Rank));
    }

    [Fact]
    public void Standings_ignore_snapshots_from_an_old_round()
    {
        var standings = new Standings();
        standings.StartRound(5);

        standings.Update(new PortfolioUpdated(Guid.NewGuid(), "late", 4, 10_000m, []));
        standings.Update(new PortfolioUpdated(Guid.NewGuid(), "alice", 5, 10_000m, []));

        Assert.Equal(["alice"], standings.Rank().Select(e => e.Nickname));
    }

    [Fact]
    public void A_new_round_clears_the_standings()
    {
        var standings = new Standings();
        standings.Update(new PortfolioUpdated(Guid.NewGuid(), "alice", 1, 10_000m, []));

        standings.StartRound(2);

        Assert.Empty(standings.Rank());
    }
}
