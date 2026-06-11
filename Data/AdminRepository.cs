using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Web;
using System.Web.Script.Serialization;
using EnhanceAddiction.WebForms.Auth;
using EnhanceAddiction.WebForms.Game;

namespace EnhanceAddiction.WebForms.Data
{
    public sealed class AdminRepository
    {
        private static readonly TimeZoneInfo KoreaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time");
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

        // 관리자 API는 매 요청마다 로그인 여부와 운영자 권한을 DB에서 다시 확인합니다.
        public string RequireOperator(HttpContext context)
        {
            AuthSession.EnsureCurrentLogin(context);
            var playerKey = context.Session["PlayerKey"] as string;
            if (string.IsNullOrWhiteSpace(playerKey))
                throw new UnauthorizedAccessException("로그인이 필요합니다.");
            EnsureNotBanned(playerKey);
            if (!IsOperator(playerKey))
                throw new UnauthorizedAccessException("운영자 권한이 필요합니다.");
            return playerKey;
        }

        public bool IsOperator(string playerKey)
        {
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(
                "SELECT ISNULL(IsOperator, 0) FROM dbo.ea_players WHERE PlayerKey = @PlayerKey", connection))
            {
                command.Parameters.Add("@PlayerKey", SqlDbType.NVarChar, 100).Value = playerKey;
                var value = command.ExecuteScalar();
                return value != null && value != DBNull.Value && Convert.ToBoolean(value);
            }
        }

