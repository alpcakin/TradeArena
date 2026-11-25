using TradeArena.MarketService;

namespace TradeArena.Tests;

public class StockTests
{
    [Fact]
    public void Each_move_stays_within_two_percent_and_is_rounded_to_cents()
    {
        var stock = new Stock("TECH", "TechCorp", 100m);
        var previous = 100m;

        for (var i = 0; i < 1_000; i++)
        {
            var price = stock.Move().Price;
            Assert.InRange(price, Math.Round(previous * 0.98m, 2) - 0.01m, Math.Round(previous * 1.02m, 2) + 0.01m);
            Assert.Equal(price, Math.Round(price, 2));
            previous = price;
        }
    }

    [Fact]
    public void Price_never_drops_below_one_dollar()
    {
        var stock = new Stock("PENNY", "Penny Stock", 1m);

        for (var i = 0; i < 1_000; i++)
            Assert.True(stock.Move().Price >= 1m);
    }

    [Fact]
    public void Change_is_measured_from_the_start_price()
    {
        var stock = new Stock("TECH", "TechCorp", 50m);

        var tick = stock.Move();

        Assert.Equal(Math.Round((tick.Price - 50m) / 50m * 100, 2), tick.ChangePercent);
    }
}
