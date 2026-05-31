using ForgeIdle.Game;
using Microsoft.EntityFrameworkCore;

namespace ForgeIdle.Data;

public sealed class GameDbContext(DbContextOptions<GameDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(entity =>
        {
            entity.ToTable("accounts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AccountName).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => x.AccountName).IsUnique();
            entity.Property(x => x.Provider).HasMaxLength(20).IsRequired();
            entity.Property(x => x.ExternalId).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => new { x.Provider, x.ExternalId }).IsUnique();
            entity.Property(x => x.StateJson).HasColumnType("nvarchar(max)").IsRequired();
        });
    }
}

public sealed class Account
{
    public long Id { get; set; }
    public required string AccountName { get; set; }
    public required string Provider { get; set; }
    public required string ExternalId { get; set; }
    public required string StateJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
