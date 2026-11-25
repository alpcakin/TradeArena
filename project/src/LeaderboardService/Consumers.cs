using MassTransit;
using TradeArena.Contracts;

namespace TradeArena.LeaderboardService;

public class PortfolioUpdatedConsumer(Standings standings) : IConsumer<PortfolioUpdated>
{
    public Task Consume(ConsumeContext<PortfolioUpdated> context)
    {
        standings.Update(context.Message);
        return Task.CompletedTask;
    }
}

public class RoundStartedConsumer(Standings standings) : IConsumer<RoundStarted>
{
    public Task Consume(ConsumeContext<RoundStarted> context)
    {
        standings.StartRound(context.Message.RoundNumber);
        return Task.CompletedTask;
    }
}

// The ranking is recalculated on every tick, so the browsers get one leaderboard message
// per second no matter how many trades happen in between.
public class MarketTickConsumer(Standings standings) : IConsumer<MarketTick>
{
    public async Task Consume(ConsumeContext<MarketTick> context)
    {
        standings.UpdatePrices(context.Message.Prices);
        await context.Publish(new LeaderboardUpdated(standings.RoundNumber, standings.Rank(), context.Message.Timestamp));
    }
}
