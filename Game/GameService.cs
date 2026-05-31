using System.Security.Cryptography;

namespace ForgeIdle.Game;

public sealed class GameService(PlayerRepository players, GameCatalog catalog)
{
    private static readonly TimeSpan ManualHuntCooldown = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan BaseAutomaticHuntDuration = TimeSpan.FromHours(6);
    private static readonly TimeSpan AutomaticHuntDurationPerBoss = TimeSpan.FromMinutes(30);
    private static readonly TimeZoneInfo KoreaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time");

    public GameSnapshot GetPlayer(string name) => Snapshot(players.GetRequired(name));

    public GameActionResult StartHunt(string name, int areaId)
    {
        var player = players.GetRequired(name);
        NormalizeAutomaticHuntCycle(player, DateTimeOffset.UtcNow);
        if (player.Hunt is not null) return Failure(player, "이미 자동 사냥 중입니다.");

        var area = catalog.Areas.ElementAtOrDefault(areaId);
        if (area is null || !CanEnter(player, area)) return Failure(player, "아직 입장할 수 없는 사냥터입니다.");

        var now = DateTimeOffset.UtcNow;
        var remaining = RemainingAutomaticHuntDuration(player);
        if (remaining <= TimeSpan.Zero) return Failure(player, "오늘 사용할 수 있는 자동 사냥 시간을 모두 사용했습니다.");

        var rewardCapAt = Min(now + remaining, NextAutomaticHuntCycleStart(now));
        player.Hunt = new HuntSession(area.Id, now, rewardCapAt);
        PlayerRepository.AddMessage(player, $"{area.Name}에서 자동 사냥을 시작했습니다.");
        return SaveSuccess(player, "자동 사냥을 시작했습니다.");
    }

    public GameActionResult ClaimHunt(string name)
    {
        var player = players.GetRequired(name);
        if (player.Hunt is null) return Failure(player, "진행 중인 자동 사냥이 없습니다.");

        var reward = ClaimAutomaticHunt(player, DateTimeOffset.UtcNow);
        var message = $"자동 사냥 정산: {reward.Gold:N0} 골드, 경험치 {reward.Experience:N0}";
        PlayerRepository.AddMessage(player, message);
        return SaveSuccess(player, message);
    }

    public GameActionResult ManualHunt(string name)
    {
        var player = players.GetRequired(name);
        var now = DateTimeOffset.UtcNow;
        if (player.LastManualHuntAt is { } last && now - last < ManualHuntCooldown)
            return Failure(player, "아직 다음 몬스터를 찾는 중입니다.", last + ManualHuntCooldown);

        var claimed = player.Hunt is null ? new HuntReward(0, 0) : ClaimAutomaticHunt(player, now);
        var area = BestAvailableArea(player);
        var first = RollManualHunt(player, area);
        var dualWield = Roll(player.Stats.DualWield * .005);
        var second = dualWield ? RollManualHunt(player, area) : new ManualHuntReward("", 0, 0);
        var totalGold = first.Gold + second.Gold;
        var totalExp = first.Experience + second.Experience;

        player.Gold += totalGold;
        GrantExperience(player, totalExp);
        player.LastManualHuntAt = now;

        var prefix = claimed.Gold > 0 || claimed.Experience > 0
            ? $"자동 사냥 {claimed.Gold:N0} 골드, 경험치 {claimed.Experience:N0}를 먼저 정산했습니다. "
            : string.Empty;
        var dualMessage = dualWield ? $" 이도류 발동! {second.MonsterName}도 처치했습니다." : string.Empty;
        var message = $"{prefix}{first.MonsterName} 처치! {totalGold:N0} 골드, 경험치 {totalExp:N0} 획득.{dualMessage}";
        PlayerRepository.AddMessage(player, message);
        return SaveSuccess(player, message, now + ManualHuntCooldown);
    }

