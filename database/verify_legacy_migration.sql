-- 이전 ASP.NET Core 테이블과 새 ea_ 테이블의 행 수를 비교합니다.
-- 조회만 수행하므로 운영 DB에서 실행해도 데이터가 변경되지 않습니다.
IF OBJECT_ID(N'dbo.accounts', N'U') IS NOT NULL
    SELECT N'dbo.accounts' AS TableName, COUNT_BIG(*) AS TotalRows FROM dbo.accounts;
ELSE
    SELECT N'dbo.accounts' AS TableName, CAST(NULL AS bigint) AS TotalRows;

SELECT N'dbo.ea_players' AS TableName, COUNT_BIG(*) AS TotalRows FROM dbo.ea_players;
SELECT N'dbo.ea_social_accounts' AS TableName, COUNT_BIG(*) AS TotalRows FROM dbo.ea_social_accounts;

IF OBJECT_ID(N'dbo.enhancement_attempts', N'U') IS NOT NULL
    SELECT N'dbo.enhancement_attempts' AS TableName, COUNT_BIG(*) AS TotalRows FROM dbo.enhancement_attempts;
ELSE
    SELECT N'dbo.enhancement_attempts' AS TableName, CAST(NULL AS bigint) AS TotalRows;

SELECT N'dbo.ea_enhancement_attempts' AS TableName, COUNT_BIG(*) AS TotalRows FROM dbo.ea_enhancement_attempts;

IF OBJECT_ID(N'dbo.accounts', N'U') IS NOT NULL
BEGIN
    SELECT COUNT_BIG(*) AS MissingAccountMappings
    FROM dbo.accounts a
    WHERE NOT EXISTS (
        SELECT 1
        FROM dbo.ea_players p
        WHERE p.PlayerKey = LEFT(a.Provider + N'-' + a.ExternalId, 100)
    )
    OR NOT EXISTS (
        SELECT 1
        FROM dbo.ea_social_accounts s
        WHERE s.Provider = a.Provider AND s.ExternalId = a.ExternalId
    );
END;

IF OBJECT_ID(N'dbo.enhancement_attempts', N'U') IS NOT NULL
BEGIN
    SELECT COUNT_BIG(*) AS MissingEnhancementAttempts
    FROM dbo.enhancement_attempts h
    INNER JOIN dbo.accounts a ON a.AccountName = h.AccountName
    WHERE NOT EXISTS (
        SELECT 1
        FROM dbo.ea_enhancement_attempts n
        WHERE n.PlayerKey = LEFT(a.Provider + N'-' + a.ExternalId, 100)
          AND n.BeforeLevel = h.BeforeLevel
          AND n.AfterLevel = h.AfterLevel
          AND n.Cost = h.Cost
          AND n.Roll = h.Roll
          AND n.Result = h.Result
          AND n.AttemptedAt = h.AttemptedAt
    );
END;

SELECT MigrationKey, MigratedAt
FROM dbo.ea_legacy_migrations
ORDER BY MigratedAt;
