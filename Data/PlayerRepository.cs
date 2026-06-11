using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.Script.Serialization;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using EnhanceAddiction.WebForms.Game;

namespace EnhanceAddiction.WebForms.Data
{
    public sealed class PlayerRepository
    {
        // 운영 서버에서는 환경 변수를 우선 사용합니다. 기존 ForgeIdle 서버 설정도
        // 읽을 수 있게 해 두어 사이트를 교체할 때 별도 비밀값 입력이 필요 없습니다.
        private static readonly string ConnectionString = ConnectionSettings.Value;
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
        private static readonly Regex NicknamePattern = new Regex("^[가-힣A-Za-z0-9_]{2,12}$", RegexOptions.Compiled);

        // 플레이어 상태를 일반 컬럼에서 조회하고, 처음 접속한 계정이면 기본 상태를 생성합니다.
        public PlayerState GetOrCreate(string playerKey)
        {
            PlayerState existingPlayer = null;
            var requiresColumnSync = false;
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(
                @"SELECT Nickname, Gold, WeaponLevel, HighestWeaponLevel, HighestBossDefeated,
                         ProtectionTickets, Level, Experience, DualWield, GoldGain, ExperienceGain,
                         ArtisanTouch, AutomaticHuntCycleStartedAtUtc, AutomaticHuntUsedSeconds,
                         HuntAreaId, HuntStartedAtUtc, HuntRewardCapAtUtc, LastManualHuntAtUtc,
                         ManualHuntAreaId, ManualHuntCount, CollectedMonsterKeysJson, ProfileMonsterKey,
                         RiftSeasonKey, RiftWeeklyManualHuntCount, RiftDailyManualHuntProgress, RiftTickets,
                         RiftDailyTicketDate, RiftDailyTicketsEarned, RiftDamage, RiftLastDamageAtUtc, RiftCoins,
                         ActiveTitleKey, ActiveNicknameColorKey, NicknameColorExpiresAtUtc,
                         RiftRankBadge, RiftRankGlow, RiftRankRewardExpiresAtUtc,
                         StateSchemaVersion, StateJson
                  FROM dbo.ea_players
                  WHERE PlayerKey = @PlayerKey", connection))
            {
                command.Parameters.Add("@PlayerKey", SqlDbType.NVarChar, 100).Value = playerKey;
                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read()) existingPlayer = ReadPlayer(reader, out requiresColumnSync);
                }
            }
            if (existingPlayer != null)
            {
                if (requiresColumnSync) Save(playerKey, existingPlayer);
                return existingPlayer;
            }

            var player = new PlayerState();
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(
                @"INSERT INTO dbo.ea_players
                  (PlayerKey, Nickname, Gold, WeaponLevel, HighestWeaponLevel, HighestBossDefeated,
                   ProtectionTickets, Level, Experience, DualWield, GoldGain, ExperienceGain,
                   ArtisanTouch, AutomaticHuntCycleStartedAtUtc, AutomaticHuntUsedSeconds,
                   HuntAreaId, HuntStartedAtUtc, HuntRewardCapAtUtc, LastManualHuntAtUtc,
                   ManualHuntAreaId, ManualHuntCount, CollectedMonsterKeysJson, CollectedMonsterCount,
                   ProfileMonsterKey, RiftSeasonKey, RiftWeeklyManualHuntCount, RiftDailyManualHuntProgress, RiftTickets,
                   RiftDailyTicketDate, RiftDailyTicketsEarned, RiftDamage, RiftLastDamageAtUtc, RiftCoins,
                   ActiveTitleKey, ActiveNicknameColorKey, NicknameColorExpiresAtUtc,
                   RiftRankBadge, RiftRankGlow, RiftRankRewardExpiresAtUtc,
                   LevelReachedAtUtc, HighestWeaponLevelReachedAtUtc, CollectionCountReachedAtUtc,
                   ManualHuntCountReachedAtUtc, StateJson, StateSchemaVersion, CreatedAt, UpdatedAt)
                  VALUES
                  (@PlayerKey, @Nickname, @Gold, @WeaponLevel, @HighestWeaponLevel, @HighestBossDefeated,
                   @ProtectionTickets, @Level, @Experience, @DualWield, @GoldGain, @ExperienceGain,
                   @ArtisanTouch, @AutomaticHuntCycleStartedAtUtc, @AutomaticHuntUsedSeconds,
                   @HuntAreaId, @HuntStartedAtUtc, @HuntRewardCapAtUtc, @LastManualHuntAtUtc,
                   @ManualHuntAreaId, @ManualHuntCount, @CollectedMonsterKeysJson, @CollectedMonsterCount,
                   @ProfileMonsterKey, @RiftSeasonKey, @RiftWeeklyManualHuntCount, @RiftDailyManualHuntProgress, @RiftTickets,
                   @RiftDailyTicketDate, @RiftDailyTicketsEarned, @RiftDamage, @RiftLastDamageAtUtc, @RiftCoins,
                   @ActiveTitleKey, @ActiveNicknameColorKey, @NicknameColorExpiresAtUtc,
                   @RiftRankBadge, @RiftRankGlow, @RiftRankRewardExpiresAtUtc,
                   SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(),
                   SYSDATETIMEOFFSET(), @StateJson, 1,
                   SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET())", connection))
            {
                command.Parameters.Add("@PlayerKey", SqlDbType.NVarChar, 100).Value = playerKey;
                AddPlayerParameters(command, player);
                command.ExecuteNonQuery();
            }
            return player;
        }

