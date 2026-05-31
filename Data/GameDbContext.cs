using ForgeIdle.Game;
using Microsoft.EntityFrameworkCore;

namespace ForgeIdle.Data;

public sealed class GameDbContext(DbContextOptions<GameDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<EnhancementAttempt> EnhancementAttempts => Set<EnhancementAttempt>();

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

        modelBuilder.Entity<EnhancementAttempt>(entity =>
        {
            entity.ToTable("enhancement_attempts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AccountName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Result).HasMaxLength(20).IsRequired();
            entity.HasIndex(x => x.AttemptedAt);
            entity.HasIndex(x => new { x.BeforeLevel, x.Result });
        });
    }

    public void EnsureEnhancementAttemptsTable() =>
        Database.ExecuteSqlRaw("""
            IF OBJECT_ID(N'dbo.enhancement_attempts', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.enhancement_attempts
                (
                    Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_enhancement_attempts PRIMARY KEY,
                    AccountName nvarchar(100) NOT NULL,
                    BeforeLevel int NOT NULL,
                    AfterLevel int NOT NULL,
                    Cost bigint NOT NULL,
                    AppliedSuccessRate float NOT NULL,
                    AppliedKeepRate float NOT NULL,
                    AppliedDestroyRate float NOT NULL,
                    Roll float NOT NULL,
                    UsedProtection bit NOT NULL,
                    Result nvarchar(20) NOT NULL,
                    AttemptedAt datetimeoffset NOT NULL
                );
                CREATE INDEX IX_enhancement_attempts_AttemptedAt
                    ON dbo.enhancement_attempts (AttemptedAt);
                CREATE INDEX IX_enhancement_attempts_BeforeLevel_Result
                    ON dbo.enhancement_attempts (BeforeLevel, Result);
            END
            """);
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

public sealed class EnhancementAttempt
{
    public long Id { get; set; }
    public required string AccountName { get; set; }
    public int BeforeLevel { get; set; }
    public int AfterLevel { get; set; }
    public long Cost { get; set; }
    public double AppliedSuccessRate { get; set; }
    public double AppliedKeepRate { get; set; }
    public double AppliedDestroyRate { get; set; }
    public double Roll { get; set; }
    public bool UsedProtection { get; set; }
    public required string Result { get; set; }
    public DateTimeOffset AttemptedAt { get; set; }
}