    public GameActionResult Enhance(string name, bool useProtection)
    {
        var player = players.GetRequired(name);
        if (player.Hunt is not null) return Failure(player, "자동 사냥을 종료하고 정산한 뒤 강화할 수 있습니다.");
        if (player.WeaponLevel >= catalog.Enhancements.Count) return Failure(player, "이미 최고 강화 단계입니다.");

        var baseRule = catalog.Enhancements[player.WeaponLevel];
        var rule = AdjustEnhancement(baseRule, player.Stats.ArtisanTouch);
        if (player.Gold < rule.Cost) return Failure(player, "골드가 부족합니다.");
        if (useProtection && rule.DestroyRate > 0 && player.ProtectionTickets <= 0)
            return Failure(player, "보호권이 없습니다.");

        player.Gold -= rule.Cost;
        var before = player.WeaponLevel;
        var roll = RandomNumberGenerator.GetInt32(0, 1_000_000) / 1_000_000d;
        string message;
        string result;
        if (roll < rule.SuccessRate)
        {
            player.WeaponLevel++;
            player.HighestWeaponLevel = Math.Max(player.HighestWeaponLevel, player.WeaponLevel);
            message = $"+{before} → +{player.WeaponLevel} 강화에 성공했습니다!";
            result = "Success";
        }
        else if (roll < rule.SuccessRate + rule.KeepRate)
        {
            message = $"+{before} 강화에 실패했습니다. 무기는 유지됩니다.";
            result = "Keep";
        }
        else if (useProtection)
        {
            player.ProtectionTickets--;
            message = $"파괴 위기를 보호권으로 막았습니다. +{before} 무기를 유지합니다.";
            result = "Protected";
        }
        else
        {
            player.WeaponLevel = 12;
            message = "무기가 파괴되어 +12로 복구되었습니다.";
            result = "Destroyed";
        }

        players.AddEnhancementAttempt(
            player, before, rule.Cost, rule.SuccessRate, rule.KeepRate, rule.DestroyRate,
            roll, useProtection, result);
        PlayerRepository.AddMessage(player, message);
        return SaveSuccess(player, message);
    }

    public GameActionResult ChallengeBoss(string name)
    {
        var player = players.GetRequired(name);
        if (player.Hunt is not null) return Failure(player, "자동 사냥을 종료한 뒤 보스에게 도전할 수 있습니다.");

        var areaId = player.HighestBossDefeated + 1;
        var area = catalog.Areas.ElementAtOrDefault(areaId);
        if (area?.BossRequiredEnhancement is null) return Failure(player, "도전 가능한 다음 보스가 없습니다.");
        if (player.WeaponLevel < area.BossRequiredEnhancement)
            return Failure(player, $"보스 도전에는 +{area.BossRequiredEnhancement} 무기가 필요합니다.");

        player.HighestBossDefeated = areaId;
        var nextArea = catalog.Areas[areaId + 1];
        var message = $"보스를 처치했습니다! {nextArea.Name}이 해금되었습니다.";
        PlayerRepository.AddMessage(player, message);
        return SaveSuccess(player, message);
    }

    public GameActionResult InvestStat(string name, string stat)
    {
        var player = players.GetRequired(name);
        if (AvailableStatPoints(player) <= 0) return Failure(player, "사용 가능한 스탯 포인트가 없습니다.");

        var current = stat switch
        {
            "dualWield" => player.Stats.DualWield,
            "goldGain" => player.Stats.GoldGain,
            "experienceGain" => player.Stats.ExperienceGain,
            "artisanTouch" => player.Stats.ArtisanTouch,
            _ => -1
        };
        if (current < 0) return Failure(player, "알 수 없는 스탯입니다.");
        if (current >= GameCatalog.MaxStatLevel) return Failure(player, "이미 최대 레벨인 스탯입니다.");

        switch (stat)
        {
            case "dualWield": player.Stats.DualWield++; break;
            case "goldGain": player.Stats.GoldGain++; break;
            case "experienceGain": player.Stats.ExperienceGain++; break;
            case "artisanTouch": player.Stats.ArtisanTouch++; break;
        }

        return SaveSuccess(player, "스탯 포인트를 투자했습니다.");
    }

    public GameActionResult ResetStats(string name)
    {
        var player = players.GetRequired(name);
        var cost = StatResetCost(player);
        if (player.Stats.SpentPoints == 0) return Failure(player, "초기화할 스탯이 없습니다.");
        if (player.Gold < cost) return Failure(player, "스탯 초기화에 필요한 골드가 부족합니다.");

        player.Gold -= cost;
        player.Stats = new PlayerStats();
        return SaveSuccess(player, $"스탯을 초기화했습니다. {cost:N0} 골드를 사용했습니다.");
    }

    private GameActionResult SaveSuccess(PlayerState player, string message, DateTimeOffset? manualHuntAvailableAt = null)
    {
        players.Save(player);
        return new(true, message, Snapshot(player), manualHuntAvailableAt);
    }

    private GameActionResult Failure(PlayerState player, string message, DateTimeOffset? manualHuntAvailableAt = null) =>
        new(false, message, Snapshot(player), manualHuntAvailableAt);

