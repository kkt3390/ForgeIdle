using System;
using System.Data.SqlClient;
using System.Globalization;

namespace EnhanceAddiction.WebForms.Data
{
    public sealed class RiftSeasonInfo
    {
        public string SeasonKey { get; set; }
        public string SeasonName { get; set; }
        public string Mode { get; set; }
        public int BossAreaId { get; set; }
        public DateTime StartsAtUtc { get; set; }
        public DateTime EndsAtUtc { get; set; }
        public DateTime SettlementEndsAtUtc { get; set; }
        public bool IsActive(DateTime nowUtc)
        {
            return StartsAtUtc <= nowUtc && nowUtc < EndsAtUtc;
        }
        public bool IsSettlement(DateTime nowUtc)
        {
            return EndsAtUtc <= nowUtc && nowUtc < SettlementEndsAtUtc;
        }
    }

    public sealed class RiftSettingsSnapshot
    {
        public bool Enabled { get; set; }
        public bool ShopEnabled { get; set; }
        public string Mode { get; set; }
        public string ManualSeasonName { get; set; }
        public DateTime? ManualStartsAtUtc { get; set; }
        public DateTime? ManualEndsAtUtc { get; set; }
        public DateTime? ManualSettlementEndsAtUtc { get; set; }
        public int ManualBossAreaId { get; set; }

        public RiftSeasonInfo CurrentSeason(DateTime nowUtc)
        {
            if (Mode == "manual"
                && ManualStartsAtUtc.HasValue
                && ManualEndsAtUtc.HasValue
                && ManualSettlementEndsAtUtc.HasValue)
            {
                return new RiftSeasonInfo
                {
                    SeasonKey = "manual-" + ManualStartsAtUtc.Value.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture),
                    SeasonName = string.IsNullOrWhiteSpace(ManualSeasonName) ? "테스트 균열" : ManualSeasonName,
                    Mode = "manual",
                    BossAreaId = ClampBossArea(ManualBossAreaId),
                    StartsAtUtc = ManualStartsAtUtc.Value,
                    EndsAtUtc = ManualEndsAtUtc.Value,
                    SettlementEndsAtUtc = ManualSettlementEndsAtUtc.Value
                };
            }

            return AutoSeason(nowUtc);
        }

        private static RiftSeasonInfo AutoSeason(DateTime nowUtc)
        {
            var utc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
            var koreaNow = TimeZoneInfo.ConvertTimeFromUtc(utc, RiftSettings.KoreaTimeZone);
            var weekStartDate = koreaNow.Date.AddDays(-((7 + (int)koreaNow.DayOfWeek - (int)DayOfWeek.Monday) % 7));
            var startsAtLocal = weekStartDate.AddMinutes(5);
            if (koreaNow < startsAtLocal) startsAtLocal = startsAtLocal.AddDays(-7);
            var endsAtLocal = startsAtLocal.Date.AddDays(6).AddHours(23).AddMinutes(55);
            var settlementEndsAtLocal = startsAtLocal.Date.AddDays(7).AddMinutes(5);
            var weeks = (int)Math.Floor((startsAtLocal.Date - new DateTime(2026, 1, 5)).TotalDays / 7d);
            var bossAreaId = ((weeks % 12) + 12) % 12;
            return new RiftSeasonInfo
            {
                SeasonKey = startsAtLocal.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
                SeasonName = startsAtLocal.ToString("yyyy년 MM월 dd일", CultureInfo.InvariantCulture) + " 주간 균열",
                Mode = "auto",
                BossAreaId = bossAreaId,
                StartsAtUtc = TimeZoneInfo.ConvertTimeToUtc(startsAtLocal, RiftSettings.KoreaTimeZone),
                EndsAtUtc = TimeZoneInfo.ConvertTimeToUtc(endsAtLocal, RiftSettings.KoreaTimeZone),
                SettlementEndsAtUtc = TimeZoneInfo.ConvertTimeToUtc(settlementEndsAtLocal, RiftSettings.KoreaTimeZone)
            };
        }

        private static int ClampBossArea(int areaId)
        {
            return Math.Min(11, Math.Max(0, areaId));
        }
    }

    public static class RiftSettings
    {
        public static readonly TimeZoneInfo KoreaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time");
        private static readonly object CacheLock = new object();
        private static RiftSettingsSnapshot cachedSettings;
        private static DateTime cachedUntilUtc;

        public static RiftSettingsSnapshot Current()
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

        private static RiftSettingsSnapshot Load()
        {
            var settings = new RiftSettingsSnapshot
            {
                Enabled = false,
                ShopEnabled = false,
                Mode = "auto",
                ManualSeasonName = "테스트 균열",
                ManualBossAreaId = 0
            };

            using (var connection = new SqlConnection(ConnectionSettings.Value))
            {
                connection.Open();
                using (var command = new SqlCommand(
                    @"SELECT SettingKey, SettingValue
                      FROM dbo.ea_game_settings
                      WHERE SettingKey LIKE N'Rift%'", connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var key = reader.GetString(0);
                        var value = reader.GetString(1);
                        if (key == "RiftEnabled") settings.Enabled = IsEnabled(value);
                        if (key == "RiftShopEnabled") settings.ShopEnabled = IsEnabled(value);
                        if (key == "RiftMode") settings.Mode = value == "manual" ? "manual" : "auto";
                        if (key == "RiftManualSeasonName") settings.ManualSeasonName = value;
                        if (key == "RiftManualStartsAtUtc") settings.ManualStartsAtUtc = ParseDate(value);
                        if (key == "RiftManualEndsAtUtc") settings.ManualEndsAtUtc = ParseDate(value);
                        if (key == "RiftManualSettlementEndsAtUtc") settings.ManualSettlementEndsAtUtc = ParseDate(value);
                        if (key == "RiftManualBossAreaId") settings.ManualBossAreaId = ParseInt(value, 0);
                    }
                }
            }

            return settings;
        }

        private static bool IsEnabled(string value)
        {
            return value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        private static int ParseInt(string value, int fallback)
        {
            int parsed;
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) ? parsed : fallback;
        }

        private static DateTime? ParseDate(string value)
        {
            DateTime parsed;
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out parsed))
                return parsed;
            return null;
        }
    }
}
