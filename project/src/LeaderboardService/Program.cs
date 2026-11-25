using MassTransit;
using TradeArena.LeaderboardService;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<Standings>();

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<PortfolioUpdatedConsumer>();
    x.AddConsumer<RoundStartedConsumer>();
    x.AddConsumer<MarketTickConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMq:Host"] ?? "localhost", "/", h =>
        {
            h.Username(builder.Configuration["RabbitMq:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMq:Password"] ?? "guest");
        });

        // One queue handled one message at a time, so the in-memory standings need no locking
        // and the events of one round are applied in the order they arrived.
        cfg.ReceiveEndpoint("leaderboard", e =>
        {
            e.ConcurrentMessageLimit = 1;
            e.ConfigureConsumers(context);
        });
    });
});

builder.Build().Run();