    private GameSnapshot Snapshot(PlayerState player)
    {
        NormalizeAutomaticHuntCycle(player, DateTimeOffset.UtcNow);
        var availableAreas = catalog.Areas
            .Where(area => area.Id <= player.HighestBossDefeated + 1)
            .Select(area => new AreaSnapshot(area.Id, area.Name, area.RequiredEnhancement, area.GoldPerHour, area.ExperiencePerHour, CanEnter(player, area)))
            .ToArray();
        var adjustedRule = player.WeaponLevel < catalog.Enhancements.Count
            ? AdjustEnhancement(catalog.Enhancements[player.WeaponLevel], player.Stats.ArtisanTouch)
            : null;
        var nextBossArea = catalog.Areas.ElementAtOrDefault(player.HighestBossDefeated + 1);
        var bestArea = BestAvailableArea(player);

        return new GameSnapshot(
            player.AccountName, player.Nickname, player.Gold, player.WeaponLevel, player.HighestWeaponLevel,
            catalog.AttackPower(player.WeaponLevel), player.ProtectionTickets,
            player.Level, player.Experience, catalog.RequiredExperience(player.Level),
            AvailableStatPoints(player), player.Stats, StatResetCost(player),
            new AutomaticHuntBudgetSnapshot(
                AutomaticHuntLimit(player).TotalHours,
                RemainingAutomaticHuntDuration(player).TotalHours,
                NextAutomaticHuntCycleStart(DateTimeOffset.UtcNow)),
            player.Hunt is null ? null : new HuntSnapshot(
                player.Hunt.AreaId,
                catalog.Areas[player.Hunt.AreaId].Name,
                player.Hunt.StartedAt,
                HuntRewardCapAt(player)),
            new ManualHuntSnapshot(bestArea.Name, bestArea.GoldPerHour, bestArea.ExperiencePerHour, player.LastManualHuntAt is null ? null : player.LastManualHuntAt + ManualHuntCooldown),
            adjustedRule,
            nextBossArea?.BossRequiredEnhancement is null ? null : new BossSnapshot($"{nextBossArea.Name}의 보스", nextBossArea.BossRequiredEnhancement.Value, nextBossArea.BossHealth!.Value, player.WeaponLevel >= nextBossArea.BossRequiredEnhancement),
            availableAreas, player.RecentMessages);
    }

    private HuntReward ClaimAutomaticHunt(PlayerState player, DateTimeOffset now)
    {
        var hunt = player.Hunt!;
        var area = catalog.Areas[hunt.AreaId];
        var duration = Min(now, HuntRewardCapAt(player)) - hunt.StartedAt;
        if (duration < TimeSpan.Zero) duration = TimeSpan.Zero;
        var gold = (long)Math.Floor(area.GoldPerHour * duration.TotalHours * GoldMultiplier(player));
        var experience = (long)Math.Floor(area.ExperiencePerHour * duration.TotalHours * ExperienceMultiplier(player));
        player.AutomaticHuntUsedSeconds += duration.TotalSeconds;
        player.Gold += gold;
        GrantExperience(player, experience);
        player.Hunt = null;
        return new(gold, experience);
    }

    private ManualHuntReward RollManualHunt(PlayerState player, HuntingArea area)
    {
        const double actionsPerHour = 1_200;
        var variance = .8 + RandomNumberGenerator.GetInt32(0, 400_001) / 1_000_000d;
        var roll = RandomNumberGenerator.GetInt32(0, 1_000);
        var monsterMultiplier = roll == 0 ? 30 : roll < 21 ? 5 : 1;
        var monsterName = monsterMultiplier switch { 30 => "황금 몬스터", 5 => "정예 몬스터", _ => "몬스터" };
        var gold = Math.Max(1, (long)Math.Round(area.GoldPerHour * 1.5 / actionsPerHour * variance * monsterMultiplier * GoldMultiplier(player)));
        var experience = Math.Max(1, (long)Math.Round(area.ExperiencePerHour * 1.25 / actionsPerHour * variance * monsterMultiplier * ExperienceMultiplier(player)));
        return new(monsterName, gold, experience);
    }

    private void GrantExperience(PlayerState player, long amount)
    {
        player.Experience += amount;
        while (player.Level < GameCatalog.MaxPlayerLevel)
        {
            var required = catalog.RequiredExperience(player.Level);
            if (player.Experience < required) break;
            player.Experience -= required;
            player.Level++;
            PlayerRepository.AddMessage(player, $"레벨 {player.Level} 달성! 스탯 포인트를 획득했습니다.");
        }
    }

    private static EnhancementRule AdjustEnhancement(EnhancementRule rule, int artisanTouch)
    {
        var success = Math.Min(1 - rule.DestroyRate, rule.SuccessRate * (1 + artisanTouch * .005));
        return rule with { SuccessRate = success, KeepRate = 1 - success - rule.DestroyRate };
    }

