using MassTransit;
using TradeArena.Contracts;

namespace TradeArena.GameService;

public record RoundStatus(int Number, string Phase, DateTime StartedAt, DateTime EndsAt, DateTime? NextRoundAt);

// What the round timer is doing right now. Read by GET /rounds/current.
public class RoundClock
{
    private volatile RoundStatus? _current;

    public RoundStatus? Current
    {
        get => _current;
        set => _current = value;
    }
}

// The last ranking received from the Leaderboard Service. When the timer runs out,
// this is stored as the final result of the round.
public class LatestStandings
{
    private volatile LeaderboardUpdated? _latest;

    public void Set(LeaderboardUpdated update) => _latest = update;

    public LeaderboardEntry[] For(int roundNumber) =>
        _latest is { } latest && latest.RoundNumber == roundNumber ? latest.Entries : [];
}

public class LeaderboardUpdatedConsumer(LatestStandings standings) : IConsumer<LeaderboardUpdated>
{
    public Task Consume(ConsumeContext<LeaderboardUpdated> context)
    {
        standings.Set(context.Message);
        return Task.CompletedTask;
    }
}
