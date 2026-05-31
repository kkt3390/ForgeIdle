using System.Text.Json;
using ForgeIdle.Data;

namespace ForgeIdle.Game;

public sealed class PlayerState
{
    public required string AccountName { get; init; }
    public long Gold { get; set; } = 5_000;
    public int WeaponLevel { get; set; }
    public int HighestWeaponLevel { get; set; }
    public int HighestBossDefeated { get; set; } = -1;
    public int ProtectionTickets { get; set; } = 3;
    public int Level { get; set; } = 1;
    public long Experience { get; set; }
    public PlayerStats Stats { get; set; } = new();
    public DateTimeOffset? AutomaticHuntCycleStartedAt { get; set; }
    public double AutomaticHuntUsedSeconds { get; set; }
    public HuntSession? Hunt { get; set; }
    public DateTimeOffset? LastManualHuntAt { get; set; }
    public List<string> RecentMessages { get; set; } = [];
}

public sealed record HuntSession(int AreaId, DateTimeOffset StartedAt, DateTimeOffset? RewardCapAt = null);

public sealed class PlayerStats
{
    public int DualWield { get; set; }
    public int GoldGain { get; set; }
    public int ExperienceGain { get; set; }
    public int ArtisanTouch { get; set; }

    public int SpentPoints => DualWield + GoldGain + ExperienceGain + ArtisanTouch;
}

public sealed class PlayerRepository(GameDbContext db)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string GetOrCreateSocialAccount(string provider, string externalId)
    {
        var existing = db.Accounts.SingleOrDefault(x =>
            x.Provider == provider && x.ExternalId == externalId);
        if (existing is not null)
        {
            return existing.AccountName;
        }

        var accountName = $"{provider}-{externalId}";
        var player = NewPlayer(accountName);
        var account = new Account
        {
            AccountName = accountName,
            Provider = provider,
            ExternalId = externalId,
            StateJson = JsonSerializer.Serialize(player, JsonOptions),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Accounts.Add(account);
        db.SaveChanges();
        return accountName;
    }

    public PlayerState GetRequired(string accountName)
    {
        var account = db.Accounts.Single(x => x.AccountName == accountName);
        return JsonSerializer.Deserialize<PlayerState>(account.StateJson, JsonOptions)
            ?? throw new InvalidOperationException("저장된 게임 상태를 읽을 수 없습니다.");
    }

    public void Save(PlayerState player)
    {
        var account = db.Accounts.Single(x => x.AccountName == player.AccountName);
        account.StateJson = JsonSerializer.Serialize(player, JsonOptions);
        account.UpdatedAt = DateTimeOffset.UtcNow;
        db.SaveChanges();
    }

    public static void AddMessage(PlayerState player, string message)
    {
        player.RecentMessages.Insert(0, message);
        if (player.RecentMessages.Count > 12)
        {
            player.RecentMessages.RemoveAt(player.RecentMessages.Count - 1);
        }
    }

    private static PlayerState NewPlayer(string accountName)
    {
        var player = new PlayerState { AccountName = accountName };
        AddMessage(player, "새로운 검을 받았습니다. 사냥을 시작해 골드를 모으세요.");
        return player;
    }

}
