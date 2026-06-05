using System;
using System.Collections.Generic;

namespace EnhanceAddiction.WebForms.Game
{
    [Serializable]
    public sealed class PlayerState
    {
        // 자주 조회하는 상태는 MSSQL의 ea_players 일반 컬럼에 저장합니다.
        // StateJson은 최근 메시지와 이전 버전 호환을 위한 보조 사본으로 유지합니다.
        // 신규 플레이어가 처음 로그인했을 때 적용할 기본 상태를 만듭니다.
        public PlayerState()
        {
            Gold = 5000;
            HighestBossDefeated = -1;
            ProtectionTickets = 3;
            Level = 1;
            Stats = new PlayerStats();
            CollectedMonsterKeys = new List<string>();
            RecentMessages = new List<string> { "새로운 검을 받았습니다. 사냥을 시작해 골드를 모으세요." };
        }

        public string Nickname { get; set; }
        public long Gold { get; set; }
        public int WeaponLevel { get; set; }
        public int HighestWeaponLevel { get; set; }
        public int HighestBossDefeated { get; set; }
        public int ProtectionTickets { get; set; }
        public int Level { get; set; }
        public double Experience { get; set; }
        public PlayerStats Stats { get; set; }
        public int ManualHuntAreaId { get; set; }
        public int ManualHuntCount { get; set; }
        public string ProfileMonsterKey { get; set; }
        public List<string> CollectedMonsterKeys { get; set; }
        public DateTime? AutomaticHuntCycleStartedAtUtc { get; set; }
        public double AutomaticHuntUsedSeconds { get; set; }
        public HuntSession Hunt { get; set; }
        public DateTime? LastManualHuntAtUtc { get; set; }
        public List<string> RecentMessages { get; set; }
    }

    [Serializable]
    public sealed class HuntSession
    {
        // 자동 사냥 중인 사냥터와 보상 누적 시간을 저장합니다.
        public int AreaId { get; set; }
        public DateTime StartedAtUtc { get; set; }
        public DateTime RewardCapAtUtc { get; set; }
    }

    [Serializable]
    public sealed class PlayerStats
    {
        // 레벨업으로 투자한 스탯과 사용한 총 포인트를 저장합니다.
        public int DualWield { get; set; }
        public int GoldGain { get; set; }
        public int ExperienceGain { get; set; }
        public int ArtisanTouch { get; set; }
        public int SpentPoints
        {
            get { return DualWield + GoldGain + ExperienceGain + ArtisanTouch; }
        }
    }

    public sealed class GameResult
    {
        // API가 브라우저에 반환할 처리 결과와 변경된 상태를 저장합니다.
        public bool Ok { get; set; }
        public string Message { get; set; }
        public object State { get; set; }
        public object Details { get; set; }
        public EnhancementAttemptLog EnhancementAttempt { get; set; }
    }

    public sealed class EnhancementAttemptLog
    {
        // 강화 확률 검증에 필요한 시도 당시의 수치와 결과를 저장합니다.
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

    public sealed class CollectionRegistration
    {
        // 직접 사냥 도감 판정 결과를 화면 알림과 감사 로그에 전달합니다.
        public bool Registered { get; set; }
        public bool Duplicate { get; set; }
        public string MonsterKey { get; set; }
        public string MonsterName { get; set; }
        public string Grade { get; set; }
        public string ImagePath { get; set; }
    }
}