    private static double GoldMultiplier(PlayerState player) => 1 + player.Stats.GoldGain * .01;
    private static double ExperienceMultiplier(PlayerState player) => 1 + player.Stats.ExperienceGain * .01;
    private static int AvailableStatPoints(PlayerState player) => Math.Max(0, player.Level - 1 - player.Stats.SpentPoints);
    private static long StatResetCost(PlayerState player) => player.Level * 10_000L;
    private static bool Roll(double chance) => RandomNumberGenerator.GetInt32(0, 1_000_000) < chance * 1_000_000;
    private bool CanEnter(PlayerState player, HuntingArea area) => area.Id <= player.HighestBossDefeated + 1 && player.WeaponLevel >= area.RequiredEnhancement;
    private HuntingArea BestAvailableArea(PlayerState player) => catalog.Areas.Last(area => CanEnter(player, area));

    private static TimeSpan AutomaticHuntLimit(PlayerState player) =>
        BaseAutomaticHuntDuration + TimeSpan.FromTicks(AutomaticHuntDurationPerBoss.Ticks * Math.Max(0, player.HighestBossDefeated + 1));

    private static TimeSpan RemainingAutomaticHuntDuration(PlayerState player) =>
        TimeSpan.FromSeconds(Math.Max(0, AutomaticHuntLimit(player).TotalSeconds - player.AutomaticHuntUsedSeconds));

    private static void NormalizeAutomaticHuntCycle(PlayerState player, DateTimeOffset now)
    {
        var cycleStart = CurrentAutomaticHuntCycleStart(now);
        if (player.AutomaticHuntCycleStartedAt is null || player.AutomaticHuntCycleStartedAt < cycleStart)
        {
            player.AutomaticHuntCycleStartedAt = cycleStart;
            player.AutomaticHuntUsedSeconds = 0;
            if (player.Hunt is not null && HuntRewardCapAt(player) <= cycleStart)
                player.Hunt = null;
        }
    }

    private static DateTimeOffset HuntRewardCapAt(PlayerState player) =>
        player.Hunt!.RewardCapAt ?? Min(
            player.Hunt.StartedAt + RemainingAutomaticHuntDuration(player),
            NextAutomaticHuntCycleStart(player.Hunt.StartedAt));

    private static DateTimeOffset CurrentAutomaticHuntCycleStart(DateTimeOffset now)
    {
        var koreaNow = TimeZoneInfo.ConvertTime(now, KoreaTimeZone);
        var localStart = koreaNow.Date;
        return new DateTimeOffset(localStart, KoreaTimeZone.GetUtcOffset(localStart)).ToUniversalTime();
    }

    private static DateTimeOffset NextAutomaticHuntCycleStart(DateTimeOffset now) =>
        CurrentAutomaticHuntCycleStart(now).AddDays(1);

    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right) => left <= right ? left : right;
}

public sealed record StartHuntRequest(int AreaId);
public sealed record EnhanceRequest(bool UseProtection);
public sealed record InvestStatRequest(string Stat);
public sealed record GameActionResult(bool Ok, string Message, GameSnapshot State, DateTimeOffset? ManualHuntAvailableAt = null);
public sealed record HuntReward(long Gold, long Experience);
public sealed record ManualHuntReward(string MonsterName, long Gold, long Experience);
public sealed record AreaSnapshot(int Id, string Name, int RequiredEnhancement, long GoldPerHour, long ExperiencePerHour, bool CanEnter);
public sealed record AutomaticHuntBudgetSnapshot(double LimitHours, double RemainingHours, DateTimeOffset ResetsAt);
public sealed record HuntSnapshot(int AreaId, string AreaName, DateTimeOffset StartedAt, DateTimeOffset RewardCapAt);
public sealed record ManualHuntSnapshot(string AreaName, long AutomaticGoldPerHour, long AutomaticExperiencePerHour, DateTimeOffset? AvailableAt);
public sealed record BossSnapshot(string Name, int RequiredEnhancement, int Health, bool CanChallenge);
public sealed record GameSnapshot(
    string AccountName, string? Nickname, long Gold, int WeaponLevel, int HighestWeaponLevel, int AttackPower, int ProtectionTickets,
    int Level, long Experience, long RequiredExperience, int AvailableStatPoints, PlayerStats Stats, long StatResetCost,
    AutomaticHuntBudgetSnapshot AutomaticHuntBudget, HuntSnapshot? Hunt, ManualHuntSnapshot ManualHunt, EnhancementRule? CurrentEnhancement, BossSnapshot? NextBoss,
    IReadOnlyList<AreaSnapshot> AvailableAreas, IReadOnlyList<string> RecentMessages);
