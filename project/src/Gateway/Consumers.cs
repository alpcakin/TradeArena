using MassTransit;
using Microsoft.AspNetCore.SignalR;
using TradeArena.Contracts;

namespace TradeArena.Gateway;

// Events that every player sees: prices, the leaderboard and the round timer.
public class BroadcastConsumer(IHubContext<ArenaHub> hub) :
    IConsumer<MarketTick>,
    IConsumer<LeaderboardUpdated>,
    IConsumer<RoundStarted>,
    IConsumer<RoundEnded>
{
    public Task Consume(ConsumeContext<MarketTick> context) =>
        hub.Clients.All.SendAsync("marketTick", context.Message, context.CancellationToken);

    public Task Consume(ConsumeContext<LeaderboardUpdated> context) =>
        hub.Clients.All.SendAsync("leaderboardUpdated", context.Message, context.CancellationToken);

    public Task Consume(ConsumeContext<RoundStarted> context) =>
        hub.Clients.All.SendAsync("roundStarted", context.Message, context.CancellationToken);

    public Task Consume(ConsumeContext<RoundEnded> context) =>
        hub.Clients.All.SendAsync("roundEnded", context.Message, context.CancellationToken);
}

// Events that concern one player only. They go to that player's group, not to everyone.
public class PlayerEventsConsumer(IHubContext<ArenaHub> hub) :
    IConsumer<OrderExecuted>,
    IConsumer<OrderRejected>,
    IConsumer<PortfolioUpdated>
{
    public Task Consume(ConsumeContext<OrderExecuted> context) =>
        Player(context.Message.PlayerId).SendAsync("orderExecuted", context.Message, context.CancellationToken);

    public Task Consume(ConsumeContext<OrderRejected> context) =>
        Player(context.Message.PlayerId).SendAsync("orderRejected", context.Message, context.CancellationToken);

    public Task Consume(ConsumeContext<PortfolioUpdated> context) =>
        Player(context.Message.PlayerId).SendAsync("portfolioUpdated", context.Message, context.CancellationToken);

    private IClientProxy Player(Guid playerId) => hub.Clients.Group(ArenaHub.PlayerGroup(playerId));
}
