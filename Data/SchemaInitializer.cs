using System;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;

namespace EnhanceAddiction.WebForms.Data
{
    public static class SchemaInitializer
    {
        // 서버 시작 시 필요한 테이블을 만들고 이전 버전 데이터를 안전하게 이전합니다.
        public static void EnsureCreated()
        {
            const string sql = @"
IF OBJECT_ID(N'dbo.ea_players', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ea_players (
        Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_ea_players PRIMARY KEY,
        PlayerKey nvarchar(100) NOT NULL,
        Nickname nvarchar(12) NULL,
        Gold bigint NOT NULL CONSTRAINT DF_ea_players_Gold DEFAULT (5000),
        WeaponLevel int NOT NULL CONSTRAINT DF_ea_players_WeaponLevel DEFAULT (0),
        HighestWeaponLevel int NOT NULL CONSTRAINT DF_ea_players_HighestWeaponLevel DEFAULT (0),
        HighestBossDefeated int NOT NULL CONSTRAINT DF_ea_players_HighestBossDefeated DEFAULT (-1),
        ProtectionTickets int NOT NULL CONSTRAINT DF_ea_players_ProtectionTickets DEFAULT (3),
        Level int NOT NULL CONSTRAINT DF_ea_players_Level DEFAULT (1),
        Experience float NOT NULL CONSTRAINT DF_ea_players_Experience DEFAULT (0),
        DualWield int NOT NULL CONSTRAINT DF_ea_players_DualWield DEFAULT (0),
        GoldGain int NOT NULL CONSTRAINT DF_ea_players_GoldGain DEFAULT (0),
        ExperienceGain int NOT NULL CONSTRAINT DF_ea_players_ExperienceGain DEFAULT (0),
        ArtisanTouch int NOT NULL CONSTRAINT DF_ea_players_ArtisanTouch DEFAULT (0),
        AutomaticHuntCycleStartedAtUtc datetimeoffset NULL,
        AutomaticHuntUsedSeconds float NOT NULL CONSTRAINT DF_ea_players_AutomaticHuntUsedSeconds DEFAULT (0),
        HuntAreaId int NULL,
        HuntStartedAtUtc datetimeoffset NULL,
        HuntRewardCapAtUtc datetimeoffset NULL,
        LastManualHuntAtUtc datetimeoffset NULL,
        ManualHuntAreaId int NOT NULL CONSTRAINT DF_ea_players_ManualHuntAreaId DEFAULT (0),
        CollectedMonsterKeysJson nvarchar(max) NOT NULL CONSTRAINT DF_ea_players_CollectedMonsterKeysJson DEFAULT (N'[]'),
        CollectedMonsterCount int NOT NULL CONSTRAINT DF_ea_players_CollectedMonsterCount DEFAULT (0),
        LevelReachedAtUtc datetimeoffset NULL,
        HighestWeaponLevelReachedAtUtc datetimeoffset NULL,
        CollectionCountReachedAtUtc datetimeoffset NULL,
        IsOperator bit NOT NULL CONSTRAINT DF_ea_players_IsOperator DEFAULT (0),
        IsBanned bit NOT NULL CONSTRAINT DF_ea_players_IsBanned DEFAULT (0),
        BanReason nvarchar(500) NULL,
        BannedAtUtc datetimeoffset NULL,
        ActiveLoginToken nvarchar(64) NULL,
        ActiveLoginAtUtc datetimeoffset NULL,
        StateJson nvarchar(max) NOT NULL,
        StateSchemaVersion int NOT NULL CONSTRAINT DF_ea_players_StateSchemaVersion DEFAULT (1),
        CreatedAt datetimeoffset NOT NULL,
        UpdatedAt datetimeoffset NOT NULL
    );
    CREATE UNIQUE INDEX IX_ea_players_PlayerKey ON dbo.ea_players (PlayerKey);
END;

-- 기존 JSON 중심 테이블에는 조회와 통계에 자주 쓰는 일반 컬럼을 추가합니다.
IF COL_LENGTH(N'dbo.ea_players', N'Nickname') IS NULL
    ALTER TABLE dbo.ea_players ADD Nickname nvarchar(12) NULL;
IF COL_LENGTH(N'dbo.ea_players', N'Gold') IS NULL
    ALTER TABLE dbo.ea_players ADD Gold bigint NOT NULL CONSTRAINT DF_ea_players_Gold DEFAULT (5000) WITH VALUES;
IF COL_LENGTH(N'dbo.ea_players', N'WeaponLevel') IS NULL
    ALTER TABLE dbo.ea_players ADD WeaponLevel int NOT NULL CONSTRAINT DF_ea_players_WeaponLevel DEFAULT (0) WITH VALUES;
IF COL_LENGTH(N'dbo.ea_players', N'HighestWeaponLevel') IS NULL
    ALTER TABLE dbo.ea_players ADD HighestWeaponLevel int NOT NULL CONSTRAINT DF_ea_players_HighestWeaponLevel DEFAULT (0) WITH VALUES;
IF COL_LENGTH(N'dbo.ea_players', N'HighestBossDefeated') IS NULL
    ALTER TABLE dbo.ea_players ADD HighestBossDefeated int NOT NULL CONSTRAINT DF_ea_players_HighestBossDefeated DEFAULT (-1) WITH VALUES;
IF COL_LENGTH(N'dbo.ea_players', N'ProtectionTickets') IS NULL
    ALTER TABLE dbo.ea_players ADD ProtectionTickets int NOT NULL CONSTRAINT DF_ea_players_ProtectionTickets DEFAULT (3) WITH VALUES;
IF COL_LENGTH(N'dbo.ea_players', N'Level') IS NULL
    ALTER TABLE dbo.ea_players ADD Level int NOT NULL CONSTRAINT DF_ea_players_Level DEFAULT (1) WITH VALUES;
IF COL_LENGTH(N'dbo.ea_players', N'Experience') IS NULL
    ALTER TABLE dbo.ea_players ADD Experience float NOT NULL CONSTRAINT DF_ea_players_Experience DEFAULT (0) WITH VALUES;
ELSE IF EXISTS (
    SELECT 1
    FROM sys.columns c
    INNER JOIN sys.types t ON t.user_type_id = c.user_type_id
    WHERE c.object_id = OBJECT_ID(N'dbo.ea_players')
      AND c.name = N'Experience'
      AND t.name <> N'float'
)
BEGIN
    IF OBJECT_ID(N'dbo.DF_ea_players_Experience', N'D') IS NOT NULL
        ALTER TABLE dbo.ea_players DROP CONSTRAINT DF_ea_players_Experience;
    ALTER TABLE dbo.ea_players ALTER COLUMN Experience float NOT NULL;
    IF OBJECT_ID(N'dbo.DF_ea_players_Experience', N'D') IS NULL
        ALTER TABLE dbo.ea_players ADD CONSTRAINT DF_ea_players_Experience DEFAULT (0) FOR Experience;
END;
IF COL_LENGTH(N'dbo.ea_players', N'DualWield') IS NULL
    ALTER TABLE dbo.ea_players ADD DualWield int NOT NULL CONSTRAINT DF_ea_players_DualWield DEFAULT (0) WITH VALUES;
IF COL_LENGTH(N'dbo.ea_players', N'GoldGain') IS NULL
    ALTER TABLE dbo.ea_players ADD GoldGain int NOT NULL CONSTRAINT DF_ea_players_GoldGain DEFAULT (0) WITH VALUES;
IF COL_LENGTH(N'dbo.ea_players', N'ExperienceGain') IS NULL
    ALTER TABLE dbo.ea_players ADD ExperienceGain int NOT NULL CONSTRAINT DF_ea_players_ExperienceGain DEFAULT (0) WITH VALUES;
IF COL_LENGTH(N'dbo.ea_players', N'ArtisanTouch') IS NULL
    ALTER TABLE dbo.ea_players ADD ArtisanTouch int NOT NULL CONSTRAINT DF_ea_players_ArtisanTouch DEFAULT (0) WITH VALUES;
IF COL_LENGTH(N'dbo.ea_players', N'AutomaticHuntCycleStartedAtUtc') IS NULL
    ALTER TABLE dbo.ea_players ADD AutomaticHuntCycleStartedAtUtc datetimeoffset NULL;
IF COL_LENGTH(N'dbo.ea_players', N'AutomaticHuntUsedSeconds') IS NULL
    ALTER TABLE dbo.ea_players ADD AutomaticHuntUsedSeconds float NOT NULL CONSTRAINT DF_ea_players_AutomaticHuntUsedSeconds DEFAULT (0) WITH VALUES;
IF COL_LENGTH(N'dbo.ea_players', N'HuntAreaId') IS NULL
    ALTER TABLE dbo.ea_players ADD HuntAreaId int NULL;
IF COL_LENGTH(N'dbo.ea_players', N'HuntStartedAtUtc') IS NULL
    ALTER TABLE dbo.ea_players ADD HuntStartedAtUtc datetimeoffset NULL;
IF COL_LENGTH(N'dbo.ea_players', N'HuntRewardCapAtUtc') IS NULL
    ALTER TABLE dbo.ea_players ADD HuntRewardCapAtUtc datetimeoffset NULL;
IF COL_LENGTH(N'dbo.ea_players', N'LastManualHuntAtUtc') IS NULL
    ALTER TABLE dbo.ea_players ADD LastManualHuntAtUtc datetimeoffset NULL;
IF COL_LENGTH(N'dbo.ea_players', N'ManualHuntAreaId') IS NULL
    ALTER TABLE dbo.ea_players ADD ManualHuntAreaId int NOT NULL CONSTRAINT DF_ea_players_ManualHuntAreaId DEFAULT (0) WITH VALUES;
IF COL_LENGTH(N'dbo.ea_players', N'CollectedMonsterKeysJson') IS NULL
    ALTER TABLE dbo.ea_players ADD CollectedMonsterKeysJson nvarchar(max) NOT NULL CONSTRAINT DF_ea_players_CollectedMonsterKeysJson DEFAULT (N'[]') WITH VALUES;
IF COL_LENGTH(N'dbo.ea_players', N'CollectedMonsterCount') IS NULL
    ALTER TABLE dbo.ea_players ADD CollectedMonsterCount int NOT NULL CONSTRAINT DF_ea_players_CollectedMonsterCount DEFAULT (0) WITH VALUES;
IF COL_LENGTH(N'dbo.ea_players', N'LevelReachedAtUtc') IS NULL
    ALTER TABLE dbo.ea_players ADD LevelReachedAtUtc datetimeoffset NULL;
IF COL_LENGTH(N'dbo.ea_players', N'HighestWeaponLevelReachedAtUtc') IS NULL
    ALTER TABLE dbo.ea_players ADD HighestWeaponLevelReachedAtUtc datetimeoffset NULL;
IF COL_LENGTH(N'dbo.ea_players', N'CollectionCountReachedAtUtc') IS NULL
    ALTER TABLE dbo.ea_players ADD CollectionCountReachedAtUtc datetimeoffset NULL;
IF COL_LENGTH(N'dbo.ea_players', N'IsOperator') IS NULL
    ALTER TABLE dbo.ea_players ADD IsOperator bit NOT NULL CONSTRAINT DF_ea_players_IsOperator DEFAULT (0) WITH VALUES;
IF COL_LENGTH(N'dbo.ea_players', N'IsBanned') IS NULL
    ALTER TABLE dbo.ea_players ADD IsBanned bit NOT NULL CONSTRAINT DF_ea_players_IsBanned DEFAULT (0) WITH VALUES;
IF COL_LENGTH(N'dbo.ea_players', N'BanReason') IS NULL
    ALTER TABLE dbo.ea_players ADD BanReason nvarchar(500) NULL;
IF COL_LENGTH(N'dbo.ea_players', N'BannedAtUtc') IS NULL
    ALTER TABLE dbo.ea_players ADD BannedAtUtc datetimeoffset NULL;
IF COL_LENGTH(N'dbo.ea_players', N'ActiveLoginToken') IS NULL
    ALTER TABLE dbo.ea_players ADD ActiveLoginToken nvarchar(64) NULL;
IF COL_LENGTH(N'dbo.ea_players', N'ActiveLoginAtUtc') IS NULL
    ALTER TABLE dbo.ea_players ADD ActiveLoginAtUtc datetimeoffset NULL;
IF COL_LENGTH(N'dbo.ea_players', N'StateSchemaVersion') IS NULL
    ALTER TABLE dbo.ea_players ADD StateSchemaVersion int NOT NULL CONSTRAINT DF_ea_players_StateSchemaVersion DEFAULT (0) WITH VALUES;

IF OBJECT_ID(N'dbo.ea_social_accounts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ea_social_accounts (
        Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_ea_social_accounts PRIMARY KEY,
        Provider nvarchar(20) NOT NULL,
        ExternalId nvarchar(100) NOT NULL,
        PlayerKey nvarchar(100) NOT NULL,
        CreatedAt datetimeoffset NOT NULL
    );
    CREATE UNIQUE INDEX IX_ea_social_accounts_Provider_ExternalId ON dbo.ea_social_accounts (Provider, ExternalId);
    CREATE UNIQUE INDEX IX_ea_social_accounts_PlayerKey ON dbo.ea_social_accounts (PlayerKey);
END;
IF OBJECT_ID(N'dbo.ea_enhancement_attempts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ea_enhancement_attempts (
        Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_ea_enhancement_attempts PRIMARY KEY,
        PlayerKey nvarchar(100) NOT NULL, BeforeLevel int NOT NULL, AfterLevel int NOT NULL,
        Cost bigint NOT NULL, SuccessRate float NOT NULL, KeepRate float NOT NULL,
        DestroyRate float NOT NULL, Roll float NOT NULL, UsedProtection bit NOT NULL,
        Result nvarchar(20) NOT NULL, AttemptedAt datetimeoffset NOT NULL
    );
    CREATE INDEX IX_ea_enhancement_attempts_AttemptedAt ON dbo.ea_enhancement_attempts (AttemptedAt);
    CREATE INDEX IX_ea_enhancement_attempts_BeforeLevel_Result ON dbo.ea_enhancement_attempts (BeforeLevel, Result);
END;
IF OBJECT_ID(N'dbo.ea_game_action_logs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ea_game_action_logs (
        Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_ea_game_action_logs PRIMARY KEY,
        PlayerKey nvarchar(100) NOT NULL, ActionType nvarchar(50) NOT NULL,
        Succeeded bit NOT NULL, Message nvarchar(500) NOT NULL,
        BeforeStateJson nvarchar(max) NOT NULL, AfterStateJson nvarchar(max) NOT NULL,
        DetailsJson nvarchar(max) NULL, CreatedAt datetimeoffset NOT NULL
    );
    CREATE INDEX IX_ea_game_action_logs_PlayerKey_CreatedAt ON dbo.ea_game_action_logs (PlayerKey, CreatedAt DESC);
    CREATE INDEX IX_ea_game_action_logs_ActionType_CreatedAt ON dbo.ea_game_action_logs (ActionType, CreatedAt DESC);
END;
IF OBJECT_ID(N'dbo.ea_admin_action_logs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ea_admin_action_logs (
        Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_ea_admin_action_logs PRIMARY KEY,
        OperatorPlayerKey nvarchar(100) NOT NULL,
        ActionType nvarchar(80) NOT NULL,
        TargetPlayerKey nvarchar(100) NULL,
        DetailsJson nvarchar(max) NULL,
        CreatedAt datetimeoffset NOT NULL
    );
    CREATE INDEX IX_ea_admin_action_logs_CreatedAt ON dbo.ea_admin_action_logs (CreatedAt DESC);
    CREATE INDEX IX_ea_admin_action_logs_TargetPlayerKey ON dbo.ea_admin_action_logs (TargetPlayerKey);
END;
IF OBJECT_ID(N'dbo.ea_game_settings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ea_game_settings (
        SettingKey nvarchar(80) NOT NULL CONSTRAINT PK_ea_game_settings PRIMARY KEY,
        SettingValue nvarchar(max) NOT NULL,
        UpdatedByPlayerKey nvarchar(100) NULL,
        UpdatedAt datetimeoffset NOT NULL
    );
END;
IF OBJECT_ID(N'dbo.ea_monster_catalog', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ea_monster_catalog (
        Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_ea_monster_catalog PRIMARY KEY,
        MonsterKey nvarchar(120) NOT NULL,
        AreaId int NOT NULL,
        Grade nvarchar(20) NOT NULL,
        SlotNumber int NOT NULL,
        Name nvarchar(100) NOT NULL,
        Description nvarchar(1000) NULL,
        ImagePath nvarchar(300) NULL,
        SortOrder int NOT NULL CONSTRAINT DF_ea_monster_catalog_SortOrder DEFAULT (0),
        IsVisible bit NOT NULL CONSTRAINT DF_ea_monster_catalog_IsVisible DEFAULT (1),
        UpdatedAt datetimeoffset NOT NULL
    );
    CREATE UNIQUE INDEX IX_ea_monster_catalog_MonsterKey ON dbo.ea_monster_catalog (MonsterKey);
END;
IF OBJECT_ID(N'dbo.ea_weapon_catalog', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ea_weapon_catalog (
        Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_ea_weapon_catalog PRIMARY KEY,
        WeaponKey nvarchar(120) NOT NULL,
        Name nvarchar(100) NOT NULL,
        Description nvarchar(1000) NULL,
        ImagePath nvarchar(300) NULL,
        SortOrder int NOT NULL CONSTRAINT DF_ea_weapon_catalog_SortOrder DEFAULT (0),
        IsVisible bit NOT NULL CONSTRAINT DF_ea_weapon_catalog_IsVisible DEFAULT (1),
        UpdatedAt datetimeoffset NOT NULL
    );
    CREATE UNIQUE INDEX IX_ea_weapon_catalog_WeaponKey ON dbo.ea_weapon_catalog (WeaponKey);
END;
IF OBJECT_ID(N'dbo.ea_enhancement_rules', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ea_enhancement_rules (
        CurrentLevel int NOT NULL CONSTRAINT PK_ea_enhancement_rules PRIMARY KEY,
        Cost bigint NOT NULL,
        SuccessRate float NOT NULL,
        KeepRate float NOT NULL,
        DestroyRate float NOT NULL,
        IsEnabled bit NOT NULL CONSTRAINT DF_ea_enhancement_rules_IsEnabled DEFAULT (1),
        UpdatedAt datetimeoffset NOT NULL
    );
END;
IF OBJECT_ID(N'dbo.ea_legacy_migrations', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ea_legacy_migrations (
        MigrationKey nvarchar(100) NOT NULL CONSTRAINT PK_ea_legacy_migrations PRIMARY KEY,
        MigratedAt datetimeoffset NOT NULL
    );
END;

-- 이전 ASP.NET Core 버전의 계정과 강화 이력을 한 번만 옮깁니다.
-- 원본 테이블은 삭제하지 않아 문제가 생기면 이전 상태를 다시 확인할 수 있습니다.
IF OBJECT_ID(N'dbo.accounts', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM dbo.ea_legacy_migrations WHERE MigrationKey = N'aspnet-core-accounts-v1')
BEGIN
    INSERT INTO dbo.ea_players (PlayerKey, StateJson, CreatedAt, UpdatedAt)
    SELECT LEFT(a.Provider + N'-' + a.ExternalId, 100), a.StateJson, a.CreatedAt, a.UpdatedAt
    FROM dbo.accounts a
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.ea_players p
        WHERE p.PlayerKey = LEFT(a.Provider + N'-' + a.ExternalId, 100)
    );

    INSERT INTO dbo.ea_social_accounts (Provider, ExternalId, PlayerKey, CreatedAt)
    SELECT a.Provider, a.ExternalId, LEFT(a.Provider + N'-' + a.ExternalId, 100), a.CreatedAt
    FROM dbo.accounts a
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.ea_social_accounts s
        WHERE s.Provider = a.Provider AND s.ExternalId = a.ExternalId
    );

    IF OBJECT_ID(N'dbo.enhancement_attempts', N'U') IS NOT NULL
    BEGIN
        INSERT INTO dbo.ea_enhancement_attempts
            (PlayerKey, BeforeLevel, AfterLevel, Cost, SuccessRate, KeepRate, DestroyRate, Roll, UsedProtection, Result, AttemptedAt)
        SELECT LEFT(a.Provider + N'-' + a.ExternalId, 100),
               h.BeforeLevel, h.AfterLevel, h.Cost,
               h.AppliedSuccessRate, h.AppliedKeepRate, h.AppliedDestroyRate,
               h.Roll, h.UsedProtection, h.Result, h.AttemptedAt
        FROM dbo.enhancement_attempts h
        INNER JOIN dbo.accounts a ON a.AccountName = h.AccountName;
    END;

    INSERT INTO dbo.ea_legacy_migrations (MigrationKey, MigratedAt)
    VALUES (N'aspnet-core-accounts-v1', SYSDATETIMEOFFSET());
END;";

            // ALTER TABLE이 끝난 다음 별도 명령으로 실행해 SQL Server의 사전 컴파일 충돌을 피합니다.
            const string backfillSql = @"
UPDATE dbo.ea_players
SET Nickname = JSON_VALUE(StateJson, N'$.Nickname'),
    Gold = COALESCE(TRY_CONVERT(bigint, JSON_VALUE(StateJson, N'$.Gold')), Gold),
    WeaponLevel = COALESCE(TRY_CONVERT(int, JSON_VALUE(StateJson, N'$.WeaponLevel')), WeaponLevel),
    HighestWeaponLevel = COALESCE(TRY_CONVERT(int, JSON_VALUE(StateJson, N'$.HighestWeaponLevel')), HighestWeaponLevel),
    HighestBossDefeated = COALESCE(TRY_CONVERT(int, JSON_VALUE(StateJson, N'$.HighestBossDefeated')), HighestBossDefeated),
    ProtectionTickets = COALESCE(TRY_CONVERT(int, JSON_VALUE(StateJson, N'$.ProtectionTickets')), ProtectionTickets),
    Level = COALESCE(TRY_CONVERT(int, JSON_VALUE(StateJson, N'$.Level')), Level),
    Experience = COALESCE(TRY_CONVERT(float, JSON_VALUE(StateJson, N'$.Experience')), Experience),
    DualWield = COALESCE(TRY_CONVERT(int, JSON_VALUE(StateJson, N'$.Stats.DualWield')), DualWield),
    GoldGain = COALESCE(TRY_CONVERT(int, JSON_VALUE(StateJson, N'$.Stats.GoldGain')), GoldGain),
    ExperienceGain = COALESCE(TRY_CONVERT(int, JSON_VALUE(StateJson, N'$.Stats.ExperienceGain')), ExperienceGain),
    ArtisanTouch = COALESCE(TRY_CONVERT(int, JSON_VALUE(StateJson, N'$.Stats.ArtisanTouch')), ArtisanTouch),
    CollectedMonsterKeysJson = COALESCE(JSON_QUERY(StateJson, N'$.CollectedMonsterKeys'), CollectedMonsterKeysJson)
WHERE StateSchemaVersion = 0;";
            const string rankingBackfillSql = @"
;WITH CollectionCounts AS (
    SELECT p.Id, COUNT(j.[value]) AS TotalCount
    FROM dbo.ea_players p
    OUTER APPLY OPENJSON(CASE WHEN ISJSON(p.CollectedMonsterKeysJson) = 1 THEN p.CollectedMonsterKeysJson ELSE N'[]' END) j
    GROUP BY p.Id
)
UPDATE p
SET CollectedMonsterCount = c.TotalCount,
    LevelReachedAtUtc = ISNULL(p.LevelReachedAtUtc, p.CreatedAt),
    HighestWeaponLevelReachedAtUtc = ISNULL(p.HighestWeaponLevelReachedAtUtc, p.CreatedAt),
    CollectionCountReachedAtUtc = ISNULL(p.CollectionCountReachedAtUtc, p.CreatedAt)
FROM dbo.ea_players p
INNER JOIN CollectionCounts c ON c.Id = p.Id;";
            using (var connection = new SqlConnection(ConnectionSettings.Value))
            {
                connection.Open();
                using (var schemaCommand = new SqlCommand(sql, connection))
                {
                    schemaCommand.ExecuteNonQuery();
                }
                using (var backfillCommand = new SqlCommand(backfillSql, connection))
                {
                    backfillCommand.ExecuteNonQuery();
                }
                using (var rankingBackfillCommand = new SqlCommand(rankingBackfillSql, connection))
                {
                    rankingBackfillCommand.ExecuteNonQuery();
                }

                SeedMonsterCatalog(connection);
            }
        }

        // 원본 몬스터 120종을 일반/정예/황금 3등급 도감 항목으로 등록합니다.
        private static void SeedMonsterCatalog(SqlConnection connection)
        {
            const string migrationKey = "monster-catalog-texts-v3";
            using (var checkCommand = new SqlCommand(
                "SELECT COUNT(1) FROM dbo.ea_legacy_migrations WHERE MigrationKey = @MigrationKey",
                connection))
            {
                checkCommand.Parameters.AddWithValue("@MigrationKey", migrationKey);
                if ((int)checkCommand.ExecuteScalar() > 0) return;
            }

            var manifestPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Content",
                "monsters",
                "monster-manifest.tsv");
            if (!File.Exists(manifestPath)) return;

            var grades = new[] { "normal", "elite", "golden" };
            var now = DateTimeOffset.UtcNow;
            foreach (var line in File.ReadAllLines(manifestPath))
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("AreaId\t", StringComparison.OrdinalIgnoreCase))
                    continue;

                var parts = line.Split('\t');
                if (parts.Length < 3) continue;
                int areaId;
                int slotNumber;
                if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out areaId)) continue;
                if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out slotNumber)) continue;

                var name = parts[2].Trim();
                var description = parts.Length >= 4 && !string.IsNullOrWhiteSpace(parts[3])
                    ? parts[3].Trim()
                    : "도감 몬스터";
                foreach (var grade in grades)
                {
                    var monsterKey = string.Format(CultureInfo.InvariantCulture, "area-{0:D2}-{1}-{2:D2}", areaId, grade, slotNumber);
                    var imagePath = string.Format(CultureInfo.InvariantCulture, "Content/monsters/area-{0:D2}-{1:D2}.webp", areaId, slotNumber);
                    var sortOrder = areaId * 1000 + slotNumber * 10 + GradeSortOrder(grade);
                    using (var command = new SqlCommand(@"
IF NOT EXISTS (SELECT 1 FROM dbo.ea_monster_catalog WHERE MonsterKey = @MonsterKey)
BEGIN
    INSERT INTO dbo.ea_monster_catalog
        (MonsterKey, AreaId, Grade, SlotNumber, Name, Description, ImagePath, SortOrder, IsVisible, UpdatedAt)
    VALUES
        (@MonsterKey, @AreaId, @Grade, @SlotNumber, @Name, @Description, @ImagePath, @SortOrder, 1, @UpdatedAt);
END
ELSE
BEGIN
    UPDATE dbo.ea_monster_catalog
    SET Name = @Name,
        Description = @Description,
        ImagePath = @ImagePath,
        SortOrder = @SortOrder,
        UpdatedAt = @UpdatedAt
    WHERE MonsterKey = @MonsterKey;
END;", connection))
                    {
                        command.Parameters.AddWithValue("@MonsterKey", monsterKey);
                        command.Parameters.AddWithValue("@AreaId", areaId);
                        command.Parameters.AddWithValue("@Grade", grade);
                        command.Parameters.AddWithValue("@SlotNumber", slotNumber);
                        command.Parameters.AddWithValue("@Name", name);
                        command.Parameters.AddWithValue("@Description", description);
                        command.Parameters.AddWithValue("@ImagePath", imagePath);
                        command.Parameters.AddWithValue("@SortOrder", sortOrder);
                        command.Parameters.AddWithValue("@UpdatedAt", now);
                        command.ExecuteNonQuery();
                    }
                }
            }

            using (var migrationCommand = new SqlCommand(@"
IF NOT EXISTS (SELECT 1 FROM dbo.ea_legacy_migrations WHERE MigrationKey = @MigrationKey)
BEGIN
    INSERT INTO dbo.ea_legacy_migrations (MigrationKey, MigratedAt)
    VALUES (@MigrationKey, SYSDATETIMEOFFSET());
END;", connection))
            {
                migrationCommand.Parameters.AddWithValue("@MigrationKey", migrationKey);
                migrationCommand.ExecuteNonQuery();
            }
        }

        // 등급별 정렬 순서를 고정해 도감과 관리자 화면이 같은 순서로 표시되게 합니다.
        private static int GradeSortOrder(string grade)
        {
            if (grade == "elite") return 1;
            if (grade == "golden") return 2;
            return 0;
        }

        // DB 설명 문구에 사용할 등급명을 반환합니다.
        private static string GradeDisplayName(string grade)
        {
            if (grade == "elite") return "정예";
            if (grade == "golden") return "황금";
            return "일반";
        }
    }
}