        // 플레이어의 최신 진행 상태를 일반 컬럼과 호환용 JSON에 함께 저장합니다.
        public void Save(string playerKey, PlayerState player)
        {
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(
                @"UPDATE dbo.ea_players
                  SET Nickname = @Nickname,
                      Gold = @Gold,
                      WeaponLevel = @WeaponLevel,
                      HighestWeaponLevel = @HighestWeaponLevel,
                      HighestBossDefeated = @HighestBossDefeated,
                      ProtectionTickets = @ProtectionTickets,
                      Level = @Level,
                      Experience = @Experience,
                      DualWield = @DualWield,
                      GoldGain = @GoldGain,
                      ExperienceGain = @ExperienceGain,
                      ArtisanTouch = @ArtisanTouch,
                      AutomaticHuntCycleStartedAtUtc = @AutomaticHuntCycleStartedAtUtc,
                      AutomaticHuntUsedSeconds = @AutomaticHuntUsedSeconds,
                      HuntAreaId = @HuntAreaId,
                      HuntStartedAtUtc = @HuntStartedAtUtc,
                      HuntRewardCapAtUtc = @HuntRewardCapAtUtc,
                      LastManualHuntAtUtc = @LastManualHuntAtUtc,
                      ManualHuntAreaId = @ManualHuntAreaId,
                      ManualHuntCount = @ManualHuntCount,
                      CollectedMonsterKeysJson = @CollectedMonsterKeysJson,
                      CollectedMonsterCount = @CollectedMonsterCount,
                      ProfileMonsterKey = @ProfileMonsterKey,
                      RiftSeasonKey = @RiftSeasonKey,
                      RiftWeeklyManualHuntCount = @RiftWeeklyManualHuntCount,
                      RiftDailyManualHuntProgress = @RiftDailyManualHuntProgress,
                      RiftTickets = @RiftTickets,
                      RiftDailyTicketDate = @RiftDailyTicketDate,
                      RiftDailyTicketsEarned = @RiftDailyTicketsEarned,
                      RiftDamage = @RiftDamage,
                      RiftLastDamageAtUtc = @RiftLastDamageAtUtc,
                      RiftCoins = @RiftCoins,
                      ActiveTitleKey = @ActiveTitleKey,
                      ActiveNicknameColorKey = @ActiveNicknameColorKey,
                      NicknameColorExpiresAtUtc = @NicknameColorExpiresAtUtc,
                      RiftRankBadge = @RiftRankBadge,
                      RiftRankGlow = @RiftRankGlow,
                      RiftRankRewardExpiresAtUtc = @RiftRankRewardExpiresAtUtc,
                      LevelReachedAtUtc = CASE
                          WHEN @Level > Level THEN SYSDATETIMEOFFSET()
                          WHEN LevelReachedAtUtc IS NULL THEN CreatedAt
                          ELSE LevelReachedAtUtc
                      END,
                      HighestWeaponLevelReachedAtUtc = CASE
                          WHEN @HighestWeaponLevel > HighestWeaponLevel THEN SYSDATETIMEOFFSET()
                          WHEN HighestWeaponLevelReachedAtUtc IS NULL THEN CreatedAt
                          ELSE HighestWeaponLevelReachedAtUtc
                      END,
                      CollectionCountReachedAtUtc = CASE
                          WHEN @CollectedMonsterCount > CollectedMonsterCount THEN SYSDATETIMEOFFSET()
                          WHEN CollectionCountReachedAtUtc IS NULL THEN CreatedAt
                          ELSE CollectionCountReachedAtUtc
                      END,
                      ManualHuntCountReachedAtUtc = CASE
                          WHEN @ManualHuntCount > ManualHuntCount THEN SYSDATETIMEOFFSET()
                          WHEN ManualHuntCountReachedAtUtc IS NULL THEN CreatedAt
                          ELSE ManualHuntCountReachedAtUtc
                      END,
                      StateJson = @StateJson,
                      StateSchemaVersion = 1,
                      UpdatedAt = SYSDATETIMEOFFSET()
                  WHERE PlayerKey = @PlayerKey", connection))
            {
                command.Parameters.Add("@PlayerKey", SqlDbType.NVarChar, 100).Value = playerKey;
                AddPlayerParameters(command, player);
                command.ExecuteNonQuery();
            }
        }

        // 닉네임 형식과 중복 여부를 확인합니다.
        public void ValidateNickname(string playerKey, string nickname)
        {
            if (string.IsNullOrWhiteSpace(nickname) || !NicknamePattern.IsMatch(nickname.Trim()))
                throw new InvalidOperationException("닉네임은 한글, 영문, 숫자, 밑줄을 사용해 2~12자로 입력하세요.");

            using (var connection = OpenConnection())
            using (var command = new SqlCommand(
                @"SELECT COUNT(*)
                  FROM dbo.ea_players
                  WHERE PlayerKey <> @PlayerKey AND Nickname = @Nickname", connection))
            {
                command.Parameters.Add("@PlayerKey", SqlDbType.NVarChar, 100).Value = playerKey;
                command.Parameters.Add("@Nickname", SqlDbType.NVarChar, 12).Value = nickname.Trim();
                if ((int)command.ExecuteScalar() > 0)
                    throw new InvalidOperationException("이미 사용 중인 닉네임입니다.");
            }
        }

        // 요청한 랭킹 종류별로 상위 100명을 반환합니다. 동률이면 먼저 달성한 사람이 위에 옵니다.
        // 새 로그인 토큰을 DB에 저장해서 같은 계정의 이전 세션을 무효화합니다.
        public void SetActiveLoginToken(string playerKey, string loginToken)
        {
            GetOrCreate(playerKey);
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(
                @"UPDATE dbo.ea_players
                  SET ActiveLoginToken = @LoginToken,
                      ActiveLoginAtUtc = SYSDATETIMEOFFSET(),
                      UpdatedAt = SYSDATETIMEOFFSET()
                  WHERE PlayerKey = @PlayerKey", connection))
            {
                command.Parameters.Add("@PlayerKey", SqlDbType.NVarChar, 100).Value = playerKey;
                command.Parameters.Add("@LoginToken", SqlDbType.NVarChar, 64).Value = loginToken;
                command.ExecuteNonQuery();
            }
        }

        // 현재 세션 토큰이 DB에 저장된 최신 로그인 토큰과 같은지 확인합니다.
        public bool IsActiveLoginToken(string playerKey, string loginToken)
        {
            if (string.IsNullOrWhiteSpace(playerKey) || string.IsNullOrWhiteSpace(loginToken)) return false;
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(
                @"SELECT ActiveLoginToken
                  FROM dbo.ea_players
                  WHERE PlayerKey = @PlayerKey", connection))
            {
                command.Parameters.Add("@PlayerKey", SqlDbType.NVarChar, 100).Value = playerKey;
                var activeToken = command.ExecuteScalar() as string;
                return string.Equals(activeToken, loginToken, StringComparison.Ordinal);
            }
        }

        // 로그아웃한 세션이 아직 최신 세션일 때만 DB 로그인 토큰을 비웁니다.
        public void ClearActiveLoginToken(string playerKey, string loginToken)
        {
            if (string.IsNullOrWhiteSpace(playerKey) || string.IsNullOrWhiteSpace(loginToken)) return;
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(
                @"UPDATE dbo.ea_players
                  SET ActiveLoginToken = NULL,
                      ActiveLoginAtUtc = NULL,
                      UpdatedAt = SYSDATETIMEOFFSET()
                  WHERE PlayerKey = @PlayerKey
                    AND ActiveLoginToken = @LoginToken", connection))
            {
                command.Parameters.Add("@PlayerKey", SqlDbType.NVarChar, 100).Value = playerKey;
                command.Parameters.Add("@LoginToken", SqlDbType.NVarChar, 64).Value = loginToken;
                command.ExecuteNonQuery();
            }
        }

        public object GetRankings(string category)
        {
            category = NormalizeRankingCategory(category);
            var rankings = new List<object>();
            var orderBy = RankingOrderBy(category);
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(
                @"SELECT TOP (100)
                         p.Nickname, p.Level, p.WeaponLevel, p.HighestWeaponLevel, p.CollectedMonsterCount, p.ManualHuntCount,
                         p.ProfileMonsterKey, m.Name, m.ImagePath, p.RiftDamage, p.RiftRankBadge, p.RiftRankGlow,
                         p.RiftRankRewardExpiresAtUtc, p.ActiveTitleKey, p.ActiveNicknameColorKey, p.NicknameColorExpiresAtUtc
                  FROM dbo.ea_players p
                  LEFT JOIN dbo.ea_monster_catalog m ON m.MonsterKey = p.ProfileMonsterKey
                  WHERE @RiftSeasonKey = N'' OR p.RiftSeasonKey = @RiftSeasonKey
                  ORDER BY " + orderBy, connection))
            {
                command.Parameters.Add("@RiftSeasonKey", SqlDbType.NVarChar, 40).Value =
                    category == "rift" ? RiftSettings.Current().CurrentSeason(DateTime.UtcNow).SeasonKey : "";
                using (var reader = command.ExecuteReader())
                {
                var rank = 1;
                while (reader.Read())
                {
                    rankings.Add(new
                    {
                        rank = rank++,
                        nickname = reader.IsDBNull(0) ? "닉네임 미설정" : reader.GetString(0),
                        level = reader.GetInt32(1),
                        weaponLevel = reader.GetInt32(2),
                        highestWeaponLevel = reader.GetInt32(3),
                        collectionCount = reader.GetInt32(4),
                        manualHuntCount = reader.GetInt32(5),
                        profileMonsterKey = reader.IsDBNull(6) ? "" : reader.GetString(6),
                        profileMonsterName = reader.IsDBNull(7) ? "" : reader.GetString(7),
                        profileMonsterImagePath = reader.IsDBNull(8) ? "" : reader.GetString(8),
                        riftDamage = reader.GetInt64(9),
                        riftRankBadge = ActiveUntil(reader, 12) ? (reader.IsDBNull(10) ? "" : reader.GetString(10)) : "",
                        riftRankGlow = ActiveUntil(reader, 12) ? (reader.IsDBNull(11) ? "" : reader.GetString(11)) : "",
                        titleKey = reader.IsDBNull(13) ? "" : reader.GetString(13),
                        nicknameColor = ActiveUntil(reader, 15) ? (reader.IsDBNull(14) ? "" : reader.GetString(14)) : ""
                    });
                }
                }
            }
            return new
            {
                category = category,
                rows = rankings
            };
        }

        // 허용된 랭킹 종류만 사용해 SQL 정렬문 조립을 안전하게 유지합니다.
        private static string NormalizeRankingCategory(string category)
        {
            category = (category ?? "level").Trim().ToLowerInvariant();
            if (category == "enhancement" || category == "collection" || category == "manualhunt" || category == "rift") return category;
            return "level";
        }

        // 랭킹 종류별 정렬 기준을 반환합니다. 같은 수치라면 달성 시각이 빠른 사람이 우선입니다.
        private static string RankingOrderBy(string category)
        {
            if (category == "enhancement")
                return "p.HighestWeaponLevel DESC, ISNULL(p.HighestWeaponLevelReachedAtUtc, p.CreatedAt) ASC, p.Id ASC";
            if (category == "collection")
                return "p.CollectedMonsterCount DESC, ISNULL(p.CollectionCountReachedAtUtc, p.CreatedAt) ASC, p.Id ASC";
            if (category == "manualhunt")
                return "p.ManualHuntCount DESC, ISNULL(p.ManualHuntCountReachedAtUtc, p.CreatedAt) ASC, p.Id ASC";
            if (category == "rift")
                return "p.RiftDamage DESC, ISNULL(p.RiftLastDamageAtUtc, p.CreatedAt) ASC, p.Id ASC";
            return "p.Level DESC, ISNULL(p.LevelReachedAtUtc, p.CreatedAt) ASC, p.Id ASC";
        }

        // 강화 확률 검증에 사용할 개별 강화 시도 이력을 저장합니다.
        public void AddEnhancementAttempt(string playerKey, EnhancementAttemptLog attempt)
        {
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(
                @"INSERT INTO dbo.ea_enhancement_attempts
                  (PlayerKey, BeforeLevel, AfterLevel, Cost, SuccessRate, KeepRate, DestroyRate, Roll, UsedProtection, Result, AttemptedAt)
                  VALUES
                  (@PlayerKey, @BeforeLevel, @AfterLevel, @Cost, @SuccessRate, @KeepRate, @DestroyRate, @Roll, @UsedProtection, @Result, SYSDATETIMEOFFSET())", connection))
            {
                command.Parameters.Add("@PlayerKey", SqlDbType.NVarChar, 100).Value = playerKey;
                command.Parameters.Add("@BeforeLevel", SqlDbType.Int).Value = attempt.BeforeLevel;
                command.Parameters.Add("@AfterLevel", SqlDbType.Int).Value = attempt.AfterLevel;
                command.Parameters.Add("@Cost", SqlDbType.BigInt).Value = attempt.Cost;
                command.Parameters.Add("@SuccessRate", SqlDbType.Float).Value = attempt.SuccessRate;
                command.Parameters.Add("@KeepRate", SqlDbType.Float).Value = attempt.KeepRate;
                command.Parameters.Add("@DestroyRate", SqlDbType.Float).Value = attempt.DestroyRate;
                command.Parameters.Add("@Roll", SqlDbType.Float).Value = attempt.Roll;
                command.Parameters.Add("@UsedProtection", SqlDbType.Bit).Value = attempt.UsedProtection;
                command.Parameters.Add("@Result", SqlDbType.NVarChar, 20).Value = attempt.Result;
                command.ExecuteNonQuery();
            }
        }

        // 게임 데이터에 영향을 주는 행동의 전후 상태와 결과를 저장합니다.
        public void AddGameActionLog(
            string playerKey,
            string actionType,
            bool succeeded,
            string message,
            string beforeStateJson,
            string afterStateJson,
            string detailsJson)
        {
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(
                @"INSERT INTO dbo.ea_game_action_logs
                  (PlayerKey, ActionType, Succeeded, Message, BeforeStateJson, AfterStateJson, DetailsJson, CreatedAt)
                  VALUES
                  (@PlayerKey, @ActionType, @Succeeded, @Message, @BeforeStateJson, @AfterStateJson, @DetailsJson, SYSDATETIMEOFFSET())", connection))
            {
                command.Parameters.Add("@PlayerKey", SqlDbType.NVarChar, 100).Value = playerKey;
                command.Parameters.Add("@ActionType", SqlDbType.NVarChar, 50).Value = actionType;
                command.Parameters.Add("@Succeeded", SqlDbType.Bit).Value = succeeded;
                command.Parameters.Add("@Message", SqlDbType.NVarChar, 500).Value = message;
                command.Parameters.Add("@BeforeStateJson", SqlDbType.NVarChar, -1).Value = beforeStateJson;
                command.Parameters.Add("@AfterStateJson", SqlDbType.NVarChar, -1).Value = afterStateJson;
                command.Parameters.Add("@DetailsJson", SqlDbType.NVarChar, -1).Value =
                    string.IsNullOrWhiteSpace(detailsJson) ? (object)DBNull.Value : detailsJson;
                command.ExecuteNonQuery();
            }
        }

        // 소셜 로그인 계정을 내부 플레이어 키와 연결하고 처음이면 새 연결을 만듭니다.
        public string GetOrCreateSocialPlayerKey(string provider, string externalId)
        {
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(
                "SELECT PlayerKey FROM dbo.ea_social_accounts WHERE Provider = @Provider AND ExternalId = @ExternalId", connection))
            {
                command.Parameters.Add("@Provider", SqlDbType.NVarChar, 20).Value = provider;
                command.Parameters.Add("@ExternalId", SqlDbType.NVarChar, 100).Value = externalId;
                var existing = command.ExecuteScalar() as string;
                if (!string.IsNullOrWhiteSpace(existing)) return existing;
            }

            var playerKey = provider + "-" + externalId;
            GetOrCreate(playerKey);
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(
                @"IF NOT EXISTS (
                      SELECT 1 FROM dbo.ea_social_accounts WHERE Provider = @Provider AND ExternalId = @ExternalId
                  )
                  INSERT INTO dbo.ea_social_accounts (Provider, ExternalId, PlayerKey, CreatedAt)
                  VALUES (@Provider, @ExternalId, @PlayerKey, SYSDATETIMEOFFSET());
                  SELECT PlayerKey FROM dbo.ea_social_accounts WHERE Provider = @Provider AND ExternalId = @ExternalId;", connection))
            {
                command.Parameters.Add("@Provider", SqlDbType.NVarChar, 20).Value = provider;
                command.Parameters.Add("@ExternalId", SqlDbType.NVarChar, 100).Value = externalId;
                command.Parameters.Add("@PlayerKey", SqlDbType.NVarChar, 100).Value = playerKey;
                return command.ExecuteScalar().ToString();
            }
        }

        // DB에서 읽은 일반 컬럼을 게임에서 사용하는 플레이어 상태 객체로 조립합니다.
        private static PlayerState ReadPlayer(SqlDataReader reader, out bool requiresColumnSync)
        {
            var stateSchemaVersion = reader.GetInt32(37);
            var stateJson = reader.IsDBNull(38) ? null : reader.GetString(38);
            var player = string.IsNullOrWhiteSpace(stateJson)
                ? new PlayerState()
                : Json.Deserialize<PlayerState>(stateJson);
            requiresColumnSync = stateSchemaVersion < 1;
            if (requiresColumnSync) return player;

            player.Nickname = reader.IsDBNull(0) ? null : reader.GetString(0);
            player.Gold = reader.GetInt64(1);
            player.WeaponLevel = reader.GetInt32(2);
            player.HighestWeaponLevel = reader.GetInt32(3);
            player.HighestBossDefeated = reader.GetInt32(4);
            player.ProtectionTickets = reader.GetInt32(5);
            player.Level = reader.GetInt32(6);
            player.Experience = reader.GetDouble(7);
            player.Stats = new PlayerStats
            {
                DualWield = reader.GetInt32(8),
                GoldGain = reader.GetInt32(9),
                ExperienceGain = reader.GetInt32(10),
                ArtisanTouch = reader.GetInt32(11)
            };
            player.AutomaticHuntCycleStartedAtUtc = ReadNullableDateTime(reader, 12);
            player.AutomaticHuntUsedSeconds = reader.GetDouble(13);
            player.Hunt = ReadHuntSession(reader, 14, 15, 16);
            player.LastManualHuntAtUtc = ReadNullableDateTime(reader, 17);
            player.ManualHuntAreaId = reader.GetInt32(18);
            player.ManualHuntCount = reader.GetInt32(19);
            player.CollectedMonsterKeys = reader.IsDBNull(20)
                ? new List<string>()
                : Json.Deserialize<List<string>>(reader.GetString(20));
            player.ProfileMonsterKey = reader.IsDBNull(21) ? null : reader.GetString(21);
            player.RiftSeasonKey = reader.IsDBNull(22) ? "" : reader.GetString(22);
            player.RiftWeeklyManualHuntCount = reader.GetInt32(23);
            player.RiftDailyManualHuntProgress = reader.GetInt32(24);
            player.RiftTickets = reader.GetInt32(25);
            player.RiftDailyTicketDate = reader.IsDBNull(26) ? "" : reader.GetString(26);
            player.RiftDailyTicketsEarned = reader.GetInt32(27);
            player.RiftDamage = reader.GetInt64(28);
            player.RiftLastDamageAtUtc = ReadNullableDateTime(reader, 29);
            player.RiftCoins = reader.GetInt32(30);
            player.ActiveTitleKey = reader.IsDBNull(31) ? "" : reader.GetString(31);
            player.ActiveNicknameColorKey = reader.IsDBNull(32) ? "" : reader.GetString(32);
            player.NicknameColorExpiresAtUtc = ReadNullableDateTime(reader, 33);
            player.RiftRankBadge = reader.IsDBNull(34) ? "" : reader.GetString(34);
            player.RiftRankGlow = reader.IsDBNull(35) ? "" : reader.GetString(35);
            player.RiftRankRewardExpiresAtUtc = ReadNullableDateTime(reader, 36);
            return player;
        }

        // 플레이어 상태를 저장 쿼리에서 재사용할 SQL 매개 변수로 변환합니다.
        private static void AddPlayerParameters(SqlCommand command, PlayerState player)
        {
            var stats = player.Stats ?? new PlayerStats();
            command.Parameters.Add("@Nickname", SqlDbType.NVarChar, 12).Value =
                string.IsNullOrWhiteSpace(player.Nickname) ? (object)DBNull.Value : player.Nickname.Trim();
            command.Parameters.Add("@Gold", SqlDbType.BigInt).Value = player.Gold;
            command.Parameters.Add("@WeaponLevel", SqlDbType.Int).Value = player.WeaponLevel;
            command.Parameters.Add("@HighestWeaponLevel", SqlDbType.Int).Value = player.HighestWeaponLevel;
            command.Parameters.Add("@HighestBossDefeated", SqlDbType.Int).Value = player.HighestBossDefeated;
            command.Parameters.Add("@ProtectionTickets", SqlDbType.Int).Value = player.ProtectionTickets;
            command.Parameters.Add("@Level", SqlDbType.Int).Value = player.Level;
            command.Parameters.Add("@Experience", SqlDbType.Float).Value = player.Experience;
            command.Parameters.Add("@DualWield", SqlDbType.Int).Value = stats.DualWield;
            command.Parameters.Add("@GoldGain", SqlDbType.Int).Value = stats.GoldGain;
            command.Parameters.Add("@ExperienceGain", SqlDbType.Int).Value = stats.ExperienceGain;
            command.Parameters.Add("@ArtisanTouch", SqlDbType.Int).Value = stats.ArtisanTouch;
            command.Parameters.Add("@AutomaticHuntCycleStartedAtUtc", SqlDbType.DateTimeOffset).Value =
                player.AutomaticHuntCycleStartedAtUtc.HasValue
                    ? (object)player.AutomaticHuntCycleStartedAtUtc.Value
                    : DBNull.Value;
            command.Parameters.Add("@AutomaticHuntUsedSeconds", SqlDbType.Float).Value =
                player.AutomaticHuntUsedSeconds;
            command.Parameters.Add("@HuntAreaId", SqlDbType.Int).Value =
                player.Hunt == null ? (object)DBNull.Value : player.Hunt.AreaId;
            command.Parameters.Add("@HuntStartedAtUtc", SqlDbType.DateTimeOffset).Value =
                player.Hunt == null ? (object)DBNull.Value : player.Hunt.StartedAtUtc;
            command.Parameters.Add("@HuntRewardCapAtUtc", SqlDbType.DateTimeOffset).Value =
                player.Hunt == null ? (object)DBNull.Value : player.Hunt.RewardCapAtUtc;
            command.Parameters.Add("@LastManualHuntAtUtc", SqlDbType.DateTimeOffset).Value =
                player.LastManualHuntAtUtc.HasValue ? (object)player.LastManualHuntAtUtc.Value : DBNull.Value;
            command.Parameters.Add("@ManualHuntAreaId", SqlDbType.Int).Value = player.ManualHuntAreaId;
            command.Parameters.Add("@ManualHuntCount", SqlDbType.Int).Value = player.ManualHuntCount;
            command.Parameters.Add("@CollectedMonsterCount", SqlDbType.Int).Value =
                player.CollectedMonsterKeys == null ? 0 : player.CollectedMonsterKeys.Count;
            command.Parameters.Add("@CollectedMonsterKeysJson", SqlDbType.NVarChar, -1).Value =
                Json.Serialize(player.CollectedMonsterKeys ?? new List<string>());
            command.Parameters.Add("@ProfileMonsterKey", SqlDbType.NVarChar, 120).Value =
                string.IsNullOrWhiteSpace(player.ProfileMonsterKey) ? (object)DBNull.Value : player.ProfileMonsterKey;
            command.Parameters.Add("@RiftSeasonKey", SqlDbType.NVarChar, 40).Value =
                string.IsNullOrWhiteSpace(player.RiftSeasonKey) ? "" : player.RiftSeasonKey;
            command.Parameters.Add("@RiftWeeklyManualHuntCount", SqlDbType.Int).Value = player.RiftWeeklyManualHuntCount;
            command.Parameters.Add("@RiftDailyManualHuntProgress", SqlDbType.Int).Value = player.RiftDailyManualHuntProgress;
            command.Parameters.Add("@RiftTickets", SqlDbType.Int).Value = player.RiftTickets;
            command.Parameters.Add("@RiftDailyTicketDate", SqlDbType.NVarChar, 10).Value =
                string.IsNullOrWhiteSpace(player.RiftDailyTicketDate) ? "" : player.RiftDailyTicketDate;
            command.Parameters.Add("@RiftDailyTicketsEarned", SqlDbType.Int).Value = player.RiftDailyTicketsEarned;
            command.Parameters.Add("@RiftDamage", SqlDbType.BigInt).Value = player.RiftDamage;
            command.Parameters.Add("@RiftLastDamageAtUtc", SqlDbType.DateTimeOffset).Value =
                player.RiftLastDamageAtUtc.HasValue ? (object)player.RiftLastDamageAtUtc.Value : DBNull.Value;
            command.Parameters.Add("@RiftCoins", SqlDbType.Int).Value = player.RiftCoins;
            command.Parameters.Add("@ActiveTitleKey", SqlDbType.NVarChar, 80).Value =
                string.IsNullOrWhiteSpace(player.ActiveTitleKey) ? "" : player.ActiveTitleKey;
            command.Parameters.Add("@ActiveNicknameColorKey", SqlDbType.NVarChar, 80).Value =
                string.IsNullOrWhiteSpace(player.ActiveNicknameColorKey) ? "" : player.ActiveNicknameColorKey;
            command.Parameters.Add("@NicknameColorExpiresAtUtc", SqlDbType.DateTimeOffset).Value =
                player.NicknameColorExpiresAtUtc.HasValue ? (object)player.NicknameColorExpiresAtUtc.Value : DBNull.Value;
            command.Parameters.Add("@RiftRankBadge", SqlDbType.NVarChar, 20).Value =
                string.IsNullOrWhiteSpace(player.RiftRankBadge) ? "" : player.RiftRankBadge;
            command.Parameters.Add("@RiftRankGlow", SqlDbType.NVarChar, 20).Value =
                string.IsNullOrWhiteSpace(player.RiftRankGlow) ? "" : player.RiftRankGlow;
            command.Parameters.Add("@RiftRankRewardExpiresAtUtc", SqlDbType.DateTimeOffset).Value =
                player.RiftRankRewardExpiresAtUtc.HasValue ? (object)player.RiftRankRewardExpiresAtUtc.Value : DBNull.Value;
            command.Parameters.Add("@StateJson", SqlDbType.NVarChar, -1).Value = Json.Serialize(player);
        }

        // DB의 NULL 가능 시간 컬럼을 플레이어 상태에서 사용하는 값으로 읽습니다.
        private static bool ActiveUntil(SqlDataReader reader, int ordinal)
        {
            return !reader.IsDBNull(ordinal) && reader.GetDateTimeOffset(ordinal).UtcDateTime > DateTime.UtcNow;
        }

        private static DateTime? ReadNullableDateTime(SqlDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal) ? (DateTime?)null : reader.GetDateTimeOffset(ordinal).UtcDateTime;
        }

        // 자동 사냥 컬럼이 채워진 경우에만 자동 사냥 상태를 조립합니다.
        private static HuntSession ReadHuntSession(
            SqlDataReader reader,
            int areaOrdinal,
            int startedAtOrdinal,
            int rewardCapAtOrdinal)
        {
            if (reader.IsDBNull(areaOrdinal) || reader.IsDBNull(startedAtOrdinal) || reader.IsDBNull(rewardCapAtOrdinal))
                return null;

            return new HuntSession
            {
                AreaId = reader.GetInt32(areaOrdinal),
                StartedAtUtc = reader.GetDateTimeOffset(startedAtOrdinal).UtcDateTime,
                RewardCapAtUtc = reader.GetDateTimeOffset(rewardCapAtOrdinal).UtcDateTime
            };
        }

        // 현재 환경에 맞는 MSSQL 연결을 열어 반환합니다.
        private static SqlConnection OpenConnection()
        {
            var connection = new SqlConnection(ConnectionString);
            connection.Open();
            return connection;
        }
    }
}
