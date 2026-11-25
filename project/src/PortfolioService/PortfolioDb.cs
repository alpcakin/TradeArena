using Microsoft.EntityFrameworkCore;

namespace TradeArena.PortfolioService;

// The Portfolio Service's own database. No other service reads or writes it.
public class PortfolioDb(DbContextOptions<PortfolioDb> options) : DbContext(options)
{
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Trade> Trades => Set<Trade>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Player>(e =>
        {
            e.Property(p => p.Nickname).HasMaxLength(16);
            e.Property(p => p.Cash).HasPrecision(18, 2);
            e.HasMany(p => p.Positions).WithOne().HasForeignKey(p => p.PlayerId);
        });

        model.Entity<Position>(e =>
        {
            e.HasKey(p => new { p.PlayerId, p.Symbol });
            e.Property(p => p.Symbol).HasMaxLength(8);
        });

        model.Entity<Trade>(e =>
        {
            e.HasKey(t => t.OrderId);
            e.HasOne<Player>().WithMany().HasForeignKey(t => t.PlayerId);
            e.Property(t => t.Symbol).HasMaxLength(8);
            e.Property(t => t.Side).HasConversion<string>().HasMaxLength(4);
            e.Property(t => t.Price).HasPrecision(18, 2);
            e.Property(t => t.Total).HasPrecision(18, 2);
        });
    }
}
