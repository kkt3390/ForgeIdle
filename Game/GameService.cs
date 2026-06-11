using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using EnhanceAddiction.WebForms.Data;

namespace EnhanceAddiction.WebForms.Game
{
    public sealed class GameService
    {
        private static readonly TimeSpan ManualHuntCooldown = TimeSpan.FromSeconds(1); 
        private static readonly TimeSpan BaseAutomaticHuntDuration = TimeSpan.FromHours(6);
        private static readonly TimeSpan AutomaticHuntDurationPerBoss = TimeSpan.FromMinutes(10);
        private static readonly TimeZoneInfo KoreaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time");
        private const int MonstersPerCollectionGrade = 10;
        private const double NormalCollectionRegistrationRate = 1.00;
        private const double SpecialCollectionRegistrationRate = .10;
        private const int RiftManualHuntsPerTicket = 30;
        private const int RiftDailyTicketLimit = 50;
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
            return Snapshot(player, true);
        }

        // ?? ?? ????? ?? ?? ?? ??? ???? ???? ???? ????.
        public object Snapshot(PlayerState player, bool includeCollection)
        {
            var now = DateTime.UtcNow;
            NormalizePlayer(player);
            NormalizeAutomaticHuntCycle(player, now);
            NormalizeRiftSeason(player, now);
            var rewardSettings = GameRewardSettings.Current();
            var availableAreas = _catalog.Areas
                .Where(area => area.Id <= player.HighestBossDefeated + 1)
                .Select(area => new
                {
                    id = area.Id,
                    name = area.Name,
                    requiredEnhancement = area.RequiredEnhancement,
                    goldPerHour = area.GoldPerHour,
                    experiencePerHour = area.ExperiencePerHour,
                    autoGoldPerHour = AutoGoldPerHour(player, area),
                    autoExperiencePerHour = AutoExperiencePerHour(player, area),
                    manualGoldPerHour = ManualGoldPerHour(player, area, rewardSettings),
                    manualExperiencePerHour = ManualExperiencePerHour(player, area, rewardSettings),
                    canEnter = CanEnter(player, area)
                }).ToArray();
            var enhancements = GameContentRepository.EnhancementRules(_catalog.Enhancements);
            var weapon = GameContentRepository.ActiveWeapon(player.WeaponLevel);
            var adjustedRule = player.WeaponLevel < enhancements.Count
                ? AdjustEnhancement(enhancements[player.WeaponLevel], player.Stats)
                : null;
            var nextBossArea = _catalog.Areas.ElementAtOrDefault(player.HighestBossDefeated + 1);
            var manualHuntArea = ManualHuntArea(player);
            return new
            {
                nickname = player.Nickname,
                profileMonster = ProfileMonsterSnapshot(player),
                serverNow = Iso(now),
                hotTime = HotTimeSnapshot(now),
                rewardMultipliers = new
                {
                    baseGold = rewardSettings.BaseGoldMultiplier,
                    baseExperience = rewardSettings.BaseExperienceMultiplier
                },
                rift = RiftSnapshot(player, now),
                gold = player.Gold,
                weaponLevel = player.WeaponLevel,
                weaponName = weapon.Name,
                weaponImagePath = weapon.ImagePath,
                weaponDescription = weapon.Description,
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
                    artisanTouch = player.Stats.ArtisanTouch,
                    destructionResistance = player.Stats.DestructionResistance,
                    recoveryMastery = player.Stats.RecoveryMastery
                },
                statResetCost = StatResetCost(player),
                automaticHuntBudget = new
                {
                    limitHours = AutomaticHuntLimit(player).TotalHours,
                    remainingHours = RemainingAutomaticHuntDuration(player).TotalHours,
                    resetsAt = Iso(NextAutomaticHuntCycleStart(now))
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
                    areaId = manualHuntArea.Id,
                    areaName = manualHuntArea.Name,
                    automaticGoldPerHour = manualHuntArea.GoldPerHour,
                    automaticExperiencePerHour = manualHuntArea.ExperiencePerHour,
                    adjustedAutomaticGoldPerHour = AutoGoldPerHour(player, manualHuntArea),
                    adjustedAutomaticExperiencePerHour = AutoExperiencePerHour(player, manualHuntArea),
                    adjustedManualGoldPerHour = ManualGoldPerHour(player, manualHuntArea, rewardSettings),
                    adjustedManualExperiencePerHour = ManualExperiencePerHour(player, manualHuntArea, rewardSettings),
                    availableAreas = availableAreas,
                    availableAt = player.LastManualHuntAtUtc.HasValue
                        ? Iso(player.LastManualHuntAtUtc.Value.Add(ManualHuntCooldown))
                        : null
                },
                collectionEnabled = GameFeatureSettings.CollectionEnabled,
                collection = includeCollection && GameFeatureSettings.CollectionEnabled ? CollectionSnapshot(player) : null,
                currentEnhancement = adjustedRule == null ? null : RuleSnapshot(adjustedRule),
                nextBoss = nextBossArea == null || !nextBossArea.BossRequiredEnhancement.HasValue ? null : new
                {
                    name = nextBossArea.Name + "의 보스",
                    requiredEnhancement = nextBossArea.BossRequiredEnhancement.Value,
                    health = nextBossArea.BossHealth.Value,
                    canChallenge = player.HighestWeaponLevel >= nextBossArea.BossRequiredEnhancement.Value
                },
                availableAreas = availableAreas,
                recentMessages = player.RecentMessages.Take(10)
            };
        }

        // 현재 핫타임 배율과 한국시간 표시용 기간을 브라우저에 내려줍니다.
        private static object HotTimeSnapshot(DateTime now)
        {
            var settings = GameRewardSettings.Current();
            return new
            {
                enabled = settings.Enabled,
                active = settings.IsActive(now),
                goldMultiplier = settings.GoldMultiplier,
                experienceMultiplier = settings.ExperienceMultiplier,
                startsAt = settings.StartsAtUtc.HasValue ? Iso(settings.StartsAtUtc.Value) : "",
                endsAt = settings.EndsAtUtc.HasValue ? Iso(settings.EndsAtUtc.Value) : "",
                startsAtKst = settings.StartsAtUtc.HasValue ? KstDisplay(settings.StartsAtUtc.Value) : "상시",
                endsAtKst = settings.EndsAtUtc.HasValue ? KstDisplay(settings.EndsAtUtc.Value) : "종료 미정"
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
                monsters = GameFeatureSettings.CollectionEnabled
                    ? new
                    {
                        normalRate = .979,
                        eliteRate = .02,
                        goldenRate = .001,
                        collectionRates = new
                        {
                            normal = NormalCollectionRegistrationRate,
                            elite = SpecialCollectionRegistrationRate,
                            golden = SpecialCollectionRegistrationRate
                        },
                        monstersPerGrade = MonstersPerCollectionGrade
                    }
                    : null,
                enhancements = GameContentRepository.EnhancementRules(_catalog.Enhancements).Select(RuleSnapshot).ToArray()
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
            player.ManualHuntAreaId = area.Id;
            AddMessage(player, area.Name + "에서 자동 사냥을 시작했습니다.");
            return Success(player, "자동 사냥을 시작했습니다.", new { areaId = area.Id });
        }

        // 진행 중인 자동 사냥을 종료하고 누적 보상을 정산합니다.
        public GameResult ClaimAutomaticHunt(PlayerState player)
        {
            if (player.Hunt == null) return Failure(player, "진행 중인 자동 사냥이 없습니다.");
            var reward = ClaimAutomaticHuntReward(player, DateTime.UtcNow);
            var message = string.Format("자동 사냥 정산: {0:N0} 골드, 경험치 {1:N2}", reward.Gold, reward.Experience);
            AddMessage(player, message);
            return Success(player, message, reward);
        }

        // 직접 사냥 1회를 처리하고 자동 사냥 중이었다면 먼저 정산합니다.
        public GameResult ManualHunt(PlayerState player, int areaId)
        {
            var now = DateTime.UtcNow;
            if (player.LastManualHuntAtUtc.HasValue && now - player.LastManualHuntAtUtc.Value < ManualHuntCooldown)
                return Failure(player, "아직 다음 몬스터를 찾는 중입니다.");

            var claimed = player.Hunt == null ? new HuntReward(0, 0) : ClaimAutomaticHuntReward(player, now);
            var area = _catalog.Areas.ElementAtOrDefault(areaId);
            if (area == null || !CanEnter(player, area)) return Failure(player, "직접 사냥할 수 없는 사냥터입니다.");
            player.ManualHuntAreaId = area.Id;
            var first = RollManualHunt(player, area);
            var dualWield = Roll(player.Stats.DualWield * .005);
            var second = dualWield ? RollManualHunt(player, area) : new ManualHuntReward("", 0, 0);
            var totalGold = first.Gold + second.Gold;
            var totalExperience = first.Experience + second.Experience;
            player.Gold += totalGold;
            GrantExperience(player, totalExperience);
            player.LastManualHuntAtUtc = now;
            player.ManualHuntCount++;
            var riftGrant = GrantRiftTicketProgress(player, now);

            var prefix = claimed.Gold > 0 || claimed.Experience > 0
                ? string.Format("자동 사냥 {0:N0} 골드, 경험치 {1:N2}를 먼저 정산했습니다. ", claimed.Gold, claimed.Experience)
                : "";
            var firstName = HuntLogMonsterName(first);
            var secondName = HuntLogMonsterName(second);
            var dualMessage = dualWield ? " 이도류 발동! " + secondName + "도 처치했습니다." : "";
            var registrations = GameFeatureSettings.CollectionEnabled
                ? new[]
                {
                    RollCollectionRegistration(player, first),
                    dualWield ? RollCollectionRegistration(player, second) : null
                }.Where(registration => registration != null).ToArray()
                : new CollectionRegistration[0];
            var collectionMessage = CollectionRegistrationMessage(registrations);
            var message = string.Format("{0}{1} 처치! {2:N0} 골드, 경험치 {3:N2} 획득.{4}{5}",
                prefix, firstName, totalGold, totalExperience, dualMessage, collectionMessage);
            AddMessage(player, message);
            return Success(player, message, new
            {
                claimed = claimed,
                first = first,
                second = second,
                dualWield = dualWield,
                registrations = registrations,
                rift = riftGrant
            });
        }

        // 골드를 소모해 강화를 시도하고 성공·유지·보호·파괴 결과를 처리합니다.
        public GameResult SetProfileMonster(PlayerState player, string monsterKey)
        {
            NormalizePlayer(player);
            var key = (monsterKey ?? "").Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                player.ProfileMonsterKey = null;
                AddMessage(player, "프로필 몬스터를 해제했습니다.");
                return Success(player, "프로필 몬스터를 해제했습니다.", null);
            }

            if (!player.CollectedMonsterKeys.Contains(key))
                return Failure(player, "수집한 몬스터만 프로필로 지정할 수 있습니다.");

            player.ProfileMonsterKey = key;
            AddMessage(player, MonsterNameForProfile(key) + "을(를) 프로필 몬스터로 지정했습니다.");
            return Success(player, "프로필 몬스터를 지정했습니다.", new { monsterKey = key });
        }

        public GameResult Enhance(PlayerState player, bool useProtection)
        {
            if (player.Hunt != null) return Failure(player, "자동 사냥을 종료하고 정산한 뒤 강화할 수 있습니다.");
            var enhancements = GameContentRepository.EnhancementRules(_catalog.Enhancements);
            if (player.WeaponLevel >= enhancements.Count) return Failure(player, "이미 최고 강화 단계입니다.");
            var rule = AdjustEnhancement(enhancements[player.WeaponLevel], player.Stats);
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
                var recoveryLevel = DestructionRecoveryLevel(player, before);
                player.WeaponLevel = recoveryLevel;
                message = recoveryLevel > 12
                    ? string.Format("무기가 파괴되었지만 복구 숙련이 발동해 +{0}로 복구되었습니다.", recoveryLevel)
                    : "무기가 파괴되어 +12로 복구되었습니다.";
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
            if (player.HighestWeaponLevel < area.BossRequiredEnhancement.Value)
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
                case "destructionResistance": current = player.Stats.DestructionResistance; break;
                case "recoveryMastery": current = player.Stats.RecoveryMastery; break;
                default: return Failure(player, "알 수 없는 스탯입니다.");
            }
            if (current >= GameCatalog.MaxStatLevel) return Failure(player, "이미 최대 레벨인 스탯입니다.");
            switch (stat)
            {
                case "dualWield": player.Stats.DualWield++; break;
                case "goldGain": player.Stats.GoldGain++; break;
                case "experienceGain": player.Stats.ExperienceGain++; break;
                case "artisanTouch": player.Stats.ArtisanTouch++; break;
                case "destructionResistance": player.Stats.DestructionResistance++; break;
                case "recoveryMastery": player.Stats.RecoveryMastery++; break;
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
        public GameResult HitRiftBoss(PlayerState player)
        {
            var now = DateTime.UtcNow;
            NormalizeRiftSeason(player, now);
            var settings = RiftSettings.Current();
            var season = settings.CurrentSeason(now);
            if (!settings.Enabled) return Failure(player, "주간 균열이 아직 열려 있지 않습니다.");
            if (!season.IsActive(now)) return Failure(player, season.IsSettlement(now) ? "주간 균열 정산 중입니다." : "균열 타격 가능 시간이 아닙니다.");
            if (player.RiftTickets <= 0) return Failure(player, "보유한 균열 타격권이 없습니다.");

            var power = RiftCombatPower(player);
            var multiplier = .85 + RandomDouble() * .30;
            var damage = Math.Max(1, (long)Math.Round(power * multiplier));
            player.RiftTickets--;
            player.RiftDamage += damage;
            player.RiftLastDamageAtUtc = now;
            AddMessage(player, string.Format("주간 균열 타격! {0:N0} 피해를 입혔습니다.", damage));
            return Success(player, string.Format("균열 보스에게 {0:N0} 피해를 입혔습니다.", damage), new
            {
                seasonKey = season.SeasonKey,
                damage = damage,
                combatPower = power,
                multiplier = multiplier,
                remainingTickets = player.RiftTickets
            });
        }

        public GameResult BuyRiftShopItem(PlayerState player, string itemKey)
        {
            var settings = RiftSettings.Current();
            if (!settings.ShopEnabled) return Failure(player, "균열 상점이 아직 열려 있지 않습니다.");

            var item = RiftShopItems().FirstOrDefault(candidate => candidate.Key == itemKey);
            if (item == null) return Failure(player, "알 수 없는 균열 상점 상품입니다.");
            if (player.RiftCoins < item.Cost) return Failure(player, "균열 파편이 부족합니다.");

            player.RiftCoins -= item.Cost;
            if (item.Type == "color")
            {
                player.ActiveNicknameColorKey = item.Key;
                player.NicknameColorExpiresAtUtc = DateTime.UtcNow.AddDays(7);
            }
            else if (item.Type == "title")
            {
                player.ActiveTitleKey = item.Key;
            }

            AddMessage(player, item.Name + " 상품을 구매했습니다.");
            return Success(player, item.Name + " 상품을 구매했습니다.", new { itemKey = item.Key, itemType = item.Type, cost = item.Cost });
        }

        private object GrantRiftTicketProgress(PlayerState player, DateTime now)
        {
            NormalizeRiftSeason(player, now);
            var settings = RiftSettings.Current();
            var season = settings.CurrentSeason(now);
            if (!settings.Enabled || !season.IsActive(now))
                return new { enabled = settings.Enabled, grantedTickets = 0 };

            player.RiftWeeklyManualHuntCount++;
            if (player.RiftDailyTicketsEarned >= RiftDailyTicketLimit)
                return new { enabled = true, grantedTickets = 0, dailyCapReached = true };

            player.RiftDailyManualHuntProgress++;
            var granted = 0;
            while (player.RiftDailyManualHuntProgress >= RiftManualHuntsPerTicket
                && player.RiftDailyTicketsEarned < RiftDailyTicketLimit)
            {
                player.RiftDailyManualHuntProgress -= RiftManualHuntsPerTicket;
                player.RiftDailyTicketsEarned++;
                player.RiftTickets++;
                granted++;
            }

            return new
            {
                enabled = true,
                grantedTickets = granted,
                dailyTicketsEarned = player.RiftDailyTicketsEarned,
                dailyTicketLimit = RiftDailyTicketLimit,
                nextTicketProgress = player.RiftDailyManualHuntProgress,
                huntsPerTicket = RiftManualHuntsPerTicket
            };
        }

        private object RiftSnapshot(PlayerState player, DateTime now)
        {
            var settings = RiftSettings.Current();
            var season = settings.CurrentSeason(now);
            var area = _catalog.Areas.ElementAtOrDefault(season.BossAreaId);
            return new
            {
                enabled = settings.Enabled,
                shopEnabled = settings.ShopEnabled,
                mode = season.Mode,
                active = settings.Enabled && season.IsActive(now),
                settling = settings.Enabled && season.IsSettlement(now),
                seasonKey = season.SeasonKey,
                seasonName = season.SeasonName,
                startsAt = Iso(season.StartsAtUtc),
                endsAt = Iso(season.EndsAtUtc),
                settlementEndsAt = Iso(season.SettlementEndsAtUtc),
                startsAtKst = KstDateTime(season.StartsAtUtc),
                endsAtKst = KstDateTime(season.EndsAtUtc),
                settlementEndsAtKst = KstDateTime(season.SettlementEndsAtUtc),
                bossAreaId = season.BossAreaId,
                bossName = area == null ? "균열 보스" : area.Name + " 균열 보스",
                bossImagePath = CollectionImagePath(season.BossAreaId, MonstersPerCollectionGrade),
                combatPower = RiftCombatPower(player),
                tickets = player.RiftTickets,
                weeklyManualHuntCount = player.RiftWeeklyManualHuntCount,
                dailyTicketsEarned = player.RiftDailyTicketsEarned,
                dailyTicketLimit = RiftDailyTicketLimit,
                nextTicketProgress = player.RiftDailyManualHuntProgress,
                huntsPerTicket = RiftManualHuntsPerTicket,
                damage = player.RiftDamage,
                coins = player.RiftCoins,
                title = CosmeticTitle(player.ActiveTitleKey),
                nicknameColor = ActiveNicknameColor(player, now),
                rankBadge = ActiveRankBadge(player, now),
                rankGlow = ActiveRankGlow(player, now),
                rewardRules = RiftRewardRules(),
                shopItems = settings.ShopEnabled ? RiftShopItems().Select(item => new
                {
                    key = item.Key,
                    name = item.Name,
                    type = item.Type,
                    cost = item.Cost,
                    durationDays = item.DurationDays
                }).ToArray() : new object[0]
            };
        }

        private static object[] RiftRewardRules()
        {
            return new object[]
            {
                new { rank = "지급 대상", reward = "피해량 1 이상 참여자만 보상 지급" },
                new { rank = "1위", reward = "120 균열 파편 + 전용 칭호 + Ⅰ 표식 + 금빛 테두리" },
                new { rank = "2위", reward = "100 균열 파편 + 전용 칭호 + Ⅱ 표식 + 은빛 테두리" },
                new { rank = "3위", reward = "80 균열 파편 + 전용 칭호 + Ⅲ 표식 + 동빛 테두리" },
                new { rank = "4~10위", reward = "60 균열 파편 + 도전자 칭호" },
                new { rank = "참여자 중 상위 30% (11위 이하)", reward = "35 균열 파편" },
                new { rank = "그 외 참여", reward = "15 균열 파편" }
            };
        }

        private void NormalizeRiftSeason(PlayerState player, DateTime now)
        {
            var settings = RiftSettings.Current();
            var season = settings.CurrentSeason(now);
            if (player.RiftSeasonKey != season.SeasonKey)
            {
                player.RiftSeasonKey = season.SeasonKey;
                player.RiftWeeklyManualHuntCount = 0;
                player.RiftDailyManualHuntProgress = 0;
                player.RiftTickets = 0;
                player.RiftDailyTicketsEarned = 0;
                player.RiftDamage = 0;
                player.RiftLastDamageAtUtc = null;
            }

            var dailyKey = KstDateKey(now);
            if (player.RiftDailyTicketDate != dailyKey)
            {
                player.RiftDailyTicketDate = dailyKey;
                player.RiftDailyManualHuntProgress = 0;
                player.RiftDailyTicketsEarned = 0;
            }

            if (player.NicknameColorExpiresAtUtc.HasValue && player.NicknameColorExpiresAtUtc.Value <= now)
            {
                player.ActiveNicknameColorKey = "";
                player.NicknameColorExpiresAtUtc = null;
            }
            if (player.RiftRankRewardExpiresAtUtc.HasValue && player.RiftRankRewardExpiresAtUtc.Value <= now)
            {
                player.RiftRankBadge = "";
                player.RiftRankGlow = "";
                player.RiftRankRewardExpiresAtUtc = null;
            }
        }

        private static double RiftCombatPower(PlayerState player)
        {
            var collectionCount = player.CollectedMonsterKeys == null ? 0 : player.CollectedMonsterKeys.Count;
            return Math.Round(
                100
                + Math.Pow(Math.Max(0, player.WeaponLevel), 1.15) * 4
                + Math.Pow(Math.Max(1, player.Level), .55) * 3
                + Math.Pow(Math.Max(0, collectionCount), .45) * 2,
                2);
        }

        private static RiftShopItem[] RiftShopItems()
        {
            return new[]
            {
                new RiftShopItem("color-blue", "푸른빛 닉네임 7일", "color", 80, 7),
                new RiftShopItem("color-purple", "보랏빛 닉네임 7일", "color", 100, 7),
                new RiftShopItem("color-red", "붉은빛 닉네임 7일", "color", 100, 7),
                new RiftShopItem("color-gold", "금빛 닉네임 7일", "color", 120, 7),
                new RiftShopItem("color-rainbow", "무지개 닉네임 7일", "color", 180, 7),
                new RiftShopItem("title-challenger", "차원의 도전자", "title", 200, 0),
                new RiftShopItem("title-survivor", "뒤틀림을 견딘 자", "title", 250, 0),
                new RiftShopItem("title-stalker", "심연의 추적자", "title", 300, 0),
                new RiftShopItem("title-cleaver", "균열을 가른 자", "title", 400, 0)
            };
        }

        private static string CosmeticTitle(string key)
        {
            var item = RiftShopItems().FirstOrDefault(candidate => candidate.Key == key && candidate.Type == "title");
            return item == null ? "" : item.Name;
        }

        private static string ActiveNicknameColor(PlayerState player, DateTime now)
        {
            return player.NicknameColorExpiresAtUtc.HasValue && player.NicknameColorExpiresAtUtc.Value > now
                ? player.ActiveNicknameColorKey
                : "";
        }

        private static string ActiveRankBadge(PlayerState player, DateTime now)
        {
            return player.RiftRankRewardExpiresAtUtc.HasValue && player.RiftRankRewardExpiresAtUtc.Value > now
                ? player.RiftRankBadge
                : "";
        }

        private static string ActiveRankGlow(PlayerState player, DateTime now)
        {
            return player.RiftRankRewardExpiresAtUtc.HasValue && player.RiftRankRewardExpiresAtUtc.Value > now
                ? player.RiftRankGlow
                : "";
        }

        // TODO: 주간 균열 2차 개발 후보
        // - 닉네임 효과 상품, 무기 카드 배경 스킨, 도감 카드 장식 프레임
        // - 시즌별 보상표 관리자 편집, 보상 지급 실패 재처리, 정산 dry-run
        // - 지난 시즌 랭킹 화면, 시즌별 보스 특수 효과, 시즌 결과 그래프

        private GameResult Success(PlayerState player, string message, object details)
        {
            return new GameResult { Ok = true, Message = message, State = Snapshot(player, false), Details = details };
        }

        // 실패 결과와 현재 사용자 상태를 공통 응답 형식으로 만듭니다.
        private GameResult Failure(PlayerState player, string message)
        {
            return new GameResult { Ok = false, Message = message, State = Snapshot(player, false) };
        }

        // 자동 사냥 경과 시간에 비례한 골드와 경험치를 실제 상태에 반영합니다.
        private HuntReward ClaimAutomaticHuntReward(PlayerState player, DateTime now)
        {
            var hunt = player.Hunt;
            var area = _catalog.Areas[hunt.AreaId];
            var duration = Min(now, hunt.RewardCapAtUtc) - hunt.StartedAtUtc;
            if (duration < TimeSpan.Zero) duration = TimeSpan.Zero;
            var gold = (long)Math.Floor(AutoGoldPerHour(player, area) * duration.TotalHours);
            var experience = AutoExperiencePerHour(player, area) * duration.TotalHours;
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
            var grade = multiplier == 30 ? "golden" : multiplier == 5 ? "elite" : "normal";
            var number = RandomInt(1, MonstersPerCollectionGrade + 1);
            var key = CollectionKey(area.Id, grade, number);
            var monsterCatalog = GameContentRepository.MonsterMap();
            MonsterCatalogEntry custom;
            monsterCatalog.TryGetValue(key, out custom);
            var name = custom == null ? CollectionMonsterName(area.Name, grade, number) : custom.Name;
            var imagePath = CollectionImagePath(area.Id, number);
            var gold = Math.Max(
                1,
                (long)Math.Round(area.GoldPerHour * 1.5 / actionsPerHour * variance * multiplier * GoldMultiplier(player)));
            var experience = Math.Max(
                .01,
                area.ExperiencePerHour * 1.25 / actionsPerHour * variance * multiplier * ExperienceMultiplier(player));
            return new ManualHuntReward(name, gold, experience, grade, key, imagePath);
        }

        // 경험치를 지급하고 필요한 만큼 연속 레벨업을 처리합니다.
        private void GrantExperience(PlayerState player, double amount)
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
        private static EnhancementRule AdjustEnhancement(EnhancementRule rule, PlayerStats stats)
        {
            stats = stats ?? new PlayerStats();
            var destroy = rule.DestroyRate * (1 - ClampStat(stats.DestructionResistance) * .03);
            var success = Math.Min(1 - destroy, rule.SuccessRate * (1 + ClampStat(stats.ArtisanTouch) * .005));
            return new EnhancementRule(rule.CurrentLevel, rule.Cost, success, 1 - success - destroy, destroy);
        }

        private static int DestructionRecoveryLevel(PlayerState player, int before)
        {
            var mastery = ClampStat(player.Stats == null ? 0 : player.Stats.RecoveryMastery);
            if (mastery <= 0 || before < 15) return 12;
            if (!Roll(mastery * .03)) return 12;
            return before >= 20 ? 18 : 15;
        }

        private static int ClampStat(int value)
        {
            return Math.Max(0, Math.Min(GameCatalog.MaxStatLevel, value));
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
            if (player.CollectedMonsterKeys == null) player.CollectedMonsterKeys = new List<string>();
            if (!string.IsNullOrWhiteSpace(player.ProfileMonsterKey) && !player.CollectedMonsterKeys.Contains(player.ProfileMonsterKey))
                player.ProfileMonsterKey = null;
            if (player.RecentMessages == null) player.RecentMessages = new List<string>();
            if (player.Level <= 0) player.Level = 1;
            if (player.ManualHuntAreaId < 0 || player.ManualHuntAreaId >= 12) player.ManualHuntAreaId = 0;
        }

        // 사용자 최근 기록 맨 앞에 메시지를 넣고 최대 100줄만 유지합니다.
        private static void AddMessage(PlayerState player, string message)
        {
            NormalizePlayer(player);
            player.RecentMessages.Insert(0, message);
            while (player.RecentMessages.Count > 10) player.RecentMessages.RemoveAt(player.RecentMessages.Count - 1);
        }

        // 해금된 지역이고 최고 강화 기록이 입장 조건을 충족했는지 확인합니다.
        private bool CanEnter(PlayerState player, HuntingArea area)
        {
            return area.Id <= player.HighestBossDefeated + 1 && player.HighestWeaponLevel >= area.RequiredEnhancement;
        }

        // 사용자가 선택한 직접 사냥터가 유효하지 않으면 입장 가능한 가장 높은 사냥터를 선택합니다.
        private HuntingArea ManualHuntArea(PlayerState player)
        {
            var selected = _catalog.Areas.ElementAtOrDefault(player.ManualHuntAreaId);
            return selected != null && CanEnter(player, selected)
                ? selected
                : _catalog.Areas.Last(area => CanEnter(player, area));
        }

        // 처치한 등급의 등록 확률에 따라 도감 항목 하나를 뽑고 중복 여부를 기록합니다.
        private static CollectionRegistration RollCollectionRegistration(PlayerState player, ManualHuntReward reward)
        {
            if (!Roll(CollectionRateForGrade(reward.Grade))) return new CollectionRegistration();

            var key = reward.MonsterKey;
            var duplicate = player.CollectedMonsterKeys.Contains(key);
            if (!duplicate) player.CollectedMonsterKeys.Add(key);
            return new CollectionRegistration
            {
                Registered = true,
                Duplicate = duplicate,
                MonsterKey = key,
                MonsterName = reward.MonsterName,
                Grade = reward.Grade,
                ImagePath = reward.ImagePath
            };
        }

        // 새로 등록된 도감 결과만 안내 문구로 보여줍니다. 중복 등록은 로그에는 남기되 화면 알림은 생략합니다.
        private static string CollectionRegistrationMessage(IEnumerable<CollectionRegistration> registrations)
        {
            var messages = registrations
                .Where(registration => registration.Registered && !registration.Duplicate)
                .Select(registration => " 도감 등록: " + registration.MonsterName + "!")
                .ToArray();
            return string.Join("", messages);
        }

        // 최근 기록에서 알아보기 쉽도록 몬스터 이름 앞에 등급 표기를 붙입니다.
        private static string HuntLogMonsterName(ManualHuntReward reward)
        {
            if (reward == null || string.IsNullOrWhiteSpace(reward.MonsterName)) return "몬스터";
            return "[" + GradeDisplayName(reward.Grade) + "] " + reward.MonsterName;
        }

        private static string GradeDisplayName(string grade)
        {
            if (grade == "elite") return "정예";
            if (grade == "golden") return "황금";
            return "일반";
        }

        // 도감 화면에 필요한 전체 항목과 사용자 등록 여부를 반환합니다.
        private object ProfileMonsterSnapshot(PlayerState player)
        {
            NormalizePlayer(player);
            var key = player.ProfileMonsterKey;
            int areaId;
            int number;
            string grade;
            if (!TryParseCollectionKey(key, out areaId, out grade, out number)) return null;
            if (!player.CollectedMonsterKeys.Contains(key)) return null;

            var monsterCatalog = GameContentRepository.MonsterMap();
            MonsterCatalogEntry custom;
            monsterCatalog.TryGetValue(key, out custom);
            var area = _catalog.Areas.ElementAtOrDefault(areaId);
            var areaName = area == null ? "도감" : area.Name;
            return new
            {
                monsterKey = key,
                key = key,
                grade = grade,
                name = custom == null ? CollectionMonsterName(areaName, grade, number) : custom.Name,
                description = custom == null ? "" : custom.Description,
                imagePath = CollectionImagePath(areaId, number)
            };
        }
        private object CollectionSnapshot(PlayerState player)
        {
            var monsterCatalog = GameContentRepository.MonsterMap();
            return new
            {
                collectedCount = player.CollectedMonsterKeys.Count,
                totalCount = _catalog.Areas.Count * 3 * MonstersPerCollectionGrade,
                areas = _catalog.Areas.Select(area => new
                {
                    id = area.Id,
                    name = area.Name,
                    unlocked = area.Id <= player.HighestBossDefeated + 1,
                    monsters = new[] { "normal", "elite", "golden" }
                        .SelectMany(grade => Enumerable.Range(1, MonstersPerCollectionGrade)
                            .Select(number =>
                            {
                                var key = CollectionKey(area.Id, grade, number);
                                MonsterCatalogEntry custom;
                                monsterCatalog.TryGetValue(key, out custom);
                                return new
                                {
                                    key = key,
                                    grade = grade,
                                    name = custom == null ? CollectionMonsterName(area.Name, grade, number) : custom.Name,
                                    description = custom == null ? "" : custom.Description,
                                    imagePath = CollectionImagePath(area.Id, number),
                                    collected = player.CollectedMonsterKeys.Contains(key)
                                };
                            })).ToArray()
                }).ToArray()
            };
        }

        // 지역, 등급, 번호를 DB에 저장할 안정적인 도감 키로 만듭니다.
        private string MonsterNameForProfile(string key)
        {
            int areaId;
            int number;
            string grade;
            if (!TryParseCollectionKey(key, out areaId, out grade, out number)) return "도감 몬스터";
            var monsterCatalog = GameContentRepository.MonsterMap();
            MonsterCatalogEntry custom;
            if (monsterCatalog.TryGetValue(key, out custom)) return custom.Name;
            var area = _catalog.Areas.ElementAtOrDefault(areaId);
            return CollectionMonsterName(area == null ? "도감" : area.Name, grade, number);
        }

        private static bool TryParseCollectionKey(string key, out int areaId, out string grade, out int number)
        {
            areaId = 0;
            number = 0;
            grade = "normal";
            if (string.IsNullOrWhiteSpace(key)) return false;
            var parts = key.Split('-');
            if (parts.Length != 4 || !string.Equals(parts[0], "area", StringComparison.OrdinalIgnoreCase)) return false;
            if (!int.TryParse(parts[1], out areaId)) return false;
            if (!int.TryParse(parts[3], out number)) return false;
            grade = parts[2];
            return grade == "normal" || grade == "elite" || grade == "golden";
        }
        private static string CollectionKey(int areaId, string grade, int number)
        {
            return string.Format("area-{0:D2}-{1}-{2:D2}", areaId, grade, number);
        }

        // 세 등급이 같은 몬스터 그림을 공유하도록 사냥터/순번 기준 공용 이미지 경로를 만듭니다.
        private static string CollectionImagePath(int areaId, int number)
        {
            return string.Format("Content/monsters/area-{0:D2}-{1:D2}.webp", areaId, number);
        }

        // 이미지가 준비되기 전에도 구분 가능한 임시 도감 이름을 만듭니다.
        private static string CollectionMonsterName(string areaName, string grade, int number)
        {
            var gradeName = grade == "golden" ? "황금" : grade == "elite" ? "정예" : "일반";
            return string.Format("{0} {1} 몬스터 {2:D2}", areaName, gradeName, number);
        }

        // 레벨로 획득한 포인트에서 이미 사용한 포인트를 빼 반환합니다.
        // 일반 몬스터는 도감 등록을 쉽게 열어주고 정예/황금은 희귀 등록 확률을 유지합니다.
        private static double CollectionRateForGrade(string grade)
        {
            return grade == "normal" ? NormalCollectionRegistrationRate : SpecialCollectionRegistrationRate;
        }

        private static int AvailableStatPoints(PlayerState player)
        {
            return Math.Max(0, player.Level - 1 - player.Stats.SpentPoints);
        }

        // 현재 레벨에 비례한 스탯 초기화 비용을 계산합니다.
        private static long StatResetCost(PlayerState player)
        {
            return player.Level * 10000L;
        }

        // 골드 획득량 스탯만 배율로 변환합니다. 자동사냥은 핫타임 배율을 적용하지 않습니다.
        private static double StatGoldMultiplier(PlayerState player)
        {
            return 1 + player.Stats.GoldGain * .01;
        }

        // 경험치 획득량 스탯만 배율로 변환합니다. 자동사냥은 핫타임 배율을 적용하지 않습니다.
        private static double StatExperienceMultiplier(PlayerState player)
        {
            return 1 + player.Stats.ExperienceGain * .01;
        }

        private static double BaseGoldMultiplier()
        {
            return GameRewardSettings.Current().BaseGoldMultiplier;
        }

        private static double BaseExperienceMultiplier()
        {
            return GameRewardSettings.Current().BaseExperienceMultiplier;
        }

        private static long AutoGoldPerHour(PlayerState player, HuntingArea area)
        {
            return (long)Math.Floor(area.GoldPerHour * StatGoldMultiplier(player) * BaseGoldMultiplier());
        }

        private static double AutoExperiencePerHour(PlayerState player, HuntingArea area)
        {
            return area.ExperiencePerHour * StatExperienceMultiplier(player) * BaseExperienceMultiplier();
        }

        private static long ManualGoldPerHour(PlayerState player, HuntingArea area, RewardMultiplierSettings settings)
        {
            var eventMultiplier = settings.IsActive(DateTime.UtcNow) ? settings.GoldMultiplier : 1;
            return (long)Math.Floor(area.GoldPerHour * 1.5 * StatGoldMultiplier(player) * settings.BaseGoldMultiplier * eventMultiplier);
        }

        private static double ManualExperiencePerHour(PlayerState player, HuntingArea area, RewardMultiplierSettings settings)
        {
            var eventMultiplier = settings.IsActive(DateTime.UtcNow) ? settings.ExperienceMultiplier : 1;
            return area.ExperiencePerHour * 1.25 * StatExperienceMultiplier(player) * settings.BaseExperienceMultiplier * eventMultiplier;
        }

        // 직접사냥용 골드 배율을 계산합니다. 핫타임 배율은 직접사냥에만 적용합니다.
        private static double GoldMultiplier(PlayerState player)
        {
            var eventSettings = GameRewardSettings.Current();
            var eventMultiplier = eventSettings.IsActive(DateTime.UtcNow) ? eventSettings.GoldMultiplier : 1;
            return StatGoldMultiplier(player) * eventSettings.BaseGoldMultiplier * eventMultiplier;
        }

        // 직접사냥용 경험치 배율을 계산합니다. 핫타임 배율은 직접사냥에만 적용합니다.
        private static double ExperienceMultiplier(PlayerState player)
        {
            var eventSettings = GameRewardSettings.Current();
            var eventMultiplier = eventSettings.IsActive(DateTime.UtcNow) ? eventSettings.ExperienceMultiplier : 1;
            return StatExperienceMultiplier(player) * eventSettings.BaseExperienceMultiplier * eventMultiplier;
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
        private void NormalizeAutomaticHuntCycle(PlayerState player, DateTime now)
        {
            var currentCycleStart = CurrentAutomaticHuntCycleStart(now);
            if (!player.AutomaticHuntCycleStartedAtUtc.HasValue)
            {
                player.AutomaticHuntCycleStartedAtUtc = currentCycleStart;
                return;
            }

            while (player.AutomaticHuntCycleStartedAtUtc.Value < currentCycleStart)
            {
                var nextCycleStart = player.AutomaticHuntCycleStartedAtUtc.Value.AddDays(1);
                var carriedAreaId = player.Hunt == null ? -1 : player.Hunt.AreaId;
                if (player.Hunt != null)
                {
                    var carriedReward = ClaimAutomaticHuntReward(player, nextCycleStart);
                    if (carriedReward.Gold > 0 || carriedReward.Experience > 0)
                    {
                        AddMessage(player, string.Format(
                            "자정 자동 정산: {0:N0} 골드, 경험치 {1:N2}를 획득했습니다.",
                            carriedReward.Gold,
                            carriedReward.Experience));
                    }
                }

                player.AutomaticHuntCycleStartedAtUtc = nextCycleStart;
                player.AutomaticHuntUsedSeconds = 0;

                if (carriedAreaId < 0)
                {
                    player.AutomaticHuntCycleStartedAtUtc = currentCycleStart;
                    break;
                }

                var area = _catalog.Areas.ElementAtOrDefault(carriedAreaId);
                var remaining = RemainingAutomaticHuntDuration(player);
                if (area != null && CanEnter(player, area) && remaining > TimeSpan.Zero)
                {
                    player.Hunt = new HuntSession
                    {
                        AreaId = area.Id,
                        StartedAtUtc = nextCycleStart,
                        RewardCapAtUtc = Min(nextCycleStart.Add(remaining), nextCycleStart.AddDays(1))
                    };
                }
                else
                {
                    player.Hunt = null;
                }
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

        private static string KstDisplay(DateTime value)
        {
            var utc = DateTime.SpecifyKind(value, DateTimeKind.Utc);
            return TimeZoneInfo.ConvertTimeFromUtc(utc, KoreaTimeZone).ToString("MM월 dd일 HH:mm");
        }

        // 전달한 확률에 따라 참·거짓 판정을 수행합니다.
        private static string KstDateTime(DateTime value)
        {
            var utc = DateTime.SpecifyKind(value, DateTimeKind.Utc);
            return TimeZoneInfo.ConvertTimeFromUtc(utc, KoreaTimeZone).ToString("yyyy-MM-dd HH:mm");
        }

        private static string KstDateKey(DateTime value)
        {
            var utc = DateTime.SpecifyKind(value, DateTimeKind.Utc);
            return TimeZoneInfo.ConvertTimeFromUtc(utc, KoreaTimeZone).ToString("yyyy-MM-dd");
        }

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
        public HuntReward(long gold, double experience)
        {
            Gold = gold;
            Experience = experience;
        }
        public long Gold { get; private set; }
        public double Experience { get; private set; }
    }

    public sealed class ManualHuntReward
    {
        // 직접 사냥 결과를 몬스터 이름, 골드, 경험치 묶음으로 보관합니다.
        public ManualHuntReward(string monsterName, long gold, double experience, string grade = null)
        {
            MonsterName = monsterName;
            Gold = gold;
            Experience = experience;
            Grade = grade;
        }
        public ManualHuntReward(string monsterName, long gold, double experience, string grade, string monsterKey, string imagePath)
            : this(monsterName, gold, experience, grade)
        {
            MonsterKey = monsterKey;
            ImagePath = imagePath;
        }
        public string MonsterName { get; private set; }
        public long Gold { get; private set; }
        public double Experience { get; private set; }
        public string Grade { get; private set; }
        public string MonsterKey { get; private set; }
        public string ImagePath { get; private set; }
    }

    public sealed class RiftShopItem
    {
        public RiftShopItem(string key, string name, string type, int cost, int durationDays)
        {
            Key = key;
            Name = name;
            Type = type;
            Cost = cost;
            DurationDays = durationDays;
        }

        public string Key { get; private set; }
        public string Name { get; private set; }
        public string Type { get; private set; }
        public int Cost { get; private set; }
        public int DurationDays { get; private set; }
    }
}
