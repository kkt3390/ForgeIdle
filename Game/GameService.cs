using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace EnhanceAddiction.WebForms.Game
{
    public sealed class GameService
    {
        private static readonly TimeSpan ManualHuntCooldown = TimeSpan.FromSeconds(1); 
        private static readonly TimeSpan BaseAutomaticHuntDuration = TimeSpan.FromHours(6);
        private static readonly TimeSpan AutomaticHuntDurationPerBoss = TimeSpan.FromMinutes(30);
        private static readonly TimeZoneInfo KoreaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time");
        private readonly GameCatalog _catalog;

        // 게임 규칙 카탈로그를 받아 서비스에서 재사용합니다.
        public GameService(GameCatalog catalog)
        {
            _catalog = catalog;
        }

        // 브라우저에 필요한 값만 골라 반환합니다.
        // 화면 항목을 추가할 때는 이 객체와 Scripts/game.js를 함께 수정하세요.
        public object Snapshot(PlayerState player)
        {
            NormalizePlayer(player);
            NormalizeAutomaticHuntCycle(player, DateTime.UtcNow);
            var availableAreas = _catalog.Areas
                .Where(area => area.Id <= player.HighestBossDefeated + 1)
                .Select(area => new
                {
                    id = area.Id,
                    name = area.Name,
                    requiredEnhancement = area.RequiredEnhancement,
                    goldPerHour = area.GoldPerHour,
                    experiencePerHour = area.ExperiencePerHour,
                    canEnter = CanEnter(player, area)
                }).ToArray();
            var adjustedRule = player.WeaponLevel < _catalog.Enhancements.Count
                ? AdjustEnhancement(_catalog.Enhancements[player.WeaponLevel], player.Stats.ArtisanTouch)
                : null;
            var nextBossArea = _catalog.Areas.ElementAtOrDefault(player.HighestBossDefeated + 1);
            var bestArea = BestAvailableArea(player);
            return new
            {
                nickname = player.Nickname,
                gold = player.Gold,
                weaponLevel = player.WeaponLevel,
                highestWeaponLevel = player.HighestWeaponLevel,
                attackPower = _catalog.AttackPower(player.WeaponLevel),
                protectionTickets = player.ProtectionTickets,
                level = player.Level,
                experience = player.Experience,
                requiredExperience = _catalog.RequiredExperience(player.Level),
                availableStatPoints = AvailableStatPoints(player),
                stats = new
                {
                    dualWield = player.Stats.DualWield,
                    goldGain = player.Stats.GoldGain,
                    experienceGain = player.Stats.ExperienceGain,
                    artisanTouch = player.Stats.ArtisanTouch
                },
                statResetCost = StatResetCost(player),
                automaticHuntBudget = new
                {
                    limitHours = AutomaticHuntLimit(player).TotalHours,
                    remainingHours = RemainingAutomaticHuntDuration(player).TotalHours,
                    resetsAt = Iso(NextAutomaticHuntCycleStart(DateTime.UtcNow))
                },
                hunt = player.Hunt == null ? null : new
                {
                    areaId = player.Hunt.AreaId,
                    areaName = _catalog.Areas[player.Hunt.AreaId].Name,
                    startedAt = Iso(player.Hunt.StartedAtUtc),
                    rewardCapAt = Iso(player.Hunt.RewardCapAtUtc)
                },
                manualHunt = new
                {
                    areaName = bestArea.Name,
                    automaticGoldPerHour = bestArea.GoldPerHour,
                    automaticExperiencePerHour = bestArea.ExperiencePerHour,
                    availableAt = player.LastManualHuntAtUtc.HasValue
                        ? Iso(player.LastManualHuntAtUtc.Value.Add(ManualHuntCooldown))
                        : null
                },
                currentEnhancement = adjustedRule == null ? null : RuleSnapshot(adjustedRule),
                nextBoss = nextBossArea == null || !nextBossArea.BossRequiredEnhancement.HasValue ? null : new
                {
                    name = nextBossArea.Name + "의 보스",
                    requiredEnhancement = nextBossArea.BossRequiredEnhancement.Value,
                    health = nextBossArea.BossHealth.Value,
                    canChallenge = player.WeaponLevel >= nextBossArea.BossRequiredEnhancement.Value
                },
                availableAreas = availableAreas,
                recentMessages = player.RecentMessages
            };
        }

        // 브라우저에 공개할 사냥터와 강화 규칙 목록을 반환합니다.
        public object CatalogSnapshot()
        {
            return new
            {
                areas = _catalog.Areas.Select(area => new
                {
                    id = area.Id,
                    name = area.Name,
                    requiredEnhancement = area.RequiredEnhancement,
                    goldPerHour = area.GoldPerHour,
                    experiencePerHour = area.ExperiencePerHour,
                    bossRequiredEnhancement = area.BossRequiredEnhancement,
                    bossHealth = area.BossHealth
                }).ToArray(),
                enhancements = _catalog.Enhancements.Select(RuleSnapshot).ToArray()
            };
        }

        // 선택한 사냥터에서 오늘 남은 시간만큼 자동 사냥을 시작합니다.
        public GameResult StartAutomaticHunt(PlayerState player, int areaId)
        {
            NormalizeAutomaticHuntCycle(player, DateTime.UtcNow);
            if (player.Hunt != null) return Failure(player, "이미 자동 사냥 중입니다.");
            var area = _catalog.Areas.ElementAtOrDefault(areaId);
            if (area == null || !CanEnter(player, area)) return Failure(player, "아직 입장할 수 없는 사냥터입니다.");
            var remaining = RemainingAutomaticHuntDuration(player);
            if (remaining <= TimeSpan.Zero) return Failure(player, "오늘 사용할 수 있는 자동 사냥 시간을 모두 사용했습니다.");

            var now = DateTime.UtcNow;
            player.Hunt = new HuntSession
            {
                AreaId = area.Id,
                StartedAtUtc = now,
                RewardCapAtUtc = Min(now.Add(remaining), NextAutomaticHuntCycleStart(now))
            };
            AddMessage(player, area.Name + "에서 자동 사냥을 시작했습니다.");
            return Success(player, "자동 사냥을 시작했습니다.", new { areaId = area.Id });
        }

        // 진행 중인 자동 사냥을 종료하고 누적 보상을 정산합니다.
        public GameResult ClaimAutomaticHunt(PlayerState player)
        {
            if (player.Hunt == null) return Failure(player, "진행 중인 자동 사냥이 없습니다.");
            var reward = ClaimAutomaticHuntReward(player, DateTime.UtcNow);
            var message = string.Format("자동 사냥 정산: {0:N0} 골드, 경험치 {1:N0}", reward.Gold, reward.Experience);
            AddMessage(player, message);
            return Success(player, message, reward);
        }

        // 직접 사냥 1회를 처리하고 자동 사냥 중이었다면 먼저 정산합니다.
        public GameResult ManualHunt(PlayerState player)
        {
            var now = DateTime.UtcNow;
            if (player.LastManualHuntAtUtc.HasValue && now - player.LastManualHuntAtUtc.Value < ManualHuntCooldown)
                return Failure(player, "아직 다음 몬스터를 찾는 중입니다.");

            var claimed = player.Hunt == null ? new HuntReward(0, 0) : ClaimAutomaticHuntReward(player, now);
            var area = BestAvailableArea(player);
            var first = RollManualHunt(player, area);
            var dualWield = Roll(player.Stats.DualWield * .005);
            var second = dualWield ? RollManualHunt(player, area) : new ManualHuntReward("", 0, 0);
            var totalGold = first.Gold + second.Gold;
            var totalExperience = first.Experience + second.Experience;
            player.Gold += totalGold;
            GrantExperience(player, totalExperience);
            player.LastManualHuntAtUtc = now;

            var prefix = claimed.Gold > 0 || claimed.Experience > 0
                ? string.Format("자동 사냥 {0:N0} 골드, 경험치 {1:N0}를 먼저 정산했습니다. ", claimed.Gold, claimed.Experience)
                : "";
            var dualMessage = dualWield ? " 이도류 발동! " + second.MonsterName + "도 처치했습니다." : "";
            var message = string.Format("{0}{1} 처치! {2:N0} 골드, 경험치 {3:N0} 획득.{4}",
                prefix, first.MonsterName, totalGold, totalExperience, dualMessage);
            AddMessage(player, message);
            return Success(player, message, new { claimed = claimed, first = first, second = second, dualWield = dualWield });
        }

        // 골드를 소모해 강화를 시도하고 성공·유지·보호·파괴 결과를 처리합니다.
        public GameResult Enhance(PlayerState player, bool useProtection)
        {
            if (player.Hunt != null) return Failure(player, "자동 사냥을 종료하고 정산한 뒤 강화할 수 있습니다.");
            if (player.WeaponLevel >= _catalog.Enhancements.Count) return Failure(player, "이미 최고 강화 단계입니다.");
            var rule = AdjustEnhancement(_catalog.Enhancements[player.WeaponLevel], player.Stats.ArtisanTouch);
            if (player.Gold < rule.Cost) return Failure(player, "골드가 부족합니다.");
            if (useProtection && rule.DestroyRate > 0 && player.ProtectionTickets <= 0)
                return Failure(player, "보호권이 없습니다.");

            player.Gold -= rule.Cost;
            var before = player.WeaponLevel;
            var roll = RandomDouble();
            string message;
            string result;
            if (roll < rule.SuccessRate)
            {
                player.WeaponLevel++;
                player.HighestWeaponLevel = Math.Max(player.HighestWeaponLevel, player.WeaponLevel);
                message = string.Format("+{0} → +{1} 강화에 성공했습니다!", before, player.WeaponLevel);
                result = "Success";
            }
            else if (roll < rule.SuccessRate + rule.KeepRate)
            {
                message = string.Format("+{0} 강화에 실패했습니다. 무기는 유지됩니다.", before);
                result = "Keep";
            }
            else if (useProtection)
            {
                player.ProtectionTickets--;
                message = string.Format("파괴 위기를 보호권으로 막았습니다. +{0} 무기를 유지합니다.", before);
                result = "Protected";
            }
            else
            {
                player.WeaponLevel = 12;
                message = "무기가 파괴되어 +12로 복구되었습니다.";
                result = "Destroyed";
            }
            AddMessage(player, message);
            var response = Success(player, message, null);
            response.EnhancementAttempt = new EnhancementAttemptLog
            {
                BeforeLevel = before,
                AfterLevel = player.WeaponLevel,
                Cost = rule.Cost,
                SuccessRate = rule.SuccessRate,
                KeepRate = rule.KeepRate,
                DestroyRate = rule.DestroyRate,
                Roll = roll,
                UsedProtection = useProtection,
                Result = result
            };
            return response;
        }

        // 강화 조건을 만족하면 다음 관문 보스를 처치하고 사냥터를 해금합니다.
        public GameResult ChallengeBoss(PlayerState player)
        {
            if (player.Hunt != null) return Failure(player, "자동 사냥을 종료한 뒤 보스에게 도전할 수 있습니다.");
            var areaId = player.HighestBossDefeated + 1;
            var area = _catalog.Areas.ElementAtOrDefault(areaId);
            if (area == null || !area.BossRequiredEnhancement.HasValue) return Failure(player, "도전 가능한 다음 보스가 없습니다.");
            if (player.WeaponLevel < area.BossRequiredEnhancement.Value)
                return Failure(player, string.Format("보스 도전에는 +{0} 무기가 필요합니다.", area.BossRequiredEnhancement.Value));
            player.HighestBossDefeated = areaId;
            var nextArea = _catalog.Areas[areaId + 1];
            var message = "보스를 처치했습니다! " + nextArea.Name + "이 해금되었습니다.";
            AddMessage(player, message);
            return Success(player, message, new { areaId = areaId });
        }

        // 남은 스탯 포인트를 선택한 스탯에 1만큼 투자합니다.
        public GameResult InvestStat(PlayerState player, string stat)
        {
            if (AvailableStatPoints(player) <= 0) return Failure(player, "사용 가능한 스탯 포인트가 없습니다.");
            int current;
            switch (stat)
            {
                case "dualWield": current = player.Stats.DualWield; break;
                case "goldGain": current = player.Stats.GoldGain; break;
                case "experienceGain": current = player.Stats.ExperienceGain; break;
                case "artisanTouch": current = player.Stats.ArtisanTouch; break;
                default: return Failure(player, "알 수 없는 스탯입니다.");
            }
            if (current >= GameCatalog.MaxStatLevel) return Failure(player, "이미 최대 레벨인 스탯입니다.");
            switch (stat)
            {
                case "dualWield": player.Stats.DualWield++; break;
                case "goldGain": player.Stats.GoldGain++; break;
                case "experienceGain": player.Stats.ExperienceGain++; break;
                case "artisanTouch": player.Stats.ArtisanTouch++; break;
            }
            return Success(player, "스탯 포인트를 투자했습니다.", new { stat = stat });
        }

        // 골드를 소모해 투자한 스탯을 모두 초기화합니다.
        public GameResult ResetStats(PlayerState player)
        {
            var cost = StatResetCost(player);
            if (player.Stats.SpentPoints == 0) return Failure(player, "초기화할 스탯이 없습니다.");
            if (player.Gold < cost) return Failure(player, "스탯 초기화에 필요한 골드가 부족합니다.");
            player.Gold -= cost;
            player.Stats = new PlayerStats();
            return Success(player, string.Format("스탯을 초기화했습니다. {0:N0} 골드를 사용했습니다.", cost), new { cost = cost });
        }

        // 성공 결과와 갱신된 사용자 상태를 공통 응답 형식으로 만듭니다.
        private GameResult Success(PlayerState player, string message, object details)
        {
            return new GameResult { Ok = true, Message = message, State = Snapshot(player), Details = details };
        }

        // 실패 결과와 현재 사용자 상태를 공통 응답 형식으로 만듭니다.
        private GameResult Failure(PlayerState player, string message)
        {
            return new GameResult { Ok = false, Message = message, State = Snapshot(player) };
        }

        // 자동 사냥 경과 시간에 비례한 골드와 경험치를 실제 상태에 반영합니다.
        private HuntReward ClaimAutomaticHuntReward(PlayerState player, DateTime now)
        {
            var hunt = player.Hunt;
            var area = _catalog.Areas[hunt.AreaId];
            var duration = Min(now, hunt.RewardCapAtUtc) - hunt.StartedAtUtc;
            if (duration < TimeSpan.Zero) duration = TimeSpan.Zero;
            var gold = (long)Math.Floor(area.GoldPerHour * duration.TotalHours * GoldMultiplier(player));
            var experience = (long)Math.Floor(area.ExperiencePerHour * duration.TotalHours * ExperienceMultiplier(player));
            player.AutomaticHuntUsedSeconds += duration.TotalSeconds;
            player.Gold += gold;
            GrantExperience(player, experience);
            player.Hunt = null;
            return new HuntReward(gold, experience);
        }

        // 직접 사냥의 일반·정예·황금 몬스터 판정과 보상을 계산합니다.
        private ManualHuntReward RollManualHunt(PlayerState player, HuntingArea area)
        {
            const double actionsPerHour = 1200;
            var variance = .8 + RandomInt(0, 400001) / 1000000d;
            var roll = RandomInt(0, 1000);
            var multiplier = roll == 0 ? 30 : roll < 21 ? 5 : 1;
            var name = multiplier == 30 ? "황금 몬스터" : multiplier == 5 ? "정예 몬스터" : "몬스터";
            var gold = Math.Max(
                1,
                (long)Math.Round(area.GoldPerHour * 1.5 / actionsPerHour * variance * multiplier * GoldMultiplier(player)));
            var experience = Math.Max(
                1,
                (long)Math.Round(area.ExperiencePerHour * 1.25 / actionsPerHour * variance * multiplier * ExperienceMultiplier(player)));
            return new ManualHuntReward(name, gold, experience);
        }

        // 경험치를 지급하고 필요한 만큼 연속 레벨업을 처리합니다.
        private void GrantExperience(PlayerState player, long amount)
        {
            player.Experience += amount;
            while (player.Level < GameCatalog.MaxPlayerLevel)
            {
                var required = _catalog.RequiredExperience(player.Level);
                if (player.Experience < required) break;
                player.Experience -= required;
                player.Level++;
                AddMessage(player, string.Format("레벨 {0} 달성! 스탯 포인트를 획득했습니다.", player.Level));
            }
        }

        // 장인의 손길 스탯을 적용한 강화 성공률을 계산합니다.
        private static EnhancementRule AdjustEnhancement(EnhancementRule rule, int artisanTouch)
        {
            var success = Math.Min(1 - rule.DestroyRate, rule.SuccessRate * (1 + artisanTouch * .005));
            return new EnhancementRule(rule.CurrentLevel, rule.Cost, success, 1 - success - rule.DestroyRate, rule.DestroyRate);
        }

        // 강화 규칙을 브라우저에 전달할 간단한 객체로 바꿉니다.
        private static object RuleSnapshot(EnhancementRule rule)
        {
            return new
            {
                currentLevel = rule.CurrentLevel,
                cost = rule.Cost,
                successRate = rule.SuccessRate,
                keepRate = rule.KeepRate,
                destroyRate = rule.DestroyRate
            };
        }

        // 이전 데이터에도 누락 필드가 없도록 기본값을 보완합니다.
        private static void NormalizePlayer(PlayerState player)
        {
            if (player.Stats == null) player.Stats = new PlayerStats();
            if (player.RecentMessages == null) player.RecentMessages = new List<string>();
            if (player.Level <= 0) player.Level = 1;
        }

        // 사용자 최근 기록 맨 앞에 메시지를 넣고 최대 100줄만 유지합니다.
        private static void AddMessage(PlayerState player, string message)
        {
            NormalizePlayer(player);
            player.RecentMessages.Insert(0, message);
            while (player.RecentMessages.Count > 100) player.RecentMessages.RemoveAt(player.RecentMessages.Count - 1);
        }

        // 해금된 지역이고 강화 조건도 충족했는지 확인합니다.
        private bool CanEnter(PlayerState player, HuntingArea area)
        {
            return area.Id <= player.HighestBossDefeated + 1 && player.WeaponLevel >= area.RequiredEnhancement;
        }

        // 현재 사용자가 입장할 수 있는 가장 높은 사냥터를 찾습니다.
        private HuntingArea BestAvailableArea(PlayerState player)
        {
            return _catalog.Areas.Last(area => CanEnter(player, area));
        }

        // 레벨로 획득한 포인트에서 이미 사용한 포인트를 빼 반환합니다.
        private static int AvailableStatPoints(PlayerState player)
        {
            return Math.Max(0, player.Level - 1 - player.Stats.SpentPoints);
        }

        // 현재 레벨에 비례한 스탯 초기화 비용을 계산합니다.
        private static long StatResetCost(PlayerState player)
        {
            return player.Level * 10000L;
        }

        // 골드 획득량 스탯을 배율로 변환합니다.
        private static double GoldMultiplier(PlayerState player)
        {
            return 1 + player.Stats.GoldGain * .01;
        }

        // 경험치 획득량 스탯을 배율로 변환합니다.
        private static double ExperienceMultiplier(PlayerState player)
        {
            return 1 + player.Stats.ExperienceGain * .01;
        }

        // 기본 시간과 보스 처치 보너스를 합쳐 오늘의 자동 사냥 한도를 계산합니다.
        private static TimeSpan AutomaticHuntLimit(PlayerState player)
        {
            var defeatedBossCount = Math.Max(0, player.HighestBossDefeated + 1);
            return BaseAutomaticHuntDuration
                + TimeSpan.FromTicks(AutomaticHuntDurationPerBoss.Ticks * defeatedBossCount);
        }

        // 오늘 자동 사냥 한도에서 이미 사용한 시간을 빼 반환합니다.
        private static TimeSpan RemainingAutomaticHuntDuration(PlayerState player)
        {
            var remainingSeconds = AutomaticHuntLimit(player).TotalSeconds - player.AutomaticHuntUsedSeconds;
            return TimeSpan.FromSeconds(Math.Max(0, remainingSeconds));
        }

        // 한국 시간 자정을 기준으로 일일 자동 사냥 사용량을 초기화합니다.
        private static void NormalizeAutomaticHuntCycle(PlayerState player, DateTime now)
        {
            var cycleStart = CurrentAutomaticHuntCycleStart(now);
            if (!player.AutomaticHuntCycleStartedAtUtc.HasValue || player.AutomaticHuntCycleStartedAtUtc.Value < cycleStart)
            {
                player.AutomaticHuntCycleStartedAtUtc = cycleStart;
                player.AutomaticHuntUsedSeconds = 0;
                if (player.Hunt != null && player.Hunt.RewardCapAtUtc <= cycleStart) player.Hunt = null;
            }
        }

        // 현재 시점이 포함된 한국 시간 기준 일일 주기의 시작 시각을 구합니다.
        private static DateTime CurrentAutomaticHuntCycleStart(DateTime now)
        {
            var koreaNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(now, DateTimeKind.Utc), KoreaTimeZone);
            var localStart = koreaNow.Date;
            return TimeZoneInfo.ConvertTimeToUtc(localStart, KoreaTimeZone);
        }

        // 다음 한국 시간 자정의 UTC 시각을 구합니다.
        private static DateTime NextAutomaticHuntCycleStart(DateTime now)
        {
            return CurrentAutomaticHuntCycleStart(now).AddDays(1);
        }

        // 두 시각 가운데 더 빠른 값을 반환합니다.
        private static DateTime Min(DateTime left, DateTime right)
        {
            return left <= right ? left : right;
        }

        // UTC 시각을 브라우저가 읽을 수 있는 ISO 문자열로 바꿉니다.
        private static string Iso(DateTime value)
        {
            return value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        }

        // 전달한 확률에 따라 참·거짓 판정을 수행합니다.
        private static bool Roll(double chance)
        {
            return RandomInt(0, 1000000) < chance * 1000000;
        }

        // 강화 판정에 사용할 0 이상 1 미만의 난수를 만듭니다.
        private static double RandomDouble()
        {
            return RandomInt(0, 1000000) / 1000000d;
        }

        // 암호학적 난수 생성기를 사용해 지정 범위의 정수를 만듭니다.
        private static int RandomInt(int minValue, int maxValue)
        {
            using (var random = RandomNumberGenerator.Create())
            {
                var bytes = new byte[4];
                random.GetBytes(bytes);
                var value = BitConverter.ToUInt32(bytes, 0);
                return minValue + (int)(value % (uint)(maxValue - minValue));
            }
        }
    }

    public sealed class HuntReward
    {
        // 자동 사냥 정산 결과를 골드와 경험치 묶음으로 보관합니다.
        public HuntReward(long gold, long experience)
        {
            Gold = gold;
            Experience = experience;
        }
        public long Gold { get; private set; }
        public long Experience { get; private set; }
    }

    public sealed class ManualHuntReward
    {
        // 직접 사냥 결과를 몬스터 이름, 골드, 경험치 묶음으로 보관합니다.
        public ManualHuntReward(string monsterName, long gold, long experience)
        {
            MonsterName = monsterName;
            Gold = gold;
            Experience = experience;
        }
        public string MonsterName { get; private set; }
        public long Gold { get; private set; }
        public long Experience { get; private set; }
    }
}
