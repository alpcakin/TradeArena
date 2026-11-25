using System.Globalization;
using TradeArena.Contracts;

namespace TradeArena.PortfolioService;

// A player's cash and shares in the current round. All trading rules live in this class,
// so they can be unit tested without a database or a message broker.
public class Player
{
    public Guid Id { get; set; }
    public string Nickname { get; set; } = "";
    public int RoundNumber { get; set; }
    public decimal Cash { get; set; }
    public List<Position> Positions { get; set; } = [];

    public static Player Create(Guid id, string nickname, int roundNumber) =>
        new() { Id = id, Nickname = nickname, RoundNumber = roundNumber, Cash = GameRules.StartingCash };

    // The first time a player acts in a new round, the money and shares of the old round are reset.
    public void EnterRound(int roundNumber)
    {
        if (roundNumber == RoundNumber) return;

        RoundNumber = roundNumber;
        Cash = GameRules.StartingCash;
        foreach (var position in Positions) position.Quantity = 0;
    }

    public int SharesOf(string symbol) => Positions.FirstOrDefault(p => p.Symbol == symbol)?.Quantity ?? 0;

    // Executes a market order at the given price.
    // Returns null on success, otherwise the reason the order was rejected (and nothing is changed).
    public string? Execute(OrderSide side, string symbol, int quantity, decimal price)
    {
        if (quantity < 1) return "Quantity must be at least 1.";

        var total = price * quantity;
        var position = Positions.FirstOrDefault(p => p.Symbol == symbol);

        if (side == OrderSide.Buy)
        {
            if (total > Cash)
                return $"Not enough cash: {quantity} {symbol} costs {Money(total)}, you have {Money(Cash)}.";

            if (position is null)
                Positions.Add(position = new Position { PlayerId = Id, Symbol = symbol });

            Cash -= total;
            position.Quantity += quantity;
        }
        else
        {
            var owned = position?.Quantity ?? 0;
            if (quantity > owned)
                return $"Not enough shares: you own {owned} {symbol}.";

            Cash += total;
            position!.Quantity -= quantity;
        }

        return null;
    }

    public PortfolioUpdated ToSnapshot() => new(
        Id, Nickname, RoundNumber, Cash,
        Positions.Where(p => p.Quantity > 0)
                 .OrderBy(p => p.Symbol)
                 .Select(p => new Holding(p.Symbol, p.Quantity))
                 .ToArray());

    private static string Money(decimal value) => "$" + value.ToString("N2", CultureInfo.InvariantCulture);
}

// How many shares of one stock a player owns. A position whose quantity drops to 0 is kept
// in the table, so buying the same stock again only updates the row.
public class Position
{
    public Guid PlayerId { get; set; }
    public string Symbol { get; set; } = "";
    public int Quantity { get; set; }
}

// One executed order. The OrderId comes from the Gateway, which makes a repeated delivery
// of the same PlaceOrder command easy to detect.
public class Trade
{
    public Guid OrderId { get; set; }
    public Guid PlayerId { get; set; }
    public int RoundNumber { get; set; }
    public string Symbol { get; set; } = "";
    public OrderSide Side { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal Total { get; set; }
    public DateTime ExecutedAt { get; set; }
}
