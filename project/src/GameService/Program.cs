using MassTransit;
using Microsoft.EntityFrameworkCore;
using TradeArena.Contracts;
using TradeArena.GameService;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<GameOptions>(builder.Configuration.GetSection("Game"));
builder.Services.AddDbContext<GameDb>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Game")));
builder.Services.AddSingleton<RoundClock>();
builder.Services.AddSingleton<LatestStandings>();

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<LeaderboardUpdatedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMq:Host"] ?? "localhost", "/", h =>
        {
            h.Username(builder.Configuration["RabbitMq:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMq:Password"] ?? "guest");
        });
        cfg.ReceiveEndpoint("game", e => e.ConfigureConsumers(context));
    });
});

builder.Services.AddHostedService<RoundTimer>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<GameDb>().Database.EnsureCreatedAsync();
}

// The current round, used by the Gateway when a browser opens the page.
app.MapGet("/rounds/current", (RoundClock clock) =>
    clock.Current is { } status ? Results.Ok(status) : Results.NotFound());

// Finished rounds in which somebody traded (at least one net worth moved away from the
// starting cash), newest first, with the top three of each.
app.MapGet("/rounds", async (GameDb db, int? take, CancellationToken ct) =>
    await db.Rounds
        .Where(r => r.EndedAt != null && r.Results.Any(x => x.NetWorth != GameRules.StartingCash))
        .OrderByDescending(r => r.Number)
        .Take(Math.Clamp(take ?? 10, 1, 50))
        .Select(r => new
        {
            r.Number,
            r.StartedAt,
            r.EndedAt,
            Players = r.Results.Count,
            Top = r.Results.OrderBy(x => x.Rank).ThenBy(x => x.Id).Take(3)
                           .Select(x => new { x.Rank, x.Nickname, x.NetWorth, x.ProfitPercent }),
        })
        .ToListAsync(ct));

app.Run();
