using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TradeArena.Contracts;

namespace TradeArena.GameService;

public class GameOptions
{
    public int RoundSeconds { get; set; } = 300;
    public int BreakSeconds { get; set; } = 20;
    public int StartDelaySeconds { get; set; } = 5;
}

// Runs the round lifecycle: Running (RoundSeconds) -> Break (BreakSeconds) -> next round.
public class RoundTimer(
    IServiceScopeFactory scopes,
    IBus bus,
    RoundClock clock,
    LatestStandings standings,
    IOptions<GameOptions> options,
    ILogger<RoundTimer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        var number = await CloseUnfinishedRoundsAsync(stoppingToken);

        // Give the other services a moment to create their queues before the first RoundStarted.
        await Task.Delay(TimeSpan.FromSeconds(settings.StartDelaySeconds), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            number++;
            var startedAt = DateTime.UtcNow;
            var endsAt = startedAt.AddSeconds(settings.RoundSeconds);

            await using (var scope = scopes.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<GameDb>();
                db.Rounds.Add(new Round { Number = number, StartedAt = startedAt, EndsAt = endsAt });
                await db.SaveChangesAsync(stoppingToken);
            }

            clock.Current = new RoundStatus(number, "Running", startedAt, endsAt, null);
            await bus.Publish(new RoundStarted(number, startedAt, endsAt), stoppingToken);
            logger.LogInformation("Round {Round} started, ends at {EndsAt:HH:mm:ss}", number, endsAt);

            await WaitUntil(endsAt, stoppingToken);

            var endedAt = DateTime.UtcNow;
            var nextRoundAt = endedAt.AddSeconds(settings.BreakSeconds);
            var finalStandings = standings.For(number);

            await using (var scope = scopes.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<GameDb>();
                var round = await db.Rounds.SingleAsync(r => r.Number == number, stoppingToken);
                round.EndedAt = endedAt;
                round.Results.AddRange(finalStandings.Select(s => new RoundResult
                {
                    Rank = s.Rank,
                    Nickname = s.Nickname,
                    NetWorth = s.NetWorth,
                    ProfitPercent = s.ProfitPercent,
                }));
                await db.SaveChangesAsync(stoppingToken);
            }

            clock.Current = new RoundStatus(number, "Break", startedAt, endsAt, nextRoundAt);
            await bus.Publish(new RoundEnded(number, endedAt, nextRoundAt, finalStandings), stoppingToken);
            logger.LogInformation("Round {Round} ended with {Players} players, winner: {Winner}",
                number, finalStandings.Length, finalStandings.FirstOrDefault()?.Nickname ?? "nobody");

            await WaitUntil(nextRoundAt, stoppingToken);
        }
    }

    // If the service was stopped in the middle of a round, that round is closed without results.
    // Round numbers keep growing, so the other services always see a new round as a new number.
    private async Task<int> CloseUnfinishedRoundsAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDb>();

        foreach (var round in await db.Rounds.Where(r => r.EndedAt == null).ToListAsync(ct))
            round.EndedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return await db.Rounds.MaxAsync(r => (int?)r.Number, ct) ?? 0;
    }

    private static Task WaitUntil(DateTime moment, CancellationToken ct)
    {
        var delay = moment - DateTime.UtcNow;
        return delay > TimeSpan.Zero ? Task.Delay(delay, ct) : Task.CompletedTask;
    }
}
