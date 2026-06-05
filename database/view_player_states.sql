-- 운영 중인 플레이어 핵심 상태를 SSMS에서 바로 확인합니다.
SELECT
    Id,
    PlayerKey,
    Nickname,
    Gold,
    WeaponLevel,
    HighestWeaponLevel,
    HighestBossDefeated,
    ProtectionTickets,
    Level,
    Experience,
    DualWield,
    GoldGain,
    ExperienceGain,
    ArtisanTouch,
    AutomaticHuntUsedSeconds,
    HuntAreaId,
    ManualHuntAreaId,
    ManualHuntCount,
    ProfileMonsterKey,
    CollectedMonsterKeysJson,
    StateSchemaVersion,
    UpdatedAt
FROM dbo.ea_players
ORDER BY Level DESC, WeaponLevel DESC, HighestWeaponLevel DESC;
