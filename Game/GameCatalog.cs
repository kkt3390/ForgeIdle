using System;
using System.Collections.Generic;

namespace EnhanceAddiction.WebForms.Game
{
    public sealed class GameCatalog
    {
        public const int MaxPlayerLevel = 100;
        public const int MaxStatLevel = 20;

        // 게임 밸런스 수치는 이 파일 한 곳에서 관리합니다.
        // 사냥터: 번호, 이름, 입장 강화도, 시간당 골드, 시간당 경험치, 다음 보스 요구 강화도, 보스 체력 순서입니다.
        public IList<HuntingArea> Areas { get; private set; }

        // 강화: 현재 단계, 시도 비용, 성공 확률, 유지 확률, 파괴 확률 순서입니다.
        // 세 확률의 합은 반드시 1이어야 합니다.
        public IList<EnhancementRule> Enhancements { get; private set; }

        // 사냥터, 보스, 강화 확률처럼 운영 중 조정할 밸런스 표를 초기화합니다.
        public GameCatalog()
        {
            Areas = new List<HuntingArea>
            {
                new HuntingArea(0, "초보자의 숲", 0, 25000, 100, 3, 460),
                new HuntingArea(1, "버려진 광산", 3, 35000, 140, 6, 1060),
                new HuntingArea(2, "깊은 동굴", 6, 50000, 200, 9, 2440),
                new HuntingArea(3, "황폐한 성터", 9, 75000, 300, 12, 5600),
                new HuntingArea(4, "얼어붙은 계곡", 12, 100000, 450, 15, 12880),
                new HuntingArea(5, "화염 지대", 15, 150000, 650, 18, 29600),
                new HuntingArea(6, "망자의 성", 18, 225000, 900, 20, 51580),
                new HuntingArea(7, "심연의 입구", 20, 325000, 1250, 22, 89880),
                new HuntingArea(8, "용의 둥지", 22, 450000, 1700, 23, 118640),
                new HuntingArea(9, "천공의 신전", 23, 600000, 2300, 25, 206720),
                new HuntingArea(10, "별이 잠든 유적", 25, 800000, 3100, 27, 360180),
                new HuntingArea(11, "끝없는 균열", 27, 1050000, 4200, null, null)
            };
            Enhancements = new List<EnhancementRule>
            {
                new EnhancementRule(0, 100, 1.00, 0, 0),
                new EnhancementRule(1, 200, 1.00, 0, 0),
                new EnhancementRule(2, 400, 1.00, 0, 0),
                new EnhancementRule(3, 700, 1.00, 0, 0),
                new EnhancementRule(4, 1000, 1.00, 0, 0),
                new EnhancementRule(5, 1800, .70, .30, 0),
                new EnhancementRule(6, 3000, .70, .30, 0),
                new EnhancementRule(7, 5000, .70, .30, 0),
                new EnhancementRule(8, 8000, .70, .30, 0),
                new EnhancementRule(9, 12000, .70, .30, 0),
                new EnhancementRule(10, 20000, .50, .50, 0),
                new EnhancementRule(11, 30000, .50, .50, 0),
                new EnhancementRule(12, 45000, .35, .65, 0),
                new EnhancementRule(13, 65000, .35, .65, 0),
                new EnhancementRule(14, 90000, .35, .65, 0),
                new EnhancementRule(15, 140000, .30, .679, .021),
                new EnhancementRule(16, 180000, .30, .679, .021),
                new EnhancementRule(17, 240000, .15, .782, .068),
                new EnhancementRule(18, 320000, .15, .782, .068),
                new EnhancementRule(19, 420000, .15, .765, .085),
                new EnhancementRule(20, 550000, .30, .595, .105),
                new EnhancementRule(21, 700000, .15, .7225, .1275),
                new EnhancementRule(22, 900000, .15, .68, .17),
                new EnhancementRule(23, 1200000, .10, .72, .18),
                new EnhancementRule(24, 1500000, .10, .72, .18),
                new EnhancementRule(25, 1900000, .10, .72, .18),
                new EnhancementRule(26, 2400000, .07, .744, .186),
                new EnhancementRule(27, 3000000, .05, .76, .19),
                new EnhancementRule(28, 4000000, .03, .776, .194),
                new EnhancementRule(29, 5000000, .01, .792, .198)
            };
        }

        // 현재 강화 단계에 해당하는 공격력을 계산합니다.
        public int AttackPower(int level)
        {
            return (int)Math.Round(10 * Math.Pow(1.32, level));
        }

        // 현재 레벨에서 다음 레벨까지 필요한 경험치를 반환합니다.
        public long RequiredExperience(int level)
        {
            return level >= MaxPlayerLevel ? 0 : (long)Math.Round(100 * Math.Pow(1.12, level - 1));
        }
    }

    public sealed class HuntingArea
    {
        // 사냥터 한 곳의 입장 조건, 시간당 보상, 관문 보스 정보를 묶습니다.
        public HuntingArea(
            int id,
            string name,
            int requiredEnhancement,
            long goldPerHour,
            long experiencePerHour,
            int? bossRequiredEnhancement,
            int? bossHealth)
        {
            Id = id;
            Name = name;
            RequiredEnhancement = requiredEnhancement;
            GoldPerHour = goldPerHour;
            ExperiencePerHour = experiencePerHour;
            BossRequiredEnhancement = bossRequiredEnhancement;
            BossHealth = bossHealth;
        }

        public int Id { get; private set; }
        public string Name { get; private set; }
        public int RequiredEnhancement { get; private set; }
        public long GoldPerHour { get; private set; }
        public long ExperiencePerHour { get; private set; }
        public int? BossRequiredEnhancement { get; private set; }
        public int? BossHealth { get; private set; }
    }

    public sealed class EnhancementRule
    {
        // 강화 단계 한 곳의 비용과 성공·유지·파괴 확률을 묶습니다.
        public EnhancementRule(
            int currentLevel,
            long cost,
            double successRate,
            double keepRate,
            double destroyRate)
        {
            CurrentLevel = currentLevel;
            Cost = cost;
            SuccessRate = successRate;
            KeepRate = keepRate;
            DestroyRate = destroyRate;
        }

        public int CurrentLevel { get; private set; }
        public long Cost { get; private set; }
        public double SuccessRate { get; private set; }
        public double KeepRate { get; private set; }
        public double DestroyRate { get; private set; }
    }
}
