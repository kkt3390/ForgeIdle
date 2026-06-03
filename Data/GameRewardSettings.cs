using System;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;

namespace EnhanceAddiction.WebForms.Data
{
    public sealed class RewardMultiplierSettings
    {
        public double GoldMultiplier { get; set; }
        public double ExperienceMultiplier { get; set; }
        public bool Enabled { get; set; }
        public DateTime? StartsAtUtc { get; set; }
        public DateTime? EndsAtUtc { get; set; }

        public bool IsActive(DateTime nowUtc)
        {
            return Enabled
                && (!StartsAtUtc.HasValue || StartsAtUtc.Value <= nowUtc)
                && (!EndsAtUtc.HasValue || EndsAtUtc.Value >= nowUtc);
        }
    }

    public static class GameRewardSettings
    {
        private static readonly object CacheLock = new object();
        private static RewardMultiplierSettings cachedSettings;
        private static DateTime cachedUntilUtc;

        // 핫타임 배율은 자주 조회되므로 짧게 캐시해서 직접 사냥 연타 중 DB 부하를 줄입니다.
        public static RewardMultiplierSettings Current()
        {
            var now = DateTime.UtcNow;
            lock (CacheLock)
            {
                if (cachedSettings != null && cachedUntilUtc > now) return cachedSettings;
                cachedSettings = Load();
                cachedUntilUtc = now.AddSeconds(10);
                return cachedSettings;
            }
        }

        public static void ClearCache()
        {
            lock (CacheLock)
            {
                cachedSettings = null;
                cachedUntilUtc = DateTime.MinValue;
            }
        }

        private static RewardMultiplierSettings Load()
        {
            var settings = new RewardMultiplierSettings
            {
                GoldMultiplier = 1,
                ExperienceMultiplier = 1,
                Enabled = false
            };

            using (var connection = new SqlConnection(ConnectionSettings.Value))
            {
                connection.Open();
                using (var command = new SqlCommand(
                    @"SELECT SettingKey, SettingValue
                      FROM dbo.ea_game_settings
                      WHERE SettingKey IN
                      (N'HotTimeEnabled', N'HotTimeGoldMultiplier', N'HotTimeExperienceMultiplier', N'HotTimeStartsAtUtc', N'HotTimeEndsAtUtc')",
                    connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var key = reader.GetString(0);
                        var value = reader.GetString(1);
                        if (key == "HotTimeEnabled") settings.Enabled = value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
                        if (key == "HotTimeGoldMultiplier") settings.GoldMultiplier = ParseDouble(value, 1);
                        if (key == "HotTimeExperienceMultiplier") settings.ExperienceMultiplier = ParseDouble(value, 1);
                        if (key == "HotTimeStartsAtUtc") settings.StartsAtUtc = ParseDate(value);
                        if (key == "HotTimeEndsAtUtc") settings.EndsAtUtc = ParseDate(value);
                    }
                }
            }

            settings.GoldMultiplier = Clamp(settings.GoldMultiplier, 0.1, 20);
            settings.ExperienceMultiplier = Clamp(settings.ExperienceMultiplier, 0.1, 20);
            return settings;
        }

        private static double ParseDouble(string value, double fallback)
        {
            double parsed;
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) ? parsed : fallback;
        }

        private static DateTime? ParseDate(string value)
        {
            DateTime parsed;
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out parsed))
                return parsed;
            return null;
        }

        private static double Clamp(double value, double min, double max)
        {
            return Math.Min(max, Math.Max(min, value));
        }
    }
}
