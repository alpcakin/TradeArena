using MassTransit;
using Microsoft.AspNetCore.SignalR;
using TradeArena.Contracts;

namespace TradeArena.Gateway;

// Called by MassTransit every time a MarketTick arrives from RabbitMQ.
// It pushes the prices to every connected browser through SignalR.
public class MarketTickConsumer(IHubContext<MarketHub> hub) : IConsumer<MarketTick>
{
    public Task Consume(ConsumeContext<MarketTick> context) =>
        hub.Clients.All.SendAsync("marketTick", context.Message, context.CancellationToken);
}
