using MassTransit;
using TradeArena.Contracts;

namespace TradeArena.MarketService;

// Runs in the background for the lifetime of the service.
// Every second it moves all prices and publishes one MarketTick event to RabbitMQ.
// MarketService doesn't know (or care) who listens - that's the point of publish/subscribe.
public class MarketSimulator(IBus bus, ILogger<MarketSimulator> logger) : BackgroundService
{
    private readonly Stock[] _stocks =
    [
        new("TECH", "TechCorp", 120m),
        new("FOOD", "Foodies Inc", 45m),
        new("AUTO", "AutoMotors", 80m),
        new("BANK", "Big Bank", 60m),
        new("GAME", "GameStudio", 25m),
    ];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var prices = _stocks.Select(s => s.Move()).ToArray();
            await bus.Publish(new MarketTick(prices, DateTime.UtcNow), stoppingToken);

            logger.LogInformation("Tick: {Prices}",
                string.Join("  ", prices.Select(p => $"{p.Symbol}={p.Price}")));
        }
    }
}
