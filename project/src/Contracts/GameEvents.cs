namespace TradeArena.Contracts;

public static class GameRules
{
    // Every player starts every round with this much cash and no shares.
    public const decimal StartingCash = 10_000m;
}

public record LeaderboardEntry(int Rank, string Nickname, decimal NetWorth, decimal ProfitPercent);

// Published by the Leaderboard Service once per market tick.
public record LeaderboardUpdated(int RoundNumber, LeaderboardEntry[] Entries, DateTime Timestamp);

// Published by the Game Service when a round begins and when its timer runs out.
public record RoundStarted(int RoundNumber, DateTime StartedAt, DateTime EndsAt);

public record RoundEnded(int RoundNumber, DateTime EndedAt, DateTime NextRoundAt, LeaderboardEntry[] FinalStandings);
