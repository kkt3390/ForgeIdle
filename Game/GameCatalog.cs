namespace ForgeIdle.Game;

public sealed class GameCatalog
{
    public const int MaxPlayerLevel = 100;
    public const int MaxStatLevel = 20;

    public IReadOnlyList<HuntingArea> Areas { get; } =
    [
        new(0, "초보자의 숲", 0, 25_000, 100, 3, 460),
        new(1, "버려진 광산", 3, 35_000, 140, 6, 1_060),
        new(2, "깊은 동굴", 6, 50_000, 200, 9, 2_440),
        new(3, "황폐한 성터", 9, 75_000, 300, 12, 5_600),
        new(4, "얼어붙은 계곡", 12, 100_000, 450, 15, 12_880),
        new(5, "화염 지대", 15, 150_000, 650, 18, 29_600),
        new(6, "망자의 성", 18, 225_000, 900, 20, 51_580),
        new(7, "심연의 입구", 20, 325_000, 1_250, 22, 89_880),
        new(8, "용의 둥지", 22, 450_000, 1_700, 23, 118_640),
        new(9, "천공의 신전", 23, 600_000, 2_300, 25, 206_720),
        new(10, "별이 잠든 유적", 25, 800_000, 3_100, 27, 360_180),
        new(11, "끝없는 균열", 27, 1_050_000, 4_200, null, null)
    ];

    public IReadOnlyList<EnhancementRule> Enhancements { get; } =
    [
        new(0, 100, 1.00, 0, 0), new(1, 200, 1.00, 0, 0),
        new(2, 400, 1.00, 0, 0), new(3, 700, 1.00, 0, 0),
        new(4, 1_000, 1.00, 0, 0), new(5, 1_800, .70, .30, 0),
        new(6, 3_000, .70, .30, 0), new(7, 5_000, .70, .30, 0),
        new(8, 8_000, .70, .30, 0), new(9, 12_000, .70, .30, 0),
        new(10, 20_000, .50, .50, 0), new(11, 30_000, .50, .50, 0),
        new(12, 45_000, .35, .65, 0), new(13, 65_000, .35, .65, 0),
        new(14, 90_000, .35, .65, 0), new(15, 140_000, .30, .679, .021),
        new(16, 180_000, .30, .679, .021), new(17, 240_000, .15, .782, .068),
        new(18, 320_000, .15, .782, .068), new(19, 420_000, .15, .765, .085),
        new(20, 550_000, .30, .595, .105), new(21, 700_000, .15, .7225, .1275),
        new(22, 900_000, .15, .68, .17), new(23, 1_200_000, .10, .72, .18),
        new(24, 1_500_000, .10, .72, .18), new(25, 1_900_000, .10, .72, .18),
        new(26, 2_400_000, .07, .744, .186), new(27, 3_000_000, .05, .76, .19),
        new(28, 4_000_000, .03, .776, .194), new(29, 5_000_000, .01, .792, .198)
    ];

    public int AttackPower(int enhancementLevel) =>
        (int)Math.Round(10 * Math.Pow(1.32, enhancementLevel));

    public long RequiredExperience(int level) =>
        level >= MaxPlayerLevel ? 0 : (long)Math.Round(100 * Math.Pow(1.12, level - 1));
}

public sealed record HuntingArea(
    int Id,
    string Name,
    int RequiredEnhancement,
    long GoldPerHour,
    long ExperiencePerHour,
    int? BossRequiredEnhancement,
    int? BossHealth);

public sealed record EnhancementRule(
    int CurrentLevel,
    long Cost,
    double SuccessRate,
    double KeepRate,
    double DestroyRate);
