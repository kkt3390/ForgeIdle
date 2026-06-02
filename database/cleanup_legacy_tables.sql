-- 이전 ASP.NET Core 테이블을 삭제하기 전에 모든 데이터가 새 구조에 존재하는지 검사합니다.
-- 검사가 하나라도 실패하면 트랜잭션을 취소하고 원본 테이블을 그대로 보존합니다.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.accounts', N'U') IS NULL
BEGIN
    PRINT N'이미 정리된 상태입니다.';
    COMMIT TRANSACTION;
    RETURN;
END;

IF NOT EXISTS (
    SELECT 1 FROM dbo.ea_legacy_migrations
    WHERE MigrationKey = N'aspnet-core-accounts-v1'
)
    THROW 51000, N'이전 사용자 데이터 이전 기록이 없습니다.', 1;

IF EXISTS (
    SELECT 1
    FROM dbo.accounts a
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.ea_players p
        WHERE p.PlayerKey = LEFT(a.Provider + N'-' + a.ExternalId, 100)
    )
    OR NOT EXISTS (
        SELECT 1 FROM dbo.ea_social_accounts s
        WHERE s.Provider = a.Provider AND s.ExternalId = a.ExternalId
    )
)
    THROW 51001, N'새 테이블에서 찾을 수 없는 이전 계정이 있습니다.', 1;

IF OBJECT_ID(N'dbo.enhancement_attempts', N'U') IS NOT NULL
   AND EXISTS (
       SELECT 1
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
       )
   )
    THROW 51002, N'새 테이블에서 찾을 수 없는 이전 강화 이력이 있습니다.', 1;

IF OBJECT_ID(N'dbo.enhancement_attempts', N'U') IS NOT NULL
    DROP TABLE dbo.enhancement_attempts;

DROP TABLE dbo.accounts;

IF NOT EXISTS (
    SELECT 1 FROM dbo.ea_legacy_migrations
    WHERE MigrationKey = N'aspnet-core-cleanup-v1'
)
BEGIN
    INSERT INTO dbo.ea_legacy_migrations (MigrationKey, MigratedAt)
    VALUES (N'aspnet-core-cleanup-v1', SYSDATETIMEOFFSET());
END;

COMMIT TRANSACTION;
PRINT N'이전 테이블 정리를 완료했습니다.';
