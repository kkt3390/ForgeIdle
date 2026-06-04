using System;
using System.Collections.Generic;

namespace EnhanceAddiction.WebForms.Game
{
    public sealed class GameCatalog
    {
        public const int MaxPlayerLevel = 100;
        public const int MaxStatLevel = 20;

        // 寃뚯엫 諛몃윴???섏튂?????뚯씪 ??怨녹뿉??愿由ы빀?덈떎.
        // ?щ깷?? 踰덊샇, ?대쫫, ?낆옣 媛뺥솕?? ?쒓컙??怨⑤뱶, ?쒓컙??寃쏀뿕移? ?ㅼ쓬 蹂댁뒪 ?붽뎄 媛뺥솕?? 蹂댁뒪 泥대젰 ?쒖꽌?낅땲??
        public IList<HuntingArea> Areas { get; private set; }

        // 媛뺥솕: ?꾩옱 ?④퀎, ?쒕룄 鍮꾩슜, ?깃났 ?뺣쪧, ?좎? ?뺣쪧, ?뚭눼 ?뺣쪧 ?쒖꽌?낅땲??
        // ???뺣쪧???⑹? 諛섎뱶??1?댁뼱???⑸땲??
        public IList<EnhancementRule> Enhancements { get; private set; }

        // ?щ깷?? 蹂댁뒪, 媛뺥솕 ?뺣쪧泥섎읆 ?댁쁺 以?議곗젙??諛몃윴???쒕? 珥덇린?뷀빀?덈떎.
        public GameCatalog()
        {
            Areas = new List<HuntingArea>
            {
                new HuntingArea(0, "초보자의 숲", 0, 37500, 200, 3, 460),
                new HuntingArea(1, "버려진 광산", 3, 52500, 280, 6, 1060),
                new HuntingArea(2, "깊은 동굴", 6, 75000, 400, 9, 2440),
                new HuntingArea(3, "불타는 폐허", 9, 112500, 600, 12, 5600),
                new HuntingArea(4, "얼어붙은 계곡", 12, 150000, 900, 15, 12880),
                new HuntingArea(5, "용암 지대", 15, 225000, 1300, 18, 29600),
                new HuntingArea(6, "망자의 탑", 18, 337500, 1800, 20, 51580),
                new HuntingArea(7, "심연의 입구", 20, 487500, 2500, 22, 89880),
                new HuntingArea(8, "별의 성소", 22, 675000, 3400, 23, 118640),
                new HuntingArea(9, "천공의 신전", 23, 900000, 4600, 25, 206720),
                new HuntingArea(10, "별이 잠든 유적", 25, 1200000, 6200, 27, 360180),
                new HuntingArea(11, "끝없는 균열", 27, 1575000, 8400, null, null)
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

        // ?꾩옱 媛뺥솕 ?④퀎???대떦?섎뒗 怨듦꺽?μ쓣 怨꾩궛?⑸땲??
        public int AttackPower(int level)
        {
            return (int)Math.Round(10 * Math.Pow(1.32, level));
        }

        // ?꾩옱 ?덈꺼?먯꽌 ?ㅼ쓬 ?덈꺼源뚯? ?꾩슂??寃쏀뿕移섎? 諛섑솚?⑸땲??
        public long RequiredExperience(int level)
        {
            return level >= MaxPlayerLevel ? 0 : (long)Math.Round(100 * Math.Pow(1.12, level - 1));
        }
    }

    public sealed class HuntingArea
    {
        // ?щ깷????怨녹쓽 ?낆옣 議곌굔, ?쒓컙??蹂댁긽, 愿臾?蹂댁뒪 ?뺣낫瑜?臾띠뒿?덈떎.
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
        // 媛뺥솕 ?④퀎 ??怨녹쓽 鍮꾩슜怨??깃났쨌?좎?쨌?뚭눼 ?뺣쪧??臾띠뒿?덈떎.
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
