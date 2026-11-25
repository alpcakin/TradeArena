using Microsoft.EntityFrameworkCore;

namespace TradeArena.GameService;

public class Round
{
    public int Number { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime EndsAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public List<RoundResult> Results { get; set; } = [];
}

// One line of a round's final ranking.
public class RoundResult
{
    public int Id { get; set; }
    public int RoundNumber { get; set; }
    public int Rank { get; set; }
    public string Nickname { get; set; } = "";
    public decimal NetWorth { get; set; }
    public decimal ProfitPercent { get; set; }
}

// The Game Service's own database: the history of all rounds and their results.
public class GameDb(DbContextOptions<GameDb> options) : DbContext(options)
{
    public DbSet<Round> Rounds => Set<Round>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Round>(e =>
        {
            e.HasKey(r => r.Number);
            e.Property(r => r.Number).ValueGeneratedNever();
            e.HasMany(r => r.Results).WithOne().HasForeignKey(x => x.RoundNumber);
        });

        model.Entity<RoundResult>(e =>
        {
            e.Property(x => x.Nickname).HasMaxLength(16);
            e.Property(x => x.NetWorth).HasPrecision(18, 2);
            e.Property(x => x.ProfitPercent).HasPrecision(9, 2);
        });
    }
}
