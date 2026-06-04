using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
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
        public int SortOrder { get; set; }
    }

    public static class GameContentRepository
    {
        private static readonly object CacheLock = new object();
        private static DateTime cachedUntilUtc;
        private static Dictionary<string, MonsterCatalogEntry> cachedMonsters;
        private static IList<WeaponCatalogEntry> cachedWeapons;
        private static IList<EnhancementRule> cachedEnhancements;

        // 관리자 수정값은 너무 자주 DB를 읽지 않도록 짧게 캐시합니다.
        public static void ClearCache()
        {
            lock (CacheLock)
            {
                cachedUntilUtc = DateTime.MinValue;
                cachedMonsters = null;
                cachedWeapons = null;
                cachedEnhancements = null;
            }
        }

        public static Dictionary<string, MonsterCatalogEntry> MonsterMap()
        {
            EnsureCache();
            return cachedMonsters;
        }

        public static WeaponCatalogEntry ActiveWeapon(int weaponLevel)
        {
            EnsureCache();
            var weapon = cachedWeapons
                .Where(row => row.SortOrder <= weaponLevel)
                .OrderByDescending(row => row.SortOrder)
                .FirstOrDefault()
                ?? cachedWeapons.OrderBy(row => row.SortOrder).FirstOrDefault();
            return weapon ?? new WeaponCatalogEntry
            {
                WeaponKey = "basic-sword",
                Name = "검",
                Description = "기본 무기",
                ImagePath = "",
                SortOrder = 0
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
                if (cachedUntilUtc > now && cachedMonsters != null && cachedWeapons != null && cachedEnhancements != null) return;
                cachedMonsters = LoadMonsters();
                cachedWeapons = LoadWeapons();
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

        private static IList<WeaponCatalogEntry> LoadWeapons()
        {
            var rows = new List<WeaponCatalogEntry>();
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(
                @"SELECT WeaponKey, Name, Description, ImagePath, SortOrder
                  FROM dbo.ea_weapon_catalog
                  WHERE IsVisible = 1
                  ORDER BY SortOrder, Id", connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    rows.Add(new WeaponCatalogEntry
                    {
                        WeaponKey = reader.GetString(0),
                        Name = reader.GetString(1),
                        Description = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        ImagePath = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        SortOrder = reader.GetInt32(4)
                    });
                }
            }
            foreach (var row in rows.Where(row => string.IsNullOrWhiteSpace(row.ImagePath)))
            {
                row.ImagePath = WeaponImagePathForSortOrder(row.SortOrder);
            }
            if (rows.Count == 0) rows = LoadWeaponFiles();
            return rows;
        }

        private static List<WeaponCatalogEntry> LoadWeaponFiles()
        {
            var directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Content", "weapons");
            if (!Directory.Exists(directory)) return new List<WeaponCatalogEntry>();
            return Directory.GetFiles(directory, "*.webp")
                .Select(path => new { Path = path, Name = Path.GetFileNameWithoutExtension(path) })
                .Select(item =>
                {
                    int sortOrder;
                    if (!int.TryParse(item.Name.Substring(0, Math.Min(3, item.Name.Length)), NumberStyles.Integer, CultureInfo.InvariantCulture, out sortOrder))
                        sortOrder = 0;
                    return new WeaponCatalogEntry
                    {
                        WeaponKey = item.Name,
                        Name = WeaponNameFromFileName(item.Name),
                        Description = "",
                        ImagePath = "Content/weapons/" + Path.GetFileName(item.Path),
                        SortOrder = sortOrder
                    };
                })
                .Where(row => row.SortOrder > 0)
                .OrderBy(row => row.SortOrder)
                .ToList();
        }

        private static string WeaponImagePathForSortOrder(int sortOrder)
        {
            if (sortOrder <= 0) return "";
            var directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Content", "weapons");
            if (!Directory.Exists(directory)) return "";
            var prefix = sortOrder.ToString("D3", CultureInfo.InvariantCulture) + "-";
            var path = Directory.GetFiles(directory, prefix + "*.webp").FirstOrDefault();
            return path == null ? "" : "Content/weapons/" + Path.GetFileName(path);
        }

        private static string WeaponNameFromFileName(string fileName)
        {
            var name = fileName.Length > 4 ? fileName.Substring(4) : fileName;
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name.Replace("-", " "));
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
