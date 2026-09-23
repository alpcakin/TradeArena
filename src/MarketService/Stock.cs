using TradeArena.Contracts;

namespace TradeArena.MarketService;

public class Stock(string symbol, string name, decimal startPrice)
{
    private readonly decimal _startPrice = startPrice;
    private decimal _price = startPrice;

    // Moves the price by a random amount between -2% and +2% (a "random walk").
    public StockPrice Move()
    {
        var stepPercent = (decimal)(Random.Shared.NextDouble() * 4 - 2);
        _price = Math.Max(1m, Math.Round(_price * (1 + stepPercent / 100), 2));

        var changeSinceStart = Math.Round((_price - _startPrice) / _startPrice * 100, 2);
        return new StockPrice(symbol, name, _price, changeSinceStart);
    }
}
