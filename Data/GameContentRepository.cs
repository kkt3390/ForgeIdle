using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using EnhanceAddiction.WebForms.Game;

namespace EnhanceAddiction.WebForms.Data
{
    public sealed class MonsterCatalogEntry
    {
        public string MonsterKey { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string ImagePath { get; set; }
    }

    public sealed class WeaponCatalogEntry
    {
        public string WeaponKey { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string ImagePath { get; set; }
    }

    public static class GameContentRepository
    {
        private static readonly object CacheLock = new object();
        private static DateTime cachedUntilUtc;
        private static Dictionary<string, MonsterCatalogEntry> cachedMonsters;
        private static WeaponCatalogEntry cachedWeapon;
        private static IList<EnhancementRule> cachedEnhancements;

        // 관리자 수정값은 너무 자주 DB를 읽지 않도록 짧게 캐시합니다.
        public static void ClearCache()
        {
            lock (CacheLock)
            {
                cachedUntilUtc = DateTime.MinValue;
                cachedMonsters = null;
                cachedWeapon = null;
                cachedEnhancements = null;
            }
        }

        public static Dictionary<string, MonsterCatalogEntry> MonsterMap()
        {
            EnsureCache();
            return cachedMonsters;
        }

        public static WeaponCatalogEntry ActiveWeapon()
        {
            EnsureCache();
            return cachedWeapon ?? new WeaponCatalogEntry
            {
                WeaponKey = "basic-sword",
                Name = "검",
                Description = "기본 무기",
                ImagePath = ""
            };
        }

        public static IList<EnhancementRule> EnhancementRules(IList<EnhancementRule> defaults)
        {
            EnsureCache();
            if (cachedEnhancements == null || cachedEnhancements.Count == 0) return defaults;

            var merged = defaults.ToDictionary(rule => rule.CurrentLevel, rule => rule);
            foreach (var rule in cachedEnhancements)
            {
                merged[rule.CurrentLevel] = rule;
            }
            return merged.OrderBy(pair => pair.Key).Select(pair => pair.Value).ToList();
        }

        private static void EnsureCache()
        {
            var now = DateTime.UtcNow;
            lock (CacheLock)
            {
                if (cachedUntilUtc > now && cachedMonsters != null && cachedWeapon != null && cachedEnhancements != null) return;
                cachedMonsters = LoadMonsters();
                cachedWeapon = LoadWeapon();
                cachedEnhancements = LoadEnhancements();
                cachedUntilUtc = now.AddSeconds(10);
            }
        }

        private static Dictionary<string, MonsterCatalogEntry> LoadMonsters()
        {
            var rows = new Dictionary<string, MonsterCatalogEntry>(StringComparer.OrdinalIgnoreCase);
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(
                @"SELECT MonsterKey, Name, Description, ImagePath
                  FROM dbo.ea_monster_catalog
                  WHERE IsVisible = 1", connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    rows[reader.GetString(0)] = new MonsterCatalogEntry
                    {
                        MonsterKey = reader.GetString(0),
                        Name = reader.GetString(1),
                        Description = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        ImagePath = reader.IsDBNull(3) ? "" : reader.GetString(3)
                    };
                }
            }
            return rows;
        }

        private static WeaponCatalogEntry LoadWeapon()
        {
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(
                @"SELECT TOP (1) WeaponKey, Name, Description, ImagePath
                  FROM dbo.ea_weapon_catalog
                  WHERE IsVisible = 1
                  ORDER BY SortOrder, Id", connection))
            using (var reader = command.ExecuteReader())
            {
                if (!reader.Read()) return null;
                return new WeaponCatalogEntry
                {
                    WeaponKey = reader.GetString(0),
                    Name = reader.GetString(1),
                    Description = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    ImagePath = reader.IsDBNull(3) ? "" : reader.GetString(3)
                };
            }
        }

        private static IList<EnhancementRule> LoadEnhancements()
        {
            var rows = new List<EnhancementRule>();
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(
                @"SELECT CurrentLevel, Cost, SuccessRate, KeepRate, DestroyRate
                  FROM dbo.ea_enhancement_rules
                  WHERE IsEnabled = 1
                  ORDER BY CurrentLevel", connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    rows.Add(new EnhancementRule(
                        reader.GetInt32(0),
                        reader.GetInt64(1),
                        reader.GetDouble(2),
                        reader.GetDouble(3),
                        reader.GetDouble(4)));
                }
            }
            return rows;
        }

        private static SqlConnection OpenConnection()
        {
            var connection = new SqlConnection(ConnectionSettings.Value);
            connection.Open();
            return connection;
        }
    }
}