        public void EnsureNotBanned(string playerKey)
        {
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(
                @"SELECT IsBanned, BanReason
                  FROM dbo.ea_players
                  WHERE PlayerKey = @PlayerKey", connection))
            {
                command.Parameters.Add("@PlayerKey", SqlDbType.NVarChar, 100).Value = playerKey;
                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read()) return;
                    if (!reader.GetBoolean(0)) return;
                    var reason = reader.IsDBNull(1) ? "운영 정책 위반으로 접속이 제한되었습니다." : reader.GetString(1);
                    throw new UnauthorizedAccessException("접속이 제한된 계정입니다. 사유: " + reason);
                }
            }
        }

        public object GetState()
        {
            return new
            {
                dashboard = GetDashboard(),
                hotTime = GetHotTime()
            };
        }

        public object GetTabState(string tab)
        {
            tab = (tab ?? "").Trim().ToLowerInvariant();
            switch (tab)
            {
                case "abuse":
                    return new { suspiciousUsers = GetSuspiciousUsers() };
                case "users":
                    return new { operators = GetOperators() };
                case "event":
                    return new { hotTime = GetHotTime() };
                case "rift":
                    return new { rift = GetRiftState() };
                case "enhancements":
                    return new { enhancementRules = GetEnhancementRules() };
                case "enhancement-proof":
                    return new { enhancementProof = GetEnhancementProof() };
                case "monsters":
                    return new { monsterCatalog = GetMonsterCatalog() };
                case "weapons":
                    return new { weaponCatalog = GetWeaponCatalog() };
                case "action-logs":
                    return new { recentActionLogs = SearchGameActionLogs("", 1, 100) };
                case "logs":
                    return new { recentAdminLogs = GetAdminLogs("", 1, 100) };
                default:
                    throw new InvalidOperationException("없는 관리자 탭입니다.");
            }
        }

        public object GetHotTime()
        {
            var settings = GameRewardSettings.Current();
            return new
            {
                enabled = settings.Enabled,
                active = settings.IsActive(DateTime.UtcNow),
                goldMultiplier = settings.GoldMultiplier,
                experienceMultiplier = settings.ExperienceMultiplier,
                baseGoldMultiplier = settings.BaseGoldMultiplier,
                baseExperienceMultiplier = settings.BaseExperienceMultiplier,
                startsAtUtc = settings.StartsAtUtc.HasValue ? Iso(settings.StartsAtUtc.Value) : "",
                endsAtUtc = settings.EndsAtUtc.HasValue ? Iso(settings.EndsAtUtc.Value) : "",
                startsAtKst = settings.StartsAtUtc.HasValue ? KstLocalInput(settings.StartsAtUtc.Value) : "",
                endsAtKst = settings.EndsAtUtc.HasValue ? KstLocalInput(settings.EndsAtUtc.Value) : "",
                serverNowKst = KstLocalInput(DateTime.UtcNow)
            };
        }

        public void SaveHotTime(string operatorKey, bool enabled, double goldMultiplier, double experienceMultiplier, double baseGoldMultiplier, double baseExperienceMultiplier, string startsAtKst, string endsAtKst)
        {
            goldMultiplier = Clamp(goldMultiplier, 0.1, 20);
            experienceMultiplier = Clamp(experienceMultiplier, 0.1, 20);
            baseGoldMultiplier = Clamp(baseGoldMultiplier, 0.1, 20);
            baseExperienceMultiplier = Clamp(baseExperienceMultiplier, 0.1, 20);
            var normalizedStartsAtUtc = NormalizeKstToUtcIso(startsAtKst);
            var normalizedEndsAtUtc = NormalizeKstToUtcIso(endsAtKst);
            ValidateHotTimeRange(normalizedStartsAtUtc, normalizedEndsAtUtc);
            UpsertSetting("HotTimeEnabled", enabled ? "1" : "0", operatorKey);
            UpsertSetting("HotTimeGoldMultiplier", goldMultiplier.ToString(CultureInfo.InvariantCulture), operatorKey);
            UpsertSetting("HotTimeExperienceMultiplier", experienceMultiplier.ToString(CultureInfo.InvariantCulture), operatorKey);
            UpsertSetting("BaseGoldMultiplier", baseGoldMultiplier.ToString(CultureInfo.InvariantCulture), operatorKey);
            UpsertSetting("BaseExperienceMultiplier", baseExperienceMultiplier.ToString(CultureInfo.InvariantCulture), operatorKey);
            UpsertSetting("HotTimeStartsAtUtc", normalizedStartsAtUtc, operatorKey);
            UpsertSetting("HotTimeEndsAtUtc", normalizedEndsAtUtc, operatorKey);
            GameRewardSettings.ClearCache();
            AddAdminLog(operatorKey, "핫타임 배율 저장", null, new
            {
                enabled = enabled,
                goldMultiplier = goldMultiplier,
                experienceMultiplier = experienceMultiplier,
                baseGoldMultiplier = baseGoldMultiplier,
                baseExperienceMultiplier = baseExperienceMultiplier,
                startsAtKst = startsAtKst,
                endsAtKst = endsAtKst,
                startsAtUtc = normalizedStartsAtUtc,
                endsAtUtc = normalizedEndsAtUtc
            });
        }

        public object GetRiftState()
        {
            var settings = RiftSettings.Current();
            var now = DateTime.UtcNow;
            var season = settings.CurrentSeason(now);
            return new
            {
                enabled = settings.Enabled,
                shopEnabled = settings.ShopEnabled,
                mode = settings.Mode,
                manualSeasonName = settings.ManualSeasonName,
                manualStartsAtKst = settings.ManualStartsAtUtc.HasValue ? KstLocalInput(settings.ManualStartsAtUtc.Value) : "",
                manualEndsAtKst = settings.ManualEndsAtUtc.HasValue ? KstLocalInput(settings.ManualEndsAtUtc.Value) : "",
                manualSettlementEndsAtKst = settings.ManualSettlementEndsAtUtc.HasValue ? KstLocalInput(settings.ManualSettlementEndsAtUtc.Value) : "",
                manualBossAreaId = settings.ManualBossAreaId,
                season = new
                {
                    seasonKey = season.SeasonKey,
                    seasonName = season.SeasonName,
                    mode = season.Mode,
                    bossAreaId = season.BossAreaId,
                    startsAtKst = KstDateTime(new DateTimeOffset(season.StartsAtUtc)),
                    endsAtKst = KstDateTime(new DateTimeOffset(season.EndsAtUtc)),
                    settlementEndsAtKst = KstDateTime(new DateTimeOffset(season.SettlementEndsAtUtc)),
                    active = settings.Enabled && season.IsActive(now),
                    settling = settings.Enabled && season.IsSettlement(now)
                },
                rankings = GetRiftRankings(season.SeasonKey),
                rewardPreview = GetRiftRewardPreview(season.SeasonKey),
                recentResults = GetRiftSeasonResults()
            };
        }

        public void SaveRiftSettings(string operatorKey, Dictionary<string, object> body)
        {
            var enabled = BoolValue(body, "enabled", false);
            var shopEnabled = BoolValue(body, "shopEnabled", false);
            var mode = TextValue(body, "mode", 20, "auto") == "manual" ? "manual" : "auto";
            var seasonName = TextValue(body, "manualSeasonName", 100, "테스트 균열");
            var startsAtUtc = NormalizeKstToUtcIso(TextValue(body, "manualStartsAtKst", 30));
            var endsAtUtc = NormalizeKstToUtcIso(TextValue(body, "manualEndsAtKst", 30));
            var settlementEndsAtUtc = NormalizeKstToUtcIso(TextValue(body, "manualSettlementEndsAtKst", 30));
            var bossAreaId = Math.Min(11, Math.Max(0, IntValue(body, "manualBossAreaId")));

            if (mode == "manual")
            {
                ValidateRiftManualRange(startsAtUtc, endsAtUtc, settlementEndsAtUtc);
            }

            UpsertSetting("RiftEnabled", enabled ? "1" : "0", operatorKey);
            UpsertSetting("RiftShopEnabled", shopEnabled ? "1" : "0", operatorKey);
            UpsertSetting("RiftMode", mode, operatorKey);
            UpsertSetting("RiftManualSeasonName", seasonName, operatorKey);
            UpsertSetting("RiftManualStartsAtUtc", startsAtUtc, operatorKey);
            UpsertSetting("RiftManualEndsAtUtc", endsAtUtc, operatorKey);
            UpsertSetting("RiftManualSettlementEndsAtUtc", settlementEndsAtUtc, operatorKey);
            UpsertSetting("RiftManualBossAreaId", bossAreaId.ToString(CultureInfo.InvariantCulture), operatorKey);
            UpsertSetting("RiftForcedSettledSeasonKey", "", operatorKey);
            UpsertSetting("RiftForcedSettledAtUtc", "", operatorKey);
            RiftSettings.ClearCache();
            AddAdminLog(operatorKey, "주간 균열 설정 저장", null, body);
        }

        public void ResetCurrentRiftSeason(string operatorKey)
        {
            var season = RiftSettings.Current().CurrentSeason(DateTime.UtcNow);
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(
                @"UPDATE dbo.ea_players
                  SET RiftWeeklyManualHuntCount = 0,
                      RiftDailyManualHuntProgress = 0,
                      RiftTickets = 0,
                      RiftDailyTicketsEarned = 0,
                      RiftDamage = 0,
                      RiftLastDamageAtUtc = NULL,
                      UpdatedAt = SYSDATETIMEOFFSET()
                  WHERE RiftSeasonKey = @SeasonKey", connection))
            {
                command.Parameters.Add("@SeasonKey", SqlDbType.NVarChar, 40).Value = season.SeasonKey;
                command.ExecuteNonQuery();
            }
            AddAdminLog(operatorKey, "주간 균열 시즌 데이터 초기화", null, new { seasonKey = season.SeasonKey });
        }

        public void ClearRiftRankRewards(string operatorKey)
        {
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(
                @"UPDATE dbo.ea_players
                  SET RiftRankBadge = N'',
                      RiftRankGlow = N'',
                      RiftRankRewardExpiresAtUtc = NULL,
                      ActiveTitleKey = CASE WHEN ActiveTitleKey LIKE N'title-rift-%' THEN N'' ELSE ActiveTitleKey END,
                      UpdatedAt = SYSDATETIMEOFFSET()
                  WHERE RiftRankBadge <> N''
                     OR RiftRankGlow <> N''
                     OR RiftRankRewardExpiresAtUtc IS NOT NULL
                     OR ActiveTitleKey LIKE N'title-rift-%'", connection))
            {
                var affectedRows = command.ExecuteNonQuery();
                AddAdminLog(operatorKey, "주간 균열 랭킹 보상 효과 제거", null, new { affectedRows = affectedRows });
            }
        }

        public void SettleCurrentRiftSeason(string operatorKey)
        {
            var settings = RiftSettings.Current();
            var settledAtUtc = DateTime.UtcNow;
            var season = settings.CurrentSeason(settledAtUtc);
            var rankings = LoadRiftRankingRows(season.SeasonKey);
            if (!rankings.Any()) throw new InvalidOperationException("정산할 균열 참여 기록이 없습니다.");

            using (var connection = OpenConnection())
            using (var transaction = connection.BeginTransaction())
            {
                using (var exists = new SqlCommand(
                    "SELECT COUNT(1) FROM dbo.ea_rift_season_results WHERE SeasonKey = @SeasonKey",
                    connection, transaction))
                {
                    exists.Parameters.Add("@SeasonKey", SqlDbType.NVarChar, 40).Value = season.SeasonKey;
                    if ((int)exists.ExecuteScalar() > 0)
                        throw new InvalidOperationException("이미 정산된 시즌입니다.");
                }

                var rewardRows = BuildRiftRewardRows(rankings);
                var totalDamage = rankings.Sum(row => row.Damage);
                using (var resultCommand = new SqlCommand(@"
INSERT INTO dbo.ea_rift_season_results
    (SeasonKey, SeasonName, Mode, BossAreaId, StartsAtUtc, EndsAtUtc, SettlementEndsAtUtc,
     SettledAtUtc, TotalParticipants, TotalDamage, DetailsJson)
VALUES
    (@SeasonKey, @SeasonName, @Mode, @BossAreaId, @StartsAtUtc, @EndsAtUtc, @SettlementEndsAtUtc,
     SYSDATETIMEOFFSET(), @TotalParticipants, @TotalDamage, @DetailsJson)", connection, transaction))
                {
                    resultCommand.Parameters.Add("@SeasonKey", SqlDbType.NVarChar, 40).Value = season.SeasonKey;
                    resultCommand.Parameters.Add("@SeasonName", SqlDbType.NVarChar, 100).Value = season.SeasonName;
                    resultCommand.Parameters.Add("@Mode", SqlDbType.NVarChar, 20).Value = season.Mode;
                    resultCommand.Parameters.Add("@BossAreaId", SqlDbType.Int).Value = season.BossAreaId;
                    resultCommand.Parameters.Add("@StartsAtUtc", SqlDbType.DateTimeOffset).Value = season.StartsAtUtc;
                    resultCommand.Parameters.Add("@EndsAtUtc", SqlDbType.DateTimeOffset).Value = season.EndsAtUtc;
                    resultCommand.Parameters.Add("@SettlementEndsAtUtc", SqlDbType.DateTimeOffset).Value = season.SettlementEndsAtUtc;
                    resultCommand.Parameters.Add("@TotalParticipants", SqlDbType.Int).Value = rankings.Count;
                    resultCommand.Parameters.Add("@TotalDamage", SqlDbType.BigInt).Value = totalDamage;
                    resultCommand.Parameters.Add("@DetailsJson", SqlDbType.NVarChar, -1).Value = Json.Serialize(new
                    {
                        rewards = "주간 균열 1차 보상표",
                        topThirtyCut = rewardRows.Any() ? rewardRows.Max(row => row.TopThirtyCut) : 0
                    });
                    resultCommand.ExecuteNonQuery();
                }

                foreach (var rewardRow in rewardRows)
                {
                    using (var update = new SqlCommand(@"
UPDATE dbo.ea_players
SET RiftCoins = RiftCoins + @RewardCoins,
    ActiveTitleKey = CASE WHEN @TitleKey <> N'' THEN @TitleKey ELSE ActiveTitleKey END,
    RiftRankBadge = @Badge,
    RiftRankGlow = @Glow,
    RiftRankRewardExpiresAtUtc = CASE WHEN @Badge <> N'' THEN DATEADD(day, 7, SYSUTCDATETIME()) ELSE RiftRankRewardExpiresAtUtc END,
    UpdatedAt = SYSDATETIMEOFFSET()
WHERE PlayerKey = @PlayerKey", connection, transaction))
                    {
                        update.Parameters.Add("@RewardCoins", SqlDbType.Int).Value = rewardRow.Coins;
                        update.Parameters.Add("@TitleKey", SqlDbType.NVarChar, 80).Value = rewardRow.TitleKey;
                        update.Parameters.Add("@Badge", SqlDbType.NVarChar, 20).Value = rewardRow.Badge;
                        update.Parameters.Add("@Glow", SqlDbType.NVarChar, 20).Value = rewardRow.Glow;
                        update.Parameters.Add("@PlayerKey", SqlDbType.NVarChar, 100).Value = rewardRow.PlayerKey;
                        update.ExecuteNonQuery();
                    }

                    using (var snapshot = new SqlCommand(@"
INSERT INTO dbo.ea_rift_ranking_snapshots
    (SeasonKey, PlayerKey, RankNo, Damage, RewardCoins, RewardTitleKey, RewardBadge, RewardGlow, SnapshotAtUtc)
VALUES
    (@SeasonKey, @PlayerKey, @RankNo, @Damage, @RewardCoins, @RewardTitleKey, @RewardBadge, @RewardGlow, SYSDATETIMEOFFSET())",
                        connection, transaction))
                    {
                        snapshot.Parameters.Add("@SeasonKey", SqlDbType.NVarChar, 40).Value = season.SeasonKey;
                        snapshot.Parameters.Add("@PlayerKey", SqlDbType.NVarChar, 100).Value = rewardRow.PlayerKey;
                        snapshot.Parameters.Add("@RankNo", SqlDbType.Int).Value = rewardRow.Rank;
                        snapshot.Parameters.Add("@Damage", SqlDbType.BigInt).Value = rewardRow.Damage;
                        snapshot.Parameters.Add("@RewardCoins", SqlDbType.Int).Value = rewardRow.Coins;
                        snapshot.Parameters.Add("@RewardTitleKey", SqlDbType.NVarChar, 80).Value = rewardRow.TitleKey;
                        snapshot.Parameters.Add("@RewardBadge", SqlDbType.NVarChar, 20).Value = rewardRow.Badge;
                        snapshot.Parameters.Add("@RewardGlow", SqlDbType.NVarChar, 20).Value = rewardRow.Glow;
                        snapshot.ExecuteNonQuery();
                    }
                }

                UpsertSetting(connection, transaction, "RiftForcedSettledSeasonKey", season.SeasonKey, operatorKey);
                UpsertSetting(connection, transaction, "RiftForcedSettledAtUtc", settledAtUtc.ToString("o", CultureInfo.InvariantCulture), operatorKey);

                transaction.Commit();
            }

            RiftSettings.ClearCache();
            AddAdminLog(operatorKey, "주간 균열 강제 정산", null, new { seasonKey = season.SeasonKey, participants = rankings.Count });
        }

        public void SetOperator(string operatorKey, string targetPlayerKey, bool isOperator)
        {
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(
                @"UPDATE dbo.ea_players
                  SET IsOperator = @IsOperator,
                      UpdatedAt = SYSDATETIMEOFFSET()
                  WHERE PlayerKey = @TargetPlayerKey", connection))
            {
                command.Parameters.Add("@IsOperator", SqlDbType.Bit).Value = isOperator;
                command.Parameters.Add("@TargetPlayerKey", SqlDbType.NVarChar, 100).Value = targetPlayerKey;
                if (command.ExecuteNonQuery() == 0)
                    throw new InvalidOperationException("대상 유저를 찾을 수 없습니다.");
            }
            AddAdminLog(operatorKey, isOperator ? "운영자 지정" : "운영자 해제", targetPlayerKey, new { isOperator = isOperator });
        }

        public void SetBan(string operatorKey, string targetPlayerKey, bool isBanned, string reason)
        {
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(
                @"UPDATE dbo.ea_players
                  SET IsBanned = @IsBanned,
                      BanReason = CASE WHEN @IsBanned = 1 THEN @Reason ELSE NULL END,
                      BannedAtUtc = CASE WHEN @IsBanned = 1 THEN SYSDATETIMEOFFSET() ELSE NULL END,
                      UpdatedAt = SYSDATETIMEOFFSET()
                  WHERE PlayerKey = @TargetPlayerKey", connection))
            {
                command.Parameters.Add("@IsBanned", SqlDbType.Bit).Value = isBanned;
                command.Parameters.Add("@Reason", SqlDbType.NVarChar, 500).Value =
                    string.IsNullOrWhiteSpace(reason) ? "부정행위 확인" : reason.Trim();
                command.Parameters.Add("@TargetPlayerKey", SqlDbType.NVarChar, 100).Value = targetPlayerKey;
                if (command.ExecuteNonQuery() == 0)
                    throw new InvalidOperationException("대상 유저를 찾을 수 없습니다.");
            }
            AddAdminLog(operatorKey, isBanned ? "유저 접속 차단" : "유저 차단 해제", targetPlayerKey, new { reason = reason });
        }

        public object SearchPlayers(string keyword)
        {
            var rows = new List<object>();
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(
                @"SELECT PlayerKey, Nickname, Level, Gold, Experience, WeaponLevel, IsOperator, IsBanned, BanReason, UpdatedAt
                  FROM dbo.ea_players
                  WHERE @Keyword = N''
                     OR PlayerKey LIKE N'%' + @Keyword + N'%'
                     OR Nickname LIKE N'%' + @Keyword + N'%'
                  ORDER BY UpdatedAt DESC", connection))
            {
                command.Parameters.Add("@Keyword", SqlDbType.NVarChar, 100).Value = (keyword ?? "").Trim();
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        rows.Add(new
                        {
                            playerKey = reader.GetString(0),
                            nickname = reader.IsDBNull(1) ? "" : reader.GetString(1),
                            level = reader.GetInt32(2),
                            gold = reader.GetInt64(3),
                            experience = reader.GetDouble(4),
                            weaponLevel = reader.GetInt32(5),
                            isOperator = reader.GetBoolean(6),
                            isBanned = reader.GetBoolean(7),
                            banReason = reader.IsDBNull(8) ? "" : reader.GetString(8),
                            updatedAt = KstDateTime(reader.GetDateTimeOffset(9))
                        });
                    }
                }
            }
            return rows;
        }

        // 게임 데이터에 영향을 준 유저 행동 로그를 운영자가 검색해 볼 수 있게 반환합니다.
        public object SearchGameActionLogs(string keyword, int page, int pageSize)
        {
            var rows = new List<object>();
            var totalRows = 0;
            page = Math.Max(1, page);
            pageSize = Math.Min(200, Math.Max(10, pageSize));
            var offset = (page - 1) * pageSize;
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(
                @"WITH FilteredLogs AS
                  (
                    SELECT
                      l.Id, l.PlayerKey, ISNULL(p.Nickname, N'') AS Nickname, l.ActionType,
                      l.Succeeded, l.Message, l.BeforeStateJson, l.AfterStateJson, l.DetailsJson, l.CreatedAt,
                      COUNT(1) OVER() AS TotalRows
                    FROM dbo.ea_game_action_logs l
                    LEFT JOIN dbo.ea_players p ON p.PlayerKey = l.PlayerKey
                    WHERE @Keyword = N''
                       OR l.PlayerKey LIKE N'%' + @Keyword + N'%'
                       OR ISNULL(p.Nickname, N'') LIKE N'%' + @Keyword + N'%'
                       OR l.ActionType LIKE N'%' + @Keyword + N'%'
                       OR l.Message LIKE N'%' + @Keyword + N'%'
                  )
                  SELECT Id, PlayerKey, Nickname, ActionType,
                    l.Succeeded, l.Message, l.BeforeStateJson, l.AfterStateJson, l.DetailsJson, l.CreatedAt, l.TotalRows
                  FROM FilteredLogs l
                  ORDER BY l.CreatedAt DESC
                  OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY", connection))
            {
                command.Parameters.Add("@Keyword", SqlDbType.NVarChar, 100).Value = (keyword ?? "").Trim();
                command.Parameters.Add("@Offset", SqlDbType.Int).Value = offset;
                command.Parameters.Add("@PageSize", SqlDbType.Int).Value = pageSize;
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (totalRows == 0) totalRows = reader.GetInt32(10);
                        rows.Add(new
                        {
                            id = reader.GetInt64(0),
                            playerKey = reader.GetString(1),
                            nickname = reader.GetString(2),
                            actionType = reader.GetString(3),
                            succeeded = reader.GetBoolean(4),
                            message = reader.GetString(5),
                            beforeStateJson = reader.GetString(6),
                            afterStateJson = reader.GetString(7),
                            detailsJson = reader.IsDBNull(8) ? "" : reader.GetString(8),
                            createdAt = KstDateTime(reader.GetDateTimeOffset(9))
                        });
                    }
                }
            }
            return new { rows = rows, totalRows = totalRows, page = page, pageSize = pageSize };
        }

        public void UpsertMonster(string operatorKey, Dictionary<string, object> body)
        {
            var id = IntValue(body, "id");
            var monsterKey = TextValue(body, "monsterKey", 120);
            if (string.IsNullOrWhiteSpace(monsterKey)) throw new InvalidOperationException("몬스터 키가 필요합니다.");
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(
                id > 0
                    ? @"UPDATE dbo.ea_monster_catalog
                        SET MonsterKey = @MonsterKey, AreaId = @AreaId, Grade = @Grade, SlotNumber = @SlotNumber,
                            Name = @Name, Description = @Description, ImagePath = @ImagePath,
                            SortOrder = @SortOrder, IsVisible = @IsVisible, UpdatedAt = SYSDATETIMEOFFSET()
                        WHERE Id = @Id"
                    : @"INSERT INTO dbo.ea_monster_catalog
                        (MonsterKey, AreaId, Grade, SlotNumber, Name, Description, ImagePath, SortOrder, IsVisible, UpdatedAt)
                        VALUES
                        (@MonsterKey, @AreaId, @Grade, @SlotNumber, @Name, @Description, @ImagePath, @SortOrder, @IsVisible, SYSDATETIMEOFFSET())",
                connection))
            {
                AddCatalogParameters(command, body);
                command.Parameters.Add("@Id", SqlDbType.BigInt).Value = id;
                command.ExecuteNonQuery();
            }
            AddAdminLog(operatorKey, "도감 데이터 저장", null, body);
            GameContentRepository.ClearCache();
        }

        public void UpsertWeapon(string operatorKey, Dictionary<string, object> body)
        {
            var id = IntValue(body, "id");
            var weaponKey = TextValue(body, "weaponKey", 120);
            if (string.IsNullOrWhiteSpace(weaponKey)) throw new InvalidOperationException("무기 키가 필요합니다.");
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(
                id > 0
                    ? @"UPDATE dbo.ea_weapon_catalog
                        SET WeaponKey = @WeaponKey, Name = @Name, Description = @Description, ImagePath = @ImagePath,
                            SortOrder = @SortOrder, IsVisible = @IsVisible, UpdatedAt = SYSDATETIMEOFFSET()
                        WHERE Id = @Id"
                    : @"INSERT INTO dbo.ea_weapon_catalog
                        (WeaponKey, Name, Description, ImagePath, SortOrder, IsVisible, UpdatedAt)
                        VALUES
                        (@WeaponKey, @Name, @Description, @ImagePath, @SortOrder, @IsVisible, SYSDATETIMEOFFSET())",
                connection))
            {
                command.Parameters.Add("@Id", SqlDbType.BigInt).Value = id;
                command.Parameters.Add("@WeaponKey", SqlDbType.NVarChar, 120).Value = weaponKey;
                command.Parameters.Add("@Name", SqlDbType.NVarChar, 100).Value = TextValue(body, "name", 100, "이름 없음");
                command.Parameters.Add("@Description", SqlDbType.NVarChar, 1000).Value = NullableText(body, "description", 1000);
                command.Parameters.Add("@ImagePath", SqlDbType.NVarChar, 300).Value = NullableText(body, "imagePath", 300);
                command.Parameters.Add("@SortOrder", SqlDbType.Int).Value = IntValue(body, "sortOrder");
                command.Parameters.Add("@IsVisible", SqlDbType.Bit).Value = BoolValue(body, "isVisible", true);
                command.ExecuteNonQuery();
            }
            AddAdminLog(operatorKey, "무기 데이터 저장", null, body);
            GameContentRepository.ClearCache();
        }

        public void DeleteMonster(string operatorKey, Dictionary<string, object> body)
        {
            var id = IntValue(body, "id");
            if (id <= 0) throw new InvalidOperationException("삭제할 도감 데이터가 필요합니다.");
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(
                @"DELETE FROM dbo.ea_monster_catalog WHERE Id = @Id", connection))
            {
                command.Parameters.Add("@Id", SqlDbType.BigInt).Value = id;
                if (command.ExecuteNonQuery() == 0)
                    throw new InvalidOperationException("삭제할 도감 데이터를 찾을 수 없습니다.");
            }
            AddAdminLog(operatorKey, "도감 데이터 삭제", null, body);
            GameContentRepository.ClearCache();
        }

        public void DeleteWeapon(string operatorKey, Dictionary<string, object> body)
        {
            var id = IntValue(body, "id");
            if (id <= 0) throw new InvalidOperationException("삭제할 무기 데이터가 필요합니다.");
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(
                @"DELETE FROM dbo.ea_weapon_catalog WHERE Id = @Id", connection))
            {
                command.Parameters.Add("@Id", SqlDbType.BigInt).Value = id;
                if (command.ExecuteNonQuery() == 0)
                    throw new InvalidOperationException("삭제할 무기 데이터를 찾을 수 없습니다.");
            }
            AddAdminLog(operatorKey, "무기 데이터 삭제", null, body);
            GameContentRepository.ClearCache();
        }
        public void UpsertEnhancementRule(string operatorKey, Dictionary<string, object> body)
        {
            var currentLevel = IntValue(body, "currentLevel");
            var cost = LongValue(body, "cost");
            var successRate = RateValue(body, "successRate");
            var keepRate = RateValue(body, "keepRate");
            var destroyRate = RateValue(body, "destroyRate");
            if (currentLevel < 0 || currentLevel > 29) throw new InvalidOperationException("강화 단계는 0~29만 사용할 수 있습니다.");
            if (cost < 0) throw new InvalidOperationException("강화 비용은 0 이상이어야 합니다.");
            if (Math.Abs((successRate + keepRate + destroyRate) - 1) > 0.0001)
                throw new InvalidOperationException("성공/유지/파괴 확률의 합은 100%여야 합니다.");

            using (var connection = OpenConnection())
            using (var command = new SqlCommand(
                @"MERGE dbo.ea_enhancement_rules AS target
                  USING (SELECT @CurrentLevel AS CurrentLevel) AS source
                  ON target.CurrentLevel = source.CurrentLevel
                  WHEN MATCHED THEN
                    UPDATE SET Cost = @Cost, SuccessRate = @SuccessRate, KeepRate = @KeepRate,
                               DestroyRate = @DestroyRate, IsEnabled = @IsEnabled, UpdatedAt = SYSDATETIMEOFFSET()
                  WHEN NOT MATCHED THEN
                    INSERT (CurrentLevel, Cost, SuccessRate, KeepRate, DestroyRate, IsEnabled, UpdatedAt)
                    VALUES (@CurrentLevel, @Cost, @SuccessRate, @KeepRate, @DestroyRate, @IsEnabled, SYSDATETIMEOFFSET());",
                connection))
            {
                command.Parameters.Add("@CurrentLevel", SqlDbType.Int).Value = currentLevel;
                command.Parameters.Add("@Cost", SqlDbType.BigInt).Value = cost;
                command.Parameters.Add("@SuccessRate", SqlDbType.Float).Value = successRate;
                command.Parameters.Add("@KeepRate", SqlDbType.Float).Value = keepRate;
                command.Parameters.Add("@DestroyRate", SqlDbType.Float).Value = destroyRate;
                command.Parameters.Add("@IsEnabled", SqlDbType.Bit).Value = BoolValue(body, "isEnabled", true);
                command.ExecuteNonQuery();
            }
            GameContentRepository.ClearCache();
            AddAdminLog(operatorKey, "강화 확률 저장", null, body);
        }

        private object GetDashboard()
        {
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(
                @"SELECT
                    (SELECT COUNT(*) FROM dbo.ea_players) AS PlayerCount,
                    (SELECT COUNT(*) FROM dbo.ea_players WHERE IsBanned = 1) AS BannedCount,
                    (SELECT COUNT(*) FROM dbo.ea_players WHERE IsOperator = 1) AS OperatorCount,
                    (SELECT COUNT(*) FROM dbo.ea_enhancement_attempts WHERE AttemptedAt >= DATEADD(day, -1, SYSDATETIMEOFFSET())) AS TodayEnhanceCount,
                    (SELECT COUNT(*) FROM dbo.ea_game_action_logs WHERE CreatedAt >= DATEADD(day, -1, SYSDATETIMEOFFSET())) AS TodayActionCount", connection))
            using (var reader = command.ExecuteReader())
            {
                reader.Read();
                return new
                {
                    playerCount = reader.GetInt32(0),
                    bannedCount = reader.GetInt32(1),
                    operatorCount = reader.GetInt32(2),
                    todayEnhanceCount = reader.GetInt32(3),
                    todayActionCount = reader.GetInt32(4)
                };
            }
        }

        // 누적 강화 이력을 단계별로 집계해 실제 결과가 설정 확률에 가까워지는지 확인합니다.
        private object GetEnhancementProof()
        {
            var rows = new List<object>();
            long totalAttempts = 0;
            long totalSuccess = 0;
            long totalKeep = 0;
            long totalDestroy = 0;

            using (var connection = OpenConnection())
            using (var command = new SqlCommand(
                @"SELECT
                    BeforeLevel,
                    COUNT_BIG(1) AS Attempts,
                    SUM(CASE WHEN LOWER(Result) = N'success' THEN 1 ELSE 0 END) AS SuccessCount,
                    SUM(CASE WHEN LOWER(Result) = N'keep' THEN 1 ELSE 0 END) AS KeepCount,
                    SUM(CASE WHEN LOWER(Result) IN (N'destroy', N'destroyed') THEN 1 ELSE 0 END) AS DestroyCount,
                    AVG(SuccessRate) AS ExpectedSuccessRate,
                    AVG(KeepRate) AS ExpectedKeepRate,
                    AVG(DestroyRate) AS ExpectedDestroyRate,
                    MAX(AttemptedAt) AS LastAttemptedAt
                  FROM dbo.ea_enhancement_attempts
                  GROUP BY BeforeLevel
                  ORDER BY BeforeLevel", connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    var attempts = reader.GetInt64(1);
                    var success = Convert.ToInt64(reader.GetValue(2));
                    var keep = Convert.ToInt64(reader.GetValue(3));
                    var destroy = Convert.ToInt64(reader.GetValue(4));
                    totalAttempts += attempts;
                    totalSuccess += success;
                    totalKeep += keep;
                    totalDestroy += destroy;
                    rows.Add(new
                    {
                        beforeLevel = reader.GetInt32(0),
                        attempts = attempts,
                        successCount = success,
                        keepCount = keep,
                        destroyCount = destroy,
                        expectedSuccessRate = reader.IsDBNull(5) ? 0 : reader.GetDouble(5),
                        expectedKeepRate = reader.IsDBNull(6) ? 0 : reader.GetDouble(6),
                        expectedDestroyRate = reader.IsDBNull(7) ? 0 : reader.GetDouble(7),
                        actualSuccessRate = attempts == 0 ? 0 : success / (double)attempts,
                        actualKeepRate = attempts == 0 ? 0 : keep / (double)attempts,
                        actualDestroyRate = attempts == 0 ? 0 : destroy / (double)attempts,
                        lastAttemptedAt = KstDateTime(reader.GetDateTimeOffset(8))
                    });
                }
            }

            return new
            {
                totalAttempts = totalAttempts,
                totalSuccess = totalSuccess,
                totalKeep = totalKeep,
                totalDestroy = totalDestroy,
                totalSuccessRate = totalAttempts == 0 ? 0 : totalSuccess / (double)totalAttempts,
                totalKeepRate = totalAttempts == 0 ? 0 : totalKeep / (double)totalAttempts,
                totalDestroyRate = totalAttempts == 0 ? 0 : totalDestroy / (double)totalAttempts,
                rows = rows
            };
        }

        private object GetSuspiciousUsers()
        {
            var rows = new List<object>();
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(
                @"SELECT TOP (100)
                    l.PlayerKey,
                    ISNULL(p.Nickname, N'') AS Nickname,
                    l.ActionType,
                    l.Message,
                    l.CreatedAt,
                    TRY_CONVERT(float, JSON_VALUE(l.BeforeStateJson, '$.Gold')) AS BeforeGold,
                    TRY_CONVERT(float, JSON_VALUE(l.AfterStateJson, '$.Gold')) AS AfterGold,
                    TRY_CONVERT(float, JSON_VALUE(l.BeforeStateJson, '$.Experience')) AS BeforeExp,
                    TRY_CONVERT(float, JSON_VALUE(l.AfterStateJson, '$.Experience')) AS AfterExp,
                    TRY_CONVERT(int, JSON_VALUE(l.AfterStateJson, '$.WeaponLevel')) AS AfterWeapon,
                    TRY_CONVERT(int, JSON_VALUE(l.AfterStateJson, '$.ProtectionTickets')) AS AfterTickets,
                    p.IsBanned,
                    COALESCE(TRY_CONVERT(float, JSON_VALUE(l.DetailsJson, '$.claimed.Gold')), 0)
                    + COALESCE(TRY_CONVERT(float, JSON_VALUE(l.DetailsJson, '$.first.Gold')), 0)
                    + COALESCE(TRY_CONVERT(float, JSON_VALUE(l.DetailsJson, '$.second.Gold')), 0) AS ExpectedGoldGain
                  FROM dbo.ea_game_action_logs l
                  LEFT JOIN dbo.ea_players p ON p.PlayerKey = l.PlayerKey
                  WHERE l.CreatedAt >= DATEADD(day, -7, SYSDATETIMEOFFSET())
                    AND (
                        TRY_CONVERT(float, JSON_VALUE(l.AfterStateJson, '$.Gold')) < 0
                        OR TRY_CONVERT(float, JSON_VALUE(l.AfterStateJson, '$.Experience')) < 0
                        OR TRY_CONVERT(int, JSON_VALUE(l.AfterStateJson, '$.WeaponLevel')) > 30
                        OR TRY_CONVERT(int, JSON_VALUE(l.AfterStateJson, '$.ProtectionTickets')) < 0
                        OR (l.ActionType = N'ManualHunt'
                            AND l.DetailsJson IS NOT NULL
                            AND TRY_CONVERT(float, JSON_VALUE(l.AfterStateJson, '$.Gold'))
                              - TRY_CONVERT(float, JSON_VALUE(l.BeforeStateJson, '$.Gold'))
                              > COALESCE(TRY_CONVERT(float, JSON_VALUE(l.DetailsJson, '$.claimed.Gold')), 0)
                                + COALESCE(TRY_CONVERT(float, JSON_VALUE(l.DetailsJson, '$.first.Gold')), 0)
                                + COALESCE(TRY_CONVERT(float, JSON_VALUE(l.DetailsJson, '$.second.Gold')), 0)
                                + 1)
                    )
                  ORDER BY l.CreatedAt DESC", connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    var reason = "규칙 위반 후보";
                    var afterGold = reader.IsDBNull(6) ? 0 : reader.GetDouble(6);
                    var beforeGold = reader.IsDBNull(5) ? 0 : reader.GetDouble(5);
                    var afterWeapon = reader.IsDBNull(9) ? 0 : reader.GetInt32(9);
                    var afterTickets = reader.IsDBNull(10) ? 0 : reader.GetInt32(10);
                    if (afterGold < 0) reason = "골드가 음수입니다.";
                    else if (afterWeapon > 30) reason = "강화도가 30을 초과했습니다.";
                    else if (afterTickets < 0) reason = "보호권이 음수입니다.";
                    else if (reader.GetString(2) == "ManualHunt")
                    {
                        var expectedGoldGain = reader.IsDBNull(12) ? 0 : reader.GetDouble(12);
                        if (afterGold - beforeGold > expectedGoldGain + 1)
                            reason = "로그에 기록된 정상 보상보다 골드 증가량이 큽니다.";
                    }

                    rows.Add(new
                    {
                        playerKey = reader.GetString(0),
                        nickname = reader.GetString(1),
                        actionType = reader.GetString(2),
                        message = reader.GetString(3),
                        createdAt = KstDateTime(reader.GetDateTimeOffset(4)),
                        reason = reason,
                        isBanned = !reader.IsDBNull(11) && reader.GetBoolean(11)
                    });
                }
            }
            return rows;
        }

        private object GetOperators()
        {
            return SearchPlayers("");
        }

        public object GetAdminLogs(string keyword, int page, int pageSize)
        {
            var rows = new List<object>();
            var totalRows = 0;
            page = Math.Max(1, page);
            pageSize = Math.Min(200, Math.Max(10, pageSize));
            var offset = (page - 1) * pageSize;
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(
                @"WITH FilteredLogs AS
                  (
                    SELECT OperatorPlayerKey, ActionType, TargetPlayerKey, DetailsJson, CreatedAt,
                           COUNT(1) OVER() AS TotalRows
                    FROM dbo.ea_admin_action_logs
                    WHERE @Keyword = N''
                       OR OperatorPlayerKey LIKE N'%' + @Keyword + N'%'
                       OR ActionType LIKE N'%' + @Keyword + N'%'
                       OR ISNULL(TargetPlayerKey, N'') LIKE N'%' + @Keyword + N'%'
                  )
                  SELECT OperatorPlayerKey, ActionType, TargetPlayerKey, DetailsJson, CreatedAt, TotalRows
                  FROM FilteredLogs
                  ORDER BY CreatedAt DESC
                  OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY", connection))
            {
                command.Parameters.Add("@Keyword", SqlDbType.NVarChar, 100).Value = (keyword ?? "").Trim();
                command.Parameters.Add("@Offset", SqlDbType.Int).Value = offset;
                command.Parameters.Add("@PageSize", SqlDbType.Int).Value = pageSize;
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (totalRows == 0) totalRows = reader.GetInt32(5);
                    rows.Add(new
                    {
                        operatorPlayerKey = reader.GetString(0),
                        actionType = reader.GetString(1),
                        targetPlayerKey = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        detailsJson = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        createdAt = KstDateTime(reader.GetDateTimeOffset(4))
                    });
                }
            }
            }
            return new { rows = rows, totalRows = totalRows, page = page, pageSize = pageSize };
        }

        private object GetMonsterCatalog()
        {
            var rows = new List<object>();
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(
                @"SELECT Id, MonsterKey, AreaId, Grade, SlotNumber, Name, Description, ImagePath, SortOrder, IsVisible
                  FROM dbo.ea_monster_catalog
                  ORDER BY AreaId, SlotNumber,
                    CASE Grade WHEN N'normal' THEN 0 WHEN N'elite' THEN 1 WHEN N'golden' THEN 2 ELSE 9 END,
                    SortOrder, Id", connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    rows.Add(new
                    {
                        id = reader.GetInt64(0),
                        monsterKey = reader.GetString(1),
                        areaId = reader.GetInt32(2),
                        grade = reader.GetString(3),
                        slotNumber = reader.GetInt32(4),
                        name = reader.GetString(5),
                        description = reader.IsDBNull(6) ? "" : reader.GetString(6),
                        imagePath = SharedMonsterImagePath(reader.GetInt32(2), reader.GetInt32(4)),
                        sortOrder = reader.GetInt32(8),
                        isVisible = reader.GetBoolean(9)
                    });
                }
            }
            return rows;
        }

        private static string SharedMonsterImagePath(int areaId, int slotNumber)
        {
            return string.Format(CultureInfo.InvariantCulture, "Content/monsters/area-{0:D2}-{1:D2}.webp", areaId, slotNumber);
        }

        private object GetWeaponCatalog()
        {
            var rows = new List<object>();
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(
                @"SELECT Id, WeaponKey, Name, Description, ImagePath, SortOrder, IsVisible
                  FROM dbo.ea_weapon_catalog
                  ORDER BY SortOrder, Id", connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    rows.Add(new
                    {
                        id = reader.GetInt64(0),
                        weaponKey = reader.GetString(1),
                        name = reader.GetString(2),
                        description = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        imagePath = reader.IsDBNull(4) ? "" : reader.GetString(4),
                        sortOrder = reader.GetInt32(5),
                        isVisible = reader.GetBoolean(6)
                    });
                }
            }
            return rows;
        }

        private object GetEnhancementRules()
        {
            var defaults = new GameCatalog().Enhancements;
            return GameContentRepository.EnhancementRules(defaults)
                .Select(rule => new
                {
                    currentLevel = rule.CurrentLevel,
                    cost = rule.Cost,
                    successRate = rule.SuccessRate,
                    keepRate = rule.KeepRate,
                    destroyRate = rule.DestroyRate,
                    isEnabled = true
                })
                .ToArray();
        }

        private object GetRiftRankings(string seasonKey)
        {
            return LoadRiftRankingRows(seasonKey)
                .Take(100)
                .Select((row, index) => new
                {
                    rank = index + 1,
                    playerKey = row.PlayerKey,
                    nickname = row.Nickname,
                    level = row.Level,
                    weaponLevel = row.WeaponLevel,
                    highestWeaponLevel = row.HighestWeaponLevel,
                    collectionCount = row.CollectionCount,
                    weeklyManualHuntCount = row.WeeklyManualHuntCount,
                    tickets = row.Tickets,
                    damage = row.Damage,
                    lastDamageAt = row.LastDamageAtUtc.HasValue ? KstDateTime(new DateTimeOffset(row.LastDamageAtUtc.Value)) : ""
                })
                .ToArray();
        }

        private object GetRiftRewardPreview(string seasonKey)
        {
            var rankings = LoadRiftRankingRows(seasonKey);
            return BuildRiftRewardRows(rankings)
                .Take(100)
                .Select(row => new
                {
                    rank = row.Rank,
                    playerKey = row.PlayerKey,
                    nickname = row.Nickname,
                    damage = row.Damage,
                    rewardCoins = row.Coins,
                    rewardTitleKey = row.TitleKey,
                    rewardBadge = row.Badge,
                    rewardGlow = row.Glow,
                    rewardLabel = row.RewardLabel
                })
                .ToArray();
        }

        private object GetRiftSeasonResults()
        {
            var results = new List<RiftSeasonResultRow>();
            using (var connection = OpenConnection())
            {
                using (var command = new SqlCommand(
                    @"SELECT TOP (5) SeasonKey, SeasonName, Mode, BossAreaId, StartsAtUtc, EndsAtUtc,
                             SettledAtUtc, TotalParticipants, TotalDamage
                      FROM dbo.ea_rift_season_results
                      ORDER BY SettledAtUtc DESC", connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        results.Add(new RiftSeasonResultRow
                        {
                            SeasonKey = reader.GetString(0),
                            SeasonName = reader.GetString(1),
                            Mode = reader.GetString(2),
                            BossAreaId = reader.GetInt32(3),
                            StartsAtUtc = reader.GetDateTimeOffset(4),
                            EndsAtUtc = reader.GetDateTimeOffset(5),
                            SettledAtUtc = reader.GetDateTimeOffset(6),
                            TotalParticipants = reader.GetInt32(7),
                            TotalDamage = reader.GetInt64(8)
                        });
                    }
                }

                return results.Select(result => new
                {
                    seasonKey = result.SeasonKey,
                    seasonName = result.SeasonName,
                    mode = result.Mode,
                    bossAreaId = result.BossAreaId,
                    startsAtKst = KstDateTime(result.StartsAtUtc),
                    endsAtKst = KstDateTime(result.EndsAtUtc),
                    settledAtKst = KstDateTime(result.SettledAtUtc),
                    totalParticipants = result.TotalParticipants,
                    totalDamage = result.TotalDamage,
                    topRankings = LoadRiftSnapshotRows(connection, result.SeasonKey)
                        .Select(row => new
                        {
                            rank = row.Rank,
                            playerKey = row.PlayerKey,
                            nickname = row.Nickname,
                            damage = row.Damage,
                            rewardCoins = row.Coins,
                            rewardTitleKey = row.TitleKey,
                            rewardBadge = row.Badge,
                            rewardGlow = row.Glow,
                            rewardLabel = RiftRewardLabel(row.Coins, row.TitleKey, row.Badge, row.Glow)
                        })
                        .ToArray()
                }).ToArray();
            }
        }

        private static List<RiftRewardPreviewRow> BuildRiftRewardRows(List<RiftRankingRow> rankings)
        {
            var rows = new List<RiftRewardPreviewRow>();
            var eligibleRankings = rankings.Where(row => row.Damage > 0).ToList();
            if (!eligibleRankings.Any()) return rows;

            var topThirtyCut = Math.Max(1, (int)Math.Ceiling(eligibleRankings.Count * .30));
            for (var index = 0; index < eligibleRankings.Count; index++)
            {
                var rank = index + 1;
                var reward = RiftRewardForRank(rank, topThirtyCut);
                rows.Add(new RiftRewardPreviewRow
                {
                    Rank = rank,
                    PlayerKey = eligibleRankings[index].PlayerKey,
                    Nickname = eligibleRankings[index].Nickname,
                    Damage = eligibleRankings[index].Damage,
                    Coins = reward.Coins,
                    TitleKey = reward.TitleKey,
                    Badge = reward.Badge,
                    Glow = reward.Glow,
                    RewardLabel = RiftRewardLabel(reward.Coins, reward.TitleKey, reward.Badge, reward.Glow),
                    TopThirtyCut = topThirtyCut
                });
            }
            return rows;
        }

        private static List<RiftSnapshotRow> LoadRiftSnapshotRows(SqlConnection connection, string seasonKey)
        {
            var rows = new List<RiftSnapshotRow>();
            using (var command = new SqlCommand(
                @"SELECT TOP (10) s.RankNo, s.PlayerKey, ISNULL(p.Nickname, N''), s.Damage,
                         s.RewardCoins, ISNULL(s.RewardTitleKey, N''), ISNULL(s.RewardBadge, N''), ISNULL(s.RewardGlow, N'')
                  FROM dbo.ea_rift_ranking_snapshots s
                  LEFT JOIN dbo.ea_players p ON p.PlayerKey = s.PlayerKey
                  WHERE s.SeasonKey = @SeasonKey
                  ORDER BY s.RankNo", connection))
            {
                command.Parameters.Add("@SeasonKey", SqlDbType.NVarChar, 40).Value = seasonKey;
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        rows.Add(new RiftSnapshotRow
                        {
                            Rank = reader.GetInt32(0),
                            PlayerKey = reader.GetString(1),
                            Nickname = reader.GetString(2),
                            Damage = reader.GetInt64(3),
                            Coins = reader.GetInt32(4),
                            TitleKey = reader.GetString(5),
                            Badge = reader.GetString(6),
                            Glow = reader.GetString(7)
                        });
                    }
                }
            }
            return rows;
        }

        private List<RiftRankingRow> LoadRiftRankingRows(string seasonKey)
        {
            var rows = new List<RiftRankingRow>();
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(
                @"SELECT PlayerKey, ISNULL(Nickname, N''), Level, WeaponLevel, HighestWeaponLevel,
                         CollectedMonsterCount, RiftWeeklyManualHuntCount, RiftTickets, RiftDamage, RiftLastDamageAtUtc
                  FROM dbo.ea_players
                  WHERE RiftSeasonKey = @SeasonKey
                    AND RiftDamage > 0
                  ORDER BY RiftDamage DESC, ISNULL(RiftLastDamageAtUtc, CreatedAt) ASC, Id ASC", connection))
            {
                command.Parameters.Add("@SeasonKey", SqlDbType.NVarChar, 40).Value = seasonKey;
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        rows.Add(new RiftRankingRow
                        {
                            PlayerKey = reader.GetString(0),
                            Nickname = reader.GetString(1),
                            Level = reader.GetInt32(2),
                            WeaponLevel = reader.GetInt32(3),
                            HighestWeaponLevel = reader.GetInt32(4),
                            CollectionCount = reader.GetInt32(5),
                            WeeklyManualHuntCount = reader.GetInt32(6),
                            Tickets = reader.GetInt32(7),
                            Damage = reader.GetInt64(8),
                            LastDamageAtUtc = reader.IsDBNull(9) ? (DateTime?)null : reader.GetDateTimeOffset(9).UtcDateTime
                        });
                    }
                }
            }
            return rows;
        }

        private static string RiftRewardLabel(int coins, string titleKey, string badge, string glow)
        {
            var parts = new List<string> { coins.ToString(CultureInfo.InvariantCulture) + " 파편" };
            if (!string.IsNullOrWhiteSpace(titleKey)) parts.Add("칭호 " + titleKey);
            if (!string.IsNullOrWhiteSpace(badge)) parts.Add("순위 표식 " + badge);
            if (!string.IsNullOrWhiteSpace(glow)) parts.Add("테두리 " + glow);
            return string.Join(" / ", parts.ToArray());
        }

        private static RiftReward RiftRewardForRank(int rank, int topThirtyCut)
        {
            if (rank == 1) return new RiftReward(120, "title-rift-ruler", "Ⅰ", "gold");
            if (rank == 2) return new RiftReward(100, "title-rift-conqueror", "Ⅱ", "silver");
            if (rank == 3) return new RiftReward(80, "title-rift-chaser", "Ⅲ", "bronze");
            if (rank <= 10) return new RiftReward(60, "title-rift-challenger", "", "");
            if (rank <= topThirtyCut) return new RiftReward(35, "", "", "");
            return new RiftReward(15, "", "", "");
        }

        private static void ValidateRiftManualRange(string startsAtUtc, string endsAtUtc, string settlementEndsAtUtc)
        {
            if (string.IsNullOrWhiteSpace(startsAtUtc)
                || string.IsNullOrWhiteSpace(endsAtUtc)
                || string.IsNullOrWhiteSpace(settlementEndsAtUtc))
                throw new InvalidOperationException("수동 테스트 시즌은 시작, 종료, 정산 종료 시간이 모두 필요합니다.");

            var start = DateTime.Parse(startsAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
            var end = DateTime.Parse(endsAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
            var settleEnd = DateTime.Parse(settlementEndsAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
            if (end <= start) throw new InvalidOperationException("균열 종료 시간은 시작 시간보다 뒤여야 합니다.");
            if (settleEnd <= end) throw new InvalidOperationException("정산 종료 시간은 균열 종료 시간보다 뒤여야 합니다.");
        }

        private void UpsertSetting(string key, string value, string operatorKey)
        {
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(
                @"MERGE dbo.ea_game_settings AS target
                  USING (SELECT @SettingKey AS SettingKey) AS source
                  ON target.SettingKey = source.SettingKey
                  WHEN MATCHED THEN
                    UPDATE SET SettingValue = @SettingValue, UpdatedByPlayerKey = @OperatorKey, UpdatedAt = SYSDATETIMEOFFSET()
                  WHEN NOT MATCHED THEN
                    INSERT (SettingKey, SettingValue, UpdatedByPlayerKey, UpdatedAt)
                    VALUES (@SettingKey, @SettingValue, @OperatorKey, SYSDATETIMEOFFSET());", connection))
            {
                command.Parameters.Add("@SettingKey", SqlDbType.NVarChar, 80).Value = key;
                command.Parameters.Add("@SettingValue", SqlDbType.NVarChar, -1).Value = value ?? "";
                command.Parameters.Add("@OperatorKey", SqlDbType.NVarChar, 100).Value = operatorKey;
                command.ExecuteNonQuery();
            }
        }

        private static void UpsertSetting(SqlConnection connection, SqlTransaction transaction, string key, string value, string operatorKey)
        {
            using (var command = new SqlCommand(
                @"MERGE dbo.ea_game_settings AS target
                  USING (SELECT @SettingKey AS SettingKey) AS source
                  ON target.SettingKey = source.SettingKey
                  WHEN MATCHED THEN
                    UPDATE SET SettingValue = @SettingValue, UpdatedByPlayerKey = @OperatorKey, UpdatedAt = SYSDATETIMEOFFSET()
                  WHEN NOT MATCHED THEN
                    INSERT (SettingKey, SettingValue, UpdatedByPlayerKey, UpdatedAt)
                    VALUES (@SettingKey, @SettingValue, @OperatorKey, SYSDATETIMEOFFSET());",
                connection, transaction))
            {
                command.Parameters.Add("@SettingKey", SqlDbType.NVarChar, 80).Value = key;
                command.Parameters.Add("@SettingValue", SqlDbType.NVarChar, -1).Value = value ?? "";
                command.Parameters.Add("@OperatorKey", SqlDbType.NVarChar, 100).Value = operatorKey;
                command.ExecuteNonQuery();
            }
        }

        private void AddAdminLog(string operatorKey, string actionType, string targetPlayerKey, object details)
        {
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(
                @"INSERT INTO dbo.ea_admin_action_logs
                  (OperatorPlayerKey, ActionType, TargetPlayerKey, DetailsJson, CreatedAt)
                  VALUES
                  (@OperatorPlayerKey, @ActionType, @TargetPlayerKey, @DetailsJson, SYSDATETIMEOFFSET())", connection))
            {
                command.Parameters.Add("@OperatorPlayerKey", SqlDbType.NVarChar, 100).Value = operatorKey;
                command.Parameters.Add("@ActionType", SqlDbType.NVarChar, 80).Value = actionType;
                command.Parameters.Add("@TargetPlayerKey", SqlDbType.NVarChar, 100).Value =
                    string.IsNullOrWhiteSpace(targetPlayerKey) ? (object)DBNull.Value : targetPlayerKey;
                command.Parameters.Add("@DetailsJson", SqlDbType.NVarChar, -1).Value =
                    details == null ? (object)DBNull.Value : Json.Serialize(details);
                command.ExecuteNonQuery();
            }
        }

        private static void AddCatalogParameters(SqlCommand command, Dictionary<string, object> body)
        {
            command.Parameters.Add("@MonsterKey", SqlDbType.NVarChar, 120).Value = TextValue(body, "monsterKey", 120);
            command.Parameters.Add("@AreaId", SqlDbType.Int).Value = IntValue(body, "areaId");
            command.Parameters.Add("@Grade", SqlDbType.NVarChar, 20).Value = TextValue(body, "grade", 20, "normal");
            command.Parameters.Add("@SlotNumber", SqlDbType.Int).Value = IntValue(body, "slotNumber");
            command.Parameters.Add("@Name", SqlDbType.NVarChar, 100).Value = TextValue(body, "name", 100, "이름 없음");
            command.Parameters.Add("@Description", SqlDbType.NVarChar, 1000).Value = NullableText(body, "description", 1000);
            command.Parameters.Add("@ImagePath", SqlDbType.NVarChar, 300).Value = NullableText(body, "imagePath", 300);
            command.Parameters.Add("@SortOrder", SqlDbType.Int).Value = IntValue(body, "sortOrder");
            command.Parameters.Add("@IsVisible", SqlDbType.Bit).Value = BoolValue(body, "isVisible", true);
        }

        private static string NormalizeKstToUtcIso(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            DateTime parsed;
            if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
                return "";
            var local = DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(local, KoreaTimeZone).ToString("o", CultureInfo.InvariantCulture);
        }

        private static void ValidateHotTimeRange(string startsAtUtc, string endsAtUtc)
        {
            if (string.IsNullOrWhiteSpace(startsAtUtc) || string.IsNullOrWhiteSpace(endsAtUtc)) return;
            var start = DateTime.Parse(startsAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
            var end = DateTime.Parse(endsAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
            if (end <= start) throw new InvalidOperationException("핫타임 종료 시간은 시작 시간보다 뒤여야 합니다.");
        }

        private static string KstLocalInput(DateTime utcValue)
        {
            var utc = DateTime.SpecifyKind(utcValue, DateTimeKind.Utc);
            return TimeZoneInfo.ConvertTimeFromUtc(utc, KoreaTimeZone).ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);
        }

        private static string KstDateTime(DateTimeOffset value)
        {
            return TimeZoneInfo.ConvertTime(value, KoreaTimeZone).ToString("yyyy-MM-dd HH:mm:ss 'KST'", CultureInfo.InvariantCulture);
        }

        private static string Iso(DateTime value)
        {
            return value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
        }

        private static double Clamp(double value, double min, double max)
        {
            return Math.Min(max, Math.Max(min, value));
        }

        private static int IntValue(Dictionary<string, object> body, string key)
        {
            int value;
            return body.ContainsKey(key) && body[key] != null && int.TryParse(body[key].ToString(), out value) ? value : 0;
        }

        private static long LongValue(Dictionary<string, object> body, string key)
        {
            long value;
            return body.ContainsKey(key) && body[key] != null && long.TryParse(body[key].ToString(), out value) ? value : 0;
        }

        private static double RateValue(Dictionary<string, object> body, string key)
        {
            double value;
            return body.ContainsKey(key) && body[key] != null && double.TryParse(body[key].ToString(), out value)
                ? Math.Min(1, Math.Max(0, value))
                : 0;
        }

        private static bool BoolValue(Dictionary<string, object> body, string key, bool fallback)
        {
            bool value;
            return body.ContainsKey(key) && body[key] != null && bool.TryParse(body[key].ToString(), out value) ? value : fallback;
        }

        private static string TextValue(Dictionary<string, object> body, string key, int maxLength, string fallback = "")
        {
            var value = body.ContainsKey(key) && body[key] != null ? body[key].ToString().Trim() : fallback;
            if (value.Length > maxLength) value = value.Substring(0, maxLength);
            return value;
        }

        private static object NullableText(Dictionary<string, object> body, string key, int maxLength)
        {
            var value = TextValue(body, key, maxLength);
            return string.IsNullOrWhiteSpace(value) ? (object)DBNull.Value : value;
        }

        private static SqlConnection OpenConnection()
        {
            var connection = new SqlConnection(ConnectionSettings.Value);
            connection.Open();
            return connection;
        }

        private sealed class RiftRankingRow
        {
            public string PlayerKey { get; set; }
            public string Nickname { get; set; }
            public int Level { get; set; }
            public int WeaponLevel { get; set; }
            public int HighestWeaponLevel { get; set; }
            public int CollectionCount { get; set; }
            public int WeeklyManualHuntCount { get; set; }
            public int Tickets { get; set; }
            public long Damage { get; set; }
            public DateTime? LastDamageAtUtc { get; set; }
        }

        private sealed class RiftRewardPreviewRow
        {
            public int Rank { get; set; }
            public string PlayerKey { get; set; }
            public string Nickname { get; set; }
            public long Damage { get; set; }
            public int Coins { get; set; }
            public string TitleKey { get; set; }
            public string Badge { get; set; }
            public string Glow { get; set; }
            public string RewardLabel { get; set; }
            public int TopThirtyCut { get; set; }
        }

        private sealed class RiftSeasonResultRow
        {
            public string SeasonKey { get; set; }
            public string SeasonName { get; set; }
            public string Mode { get; set; }
            public int BossAreaId { get; set; }
            public DateTimeOffset StartsAtUtc { get; set; }
            public DateTimeOffset EndsAtUtc { get; set; }
            public DateTimeOffset SettledAtUtc { get; set; }
            public int TotalParticipants { get; set; }
            public long TotalDamage { get; set; }
        }

        private sealed class RiftSnapshotRow
        {
            public int Rank { get; set; }
            public string PlayerKey { get; set; }
            public string Nickname { get; set; }
            public long Damage { get; set; }
            public int Coins { get; set; }
            public string TitleKey { get; set; }
            public string Badge { get; set; }
            public string Glow { get; set; }
        }

        private sealed class RiftReward
        {
            public RiftReward(int coins, string titleKey, string badge, string glow)
            {
                Coins = coins;
                TitleKey = titleKey;
                Badge = badge;
                Glow = glow;
            }

            public int Coins { get; private set; }
            public string TitleKey { get; private set; }
            public string Badge { get; private set; }
            public string Glow { get; private set; }
        }
    }
}
