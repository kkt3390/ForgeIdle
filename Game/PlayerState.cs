using System;
using System.Collections.Generic;

namespace EnhanceAddiction.WebForms.Game
{
    [Serializable]
    public sealed class PlayerState
    {
        // 사용자별 상태는 MSSQL의 players.StateJson 컬럼에 JSON으로 저장합니다.
        // 새 필드는 기본값을 지정해 기존 사용자 JSON과 호환되도록 유지하세요.
        public PlayerState()
        {
            Gold = 5000;
            HighestBossDefeated = -1;
            ProtectionTickets = 3;
            Level = 1;
            Stats = new PlayerStats();
            RecentMessages = new List<string> { "새로운 검을 받았습니다. 사냥을 시작해 골드를 모으세요." };
        }

        public string Nickname { get; set; }
        public long Gold { get; set; }
        public int WeaponLevel { get; set; }
        public int HighestWeaponLevel { get; set; }
        public int HighestBossDefeated { get; set; }
        public int ProtectionTickets { get; set; }
        public int Level { get; set; }
        public long Experience { get; set; }
        public PlayerStats Stats { get; set; }
        public DateTime? AutomaticHuntCycleStartedAtUtc { get; set; }
        public double AutomaticHuntUsedSeconds { get; set; }
        public HuntSession Hunt { get; set; }
        public DateTime? LastManualHuntAtUtc { get; set; }
        public List<string> RecentMessages { get; set; }
    }

    [Serializable]
    public sealed class HuntSession
    {
        public int AreaId { get; set; }
        public DateTime StartedAtUtc { get; set; }
        public DateTime RewardCapAtUtc { get; set; }
    }

    [Serializable]
    public sealed class PlayerStats
    {
        public int DualWield { get; set; }
        public int GoldGain { get; set; }
        public int ExperienceGain { get; set; }
        public int ArtisanTouch { get; set; }
        public int SpentPoints { get { return DualWield + GoldGain + ExperienceGain + ArtisanTouch; } }
    }

    public sealed class GameResult
    {
        public bool Ok { get; set; }
        public string Message { get; set; }
        public object State { get; set; }
        public object Details { get; set; }
        public EnhancementAttemptLog EnhancementAttempt { get; set; }
    }

    public sealed class EnhancementAttemptLog
    {
        public int BeforeLevel { get; set; }
        public int AfterLevel { get; set; }
        public long Cost { get; set; }
        public double SuccessRate { get; set; }
        public double KeepRate { get; set; }
        public double DestroyRate { get; set; }
        public double Roll { get; set; }
        public bool UsedProtection { get; set; }
        public string Result { get; set; }
    }
}
