using TradeArena.Contracts;
using TradeArena.PortfolioService;

namespace TradeArena.Tests;

public class PlayerTests
{
    private static Player NewPlayer(int round = 1) => Player.Create(Guid.NewGuid(), "alice", round);

    [Fact]
    public void New_player_starts_with_the_starting_cash_and_no_shares()
    {
        var player = NewPlayer();

        Assert.Equal(GameRules.StartingCash, player.Cash);
        Assert.Empty(player.ToSnapshot().Holdings);
    }

    [Fact]
    public void Buying_takes_cash_and_adds_shares()
    {
        var player = NewPlayer();

        var error = player.Execute(OrderSide.Buy, "TECH", 10, 120.50m);

        Assert.Null(error);
        Assert.Equal(10_000m - 1_205m, player.Cash);
        Assert.Equal(10, player.SharesOf("TECH"));
    }

    [Fact]
    public void Buying_more_than_the_cash_allows_is_rejected_and_changes_nothing()
    {
        var player = NewPlayer();

        var error = player.Execute(OrderSide.Buy, "TECH", 100, 120m);

        Assert.StartsWith("Not enough cash", error);
        Assert.Equal(GameRules.StartingCash, player.Cash);
        Assert.Equal(0, player.SharesOf("TECH"));
    }

    [Fact]
    public void Spending_exactly_all_the_cash_is_allowed()
    {
        var player = NewPlayer();

        var error = player.Execute(OrderSide.Buy, "GAME", 400, 25m);

        Assert.Null(error);
        Assert.Equal(0m, player.Cash);
    }

    [Fact]
    public void Selling_gives_cash_back_at_the_current_price()
    {
        var player = NewPlayer();
        player.Execute(OrderSide.Buy, "FOOD", 20, 45m);

        var error = player.Execute(OrderSide.Sell, "FOOD", 5, 50m);

        Assert.Null(error);
        Assert.Equal(10_000m - 900m + 250m, player.Cash);
        Assert.Equal(15, player.SharesOf("FOOD"));
    }

    [Fact]
    public void Selling_shares_the_player_does_not_own_is_rejected()
    {
        var player = NewPlayer();
        player.Execute(OrderSide.Buy, "BANK", 3, 60m);

        var error = player.Execute(OrderSide.Sell, "BANK", 4, 60m);

        Assert.Equal("Not enough shares: you own 3 BANK.", error);
        Assert.Equal(3, player.SharesOf("BANK"));
    }

    [Fact]
    public void Selling_everything_removes_the_stock_from_the_snapshot()
    {
        var player = NewPlayer();
        player.Execute(OrderSide.Buy, "AUTO", 5, 80m);

        player.Execute(OrderSide.Sell, "AUTO", 5, 80m);

        Assert.Empty(player.ToSnapshot().Holdings);
        Assert.Equal(GameRules.StartingCash, player.Cash);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Quantity_must_be_positive(int quantity)
    {
        var player = NewPlayer();

        Assert.Equal("Quantity must be at least 1.", player.Execute(OrderSide.Buy, "TECH", quantity, 100m));
    }

    [Fact]
    public void Entering_a_new_round_resets_cash_and_shares()
    {
        var player = NewPlayer(round: 1);
        player.Execute(OrderSide.Buy, "TECH", 10, 100m);

        player.EnterRound(2);

        Assert.Equal(2, player.RoundNumber);
        Assert.Equal(GameRules.StartingCash, player.Cash);
        Assert.Equal(0, player.SharesOf("TECH"));
    }

    [Fact]
    public void Entering_the_same_round_again_keeps_the_portfolio()
    {
        var player = NewPlayer(round: 3);
        player.Execute(OrderSide.Buy, "TECH", 10, 100m);

        player.EnterRound(3);

        Assert.Equal(9_000m, player.Cash);
        Assert.Equal(10, player.SharesOf("TECH"));
    }
}
