IF DB_ID(N'enhance_addiction') IS NULL
BEGIN
    CREATE DATABASE enhance_addiction
    ON PRIMARY
    (
        NAME = N'enhance_addiction',
        FILENAME = N'D:\SqlData\enhance_addiction.mdf'
    )
    LOG ON
    (
        NAME = N'enhance_addiction_log',
        FILENAME = N'D:\SqlData\enhance_addiction_log.ldf'
    );
END;
GO

USE enhance_addiction;
GO

IF OBJECT_ID(N'dbo.ea_players', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ea_players
    (
        Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_ea_players PRIMARY KEY,
        PlayerKey nvarchar(100) NOT NULL,
        StateJson nvarchar(max) NOT NULL,
        CreatedAt datetimeoffset NOT NULL,
        UpdatedAt datetimeoffset NOT NULL
    );
    CREATE UNIQUE INDEX IX_ea_players_PlayerKey ON dbo.ea_players (PlayerKey);
END;
GO

IF OBJECT_ID(N'dbo.ea_social_accounts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ea_social_accounts
    (
        Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_ea_social_accounts PRIMARY KEY,
        Provider nvarchar(20) NOT NULL,
        ExternalId nvarchar(100) NOT NULL,
        PlayerKey nvarchar(100) NOT NULL,
        CreatedAt datetimeoffset NOT NULL
    );
    CREATE UNIQUE INDEX IX_ea_social_accounts_Provider_ExternalId ON dbo.ea_social_accounts (Provider, ExternalId);
    CREATE UNIQUE INDEX IX_ea_social_accounts_PlayerKey ON dbo.ea_social_accounts (PlayerKey);
END;
GO

IF OBJECT_ID(N'dbo.ea_enhancement_attempts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ea_enhancement_attempts
    (
        Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_ea_enhancement_attempts PRIMARY KEY,
        PlayerKey nvarchar(100) NOT NULL,
        BeforeLevel int NOT NULL,
        AfterLevel int NOT NULL,
        Cost bigint NOT NULL,
        SuccessRate float NOT NULL,
        KeepRate float NOT NULL,
        DestroyRate float NOT NULL,
        Roll float NOT NULL,
        UsedProtection bit NOT NULL,
        Result nvarchar(20) NOT NULL,
        AttemptedAt datetimeoffset NOT NULL
    );
    CREATE INDEX IX_ea_enhancement_attempts_AttemptedAt ON dbo.ea_enhancement_attempts (AttemptedAt);
    CREATE INDEX IX_ea_enhancement_attempts_BeforeLevel_Result ON dbo.ea_enhancement_attempts (BeforeLevel, Result);
END;
GO

IF OBJECT_ID(N'dbo.ea_game_action_logs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ea_game_action_logs
    (
        Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_ea_game_action_logs PRIMARY KEY,
        PlayerKey nvarchar(100) NOT NULL,
        ActionType nvarchar(50) NOT NULL,
        Succeeded bit NOT NULL,
        Message nvarchar(500) NOT NULL,
        BeforeStateJson nvarchar(max) NOT NULL,
        AfterStateJson nvarchar(max) NOT NULL,
        DetailsJson nvarchar(max) NULL,
        CreatedAt datetimeoffset NOT NULL
    );
    CREATE INDEX IX_ea_game_action_logs_PlayerKey_CreatedAt
        ON dbo.ea_game_action_logs (PlayerKey, CreatedAt DESC);
    CREATE INDEX IX_ea_game_action_logs_ActionType_CreatedAt
        ON dbo.ea_game_action_logs (ActionType, CreatedAt DESC);
END;
GO
