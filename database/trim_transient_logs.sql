/*
게임 플레이 상태에 영향 없는 누적 로그 정리용 스크립트입니다.
- ea_players, ea_social_accounts, 카탈로그, 설정, 시즌 결과는 삭제하지 않습니다.
- 기본 보관 기간: 게임 행동 로그 3일, 강화 시도 3일, 운영자 로그 1일
- 삭제 후 실제 MDF 파일 크기까지 줄여야 하면 맨 아래 DBCC SHRINKDATABASE 줄을 직접 실행하세요.
*/

SET NOCOUNT ON;

DECLARE @GameActionRetentionDays int = 3;
DECLARE @EnhancementAttemptRetentionDays int = 3;
DECLARE @AdminLogRetentionDays int = 1;
DECLARE @Rows int = 1;
DECLARE @DeletedGameActionLogs bigint = 0;
DECLARE @DeletedEnhancementAttempts bigint = 0;
DECLARE @DeletedAdminLogs bigint = 0;

WHILE @Rows > 0
BEGIN
    DELETE TOP (5000)
    FROM dbo.ea_game_action_logs
    WHERE CreatedAt < DATEADD(day, -@GameActionRetentionDays, SYSDATETIMEOFFSET());

    SET @Rows = @@ROWCOUNT;
    SET @DeletedGameActionLogs += @Rows;
END;

SET @Rows = 1;
WHILE @Rows > 0
BEGIN
    DELETE TOP (5000)
    FROM dbo.ea_enhancement_attempts
    WHERE AttemptedAt < DATEADD(day, -@EnhancementAttemptRetentionDays, SYSDATETIMEOFFSET());

    SET @Rows = @@ROWCOUNT;
    SET @DeletedEnhancementAttempts += @Rows;
END;

SET @Rows = 1;
WHILE @Rows > 0
BEGIN
    DELETE TOP (5000)
    FROM dbo.ea_admin_action_logs
    WHERE CreatedAt < DATEADD(day, -@AdminLogRetentionDays, SYSDATETIMEOFFSET());

    SET @Rows = @@ROWCOUNT;
    SET @DeletedAdminLogs += @Rows;
END;

IF OBJECT_ID(N'dbo.game_action_logs', N'U') IS NOT NULL
BEGIN
    TRUNCATE TABLE dbo.game_action_logs;
END;

SELECT
    @DeletedGameActionLogs AS DeletedGameActionLogs,
    @DeletedEnhancementAttempts AS DeletedEnhancementAttempts,
    @DeletedAdminLogs AS DeletedAdminLogs;

SELECT
    t.name AS TableName,
    SUM(p.rows) AS Rows,
    CAST(SUM(a.total_pages) * 8.0 / 1024 AS decimal(18, 2)) AS TotalMB,
    CAST(SUM(a.data_pages) * 8.0 / 1024 AS decimal(18, 2)) AS DataMB
FROM sys.tables t
INNER JOIN sys.indexes i ON t.object_id = i.object_id
INNER JOIN sys.partitions p ON i.object_id = p.object_id AND i.index_id = p.index_id
INNER JOIN sys.allocation_units a ON p.partition_id = a.container_id
WHERE i.index_id <= 1
GROUP BY t.name
ORDER BY TotalMB DESC;

-- 운영 DB 파일 자체 크기까지 즉시 줄여야 할 때만 별도 실행하세요.
-- DBCC SHRINKDATABASE (N'enhance_addiction');
