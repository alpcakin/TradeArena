using MassTransit;
using Microsoft.EntityFrameworkCore;
using TradeArena.Contracts;

namespace TradeArena.PortfolioService;

public class MarketTickConsumer(PriceBook prices) : IConsumer<MarketTick>
{
    public Task Consume(ConsumeContext<MarketTick> context)
    {
        prices.Update(context.Message.Prices);
        return Task.CompletedTask;
    }
}

public class RoundConsumer(RoundState round, ILogger<RoundConsumer> logger) : IConsumer<RoundStarted>, IConsumer<RoundEnded>
{
    public Task Consume(ConsumeContext<RoundStarted> context)
    {
        round.Start(context.Message.RoundNumber);
        logger.LogInformation("Round {Round} started, accepting orders", context.Message.RoundNumber);
        return Task.CompletedTask;
    }

    public Task Consume(ConsumeContext<RoundEnded> context)
    {
        round.End(context.Message.RoundNumber);
        logger.LogInformation("Round {Round} ended, orders are closed", context.Message.RoundNumber);
        return Task.CompletedTask;
    }
}

// Creates the portfolio on the first visit. A returning player keeps their portfolio
// (or gets a fresh one if a new round has started) and receives a snapshot of it.
public class PlayerJoinedConsumer(PortfolioDb db, RoundState round) : IConsumer<PlayerJoined>
{
    public async Task Consume(ConsumeContext<PlayerJoined> context)
    {
        var message = context.Message;
        var player = await db.Players.Include(p => p.Positions)
                                     .FirstOrDefaultAsync(p => p.Id == message.PlayerId, context.CancellationToken);
        if (player is null)
        {
            player = Player.Create(message.PlayerId, message.Nickname, round.CurrentRound);
            db.Players.Add(player);
        }
        else
        {
            player.Nickname = message.Nickname;
            player.EnterRound(round.CurrentRound);
        }

        await db.SaveChangesAsync(context.CancellationToken);
        await context.Publish(player.ToSnapshot());
    }
}

public class PlaceOrderConsumer(PortfolioDb db, PriceBook prices, RoundState round, ILogger<PlaceOrderConsumer> logger)
    : IConsumer<PlaceOrder>
{
    public async Task Consume(ConsumeContext<PlaceOrder> context)
    {
        var order = context.Message;

        // RabbitMQ delivers a message at least once. An order that was already executed is skipped.
        if (await db.Trades.AnyAsync(t => t.OrderId == order.OrderId, context.CancellationToken))
            return;

        var player = await db.Players.Include(p => p.Positions)
                                     .FirstOrDefaultAsync(p => p.Id == order.PlayerId, context.CancellationToken);
        decimal price = 0;
        string? reason;

        if (player is null)
            reason = "Join the arena first.";
        else if (!round.IsOpen)
            reason = "The round is over. Wait for the next one to start.";
        else if (!prices.TryGetPrice(order.Symbol, out price))
            reason = prices.IsEmpty ? "The market has not opened yet." : $"Unknown stock: {order.Symbol}.";
        else
        {
            player.EnterRound(round.CurrentRound);
            reason = player.Execute(order.Side, order.Symbol, order.Quantity, price);
        }

        if (reason is not null)
        {
            await context.Publish(new OrderRejected(order.OrderId, order.PlayerId, order.Symbol, order.Side, order.Quantity, reason));
            return;
        }

        var total = price * order.Quantity;
        db.Trades.Add(new Trade
        {
            OrderId = order.OrderId,
            PlayerId = order.PlayerId,
            RoundNumber = player!.RoundNumber,
            Symbol = order.Symbol,
            Side = order.Side,
            Quantity = order.Quantity,
            Price = price,
            Total = total,
            ExecutedAt = DateTime.UtcNow,
        });

        // One SaveChanges = one transaction: cash, shares and the trade row change together.
        await db.SaveChangesAsync(context.CancellationToken);

        logger.LogInformation("{Nickname}: {Side} {Quantity} {Symbol} at {Price}",
            player.Nickname, order.Side, order.Quantity, order.Symbol, price);

        await context.Publish(new OrderExecuted(order.OrderId, order.PlayerId, order.Symbol, order.Side, order.Quantity, price, total));
        await context.Publish(player.ToSnapshot());
    }
}
