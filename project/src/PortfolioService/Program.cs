using MassTransit;
using Microsoft.EntityFrameworkCore;
using TradeArena.PortfolioService;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<PortfolioDb>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Portfolio")));
builder.Services.AddSingleton<PriceBook>();
builder.Services.AddSingleton<RoundState>();

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<MarketTickConsumer>();
    x.AddConsumer<RoundConsumer>();
    x.AddConsumer<PlayerJoinedConsumer>();
    x.AddConsumer<PlaceOrderConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMq:Host"] ?? "localhost", "/", h =>
        {
            h.Username(builder.Configuration["RabbitMq:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMq:Password"] ?? "guest");
        });

        // All messages for this service arrive in one queue and are handled one at a time.
        // Two orders of the same player can never run in parallel, and a new round is always
        // handled before the orders that were sent after it.
        cfg.ReceiveEndpoint("portfolio", e =>
        {
            e.ConcurrentMessageLimit = 1;
            e.UseMessageRetry(r => r.Intervals(200, 1000, 3000));
            e.UseInMemoryOutbox(context); // events are only published if the handler finished without error
            e.ConfigureConsumers(context);
        });
    });
});

var host = builder.Build();

// Create the database on the first start and continue from the last round we know about.
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PortfolioDb>();
    await db.Database.EnsureCreatedAsync();
    var lastRound = await db.Players.MaxAsync(p => (int?)p.RoundNumber) ?? 0;
    scope.ServiceProvider.GetRequiredService<RoundState>().Restore(lastRound);
}

host.Run();
