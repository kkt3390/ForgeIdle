using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.Script.Serialization;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EnhanceAddiction.WebForms.Game;

namespace EnhanceAddiction.WebForms.Data
{
    public sealed class PlayerRepository
    {
        // 운영 서버에서는 환경 변수를 우선 사용합니다. 기존 ForgeIdle 서버 설정도
        // 읽을 수 있게 해 두어 사이트를 교체할 때 별도 비밀값 입력이 필요 없습니다.
        private static readonly string ConnectionString = ConnectionSettings.Value;
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
        private static readonly Regex NicknamePattern = new Regex("^[가-힣A-Za-z0-9_]{2,12}$", RegexOptions.Compiled);

        public PlayerState GetOrCreate(string playerKey)
        {
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(
                "SELECT StateJson FROM dbo.ea_players WHERE PlayerKey = @PlayerKey", connection))
            {
                command.Parameters.Add("@PlayerKey", SqlDbType.NVarChar, 100).Value = playerKey;
                var stateJson = command.ExecuteScalar() as string;
                if (!string.IsNullOrWhiteSpace(stateJson))
                    return Json.Deserialize<PlayerState>(stateJson);
            }

            var player = new PlayerState();
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(
                @"INSERT INTO dbo.ea_players (PlayerKey, StateJson, CreatedAt, UpdatedAt)
                  VALUES (@PlayerKey, @StateJson, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET())", connection))
            {
                command.Parameters.Add("@PlayerKey", SqlDbType.NVarChar, 100).Value = playerKey;
                command.Parameters.Add("@StateJson", SqlDbType.NVarChar, -1).Value = Json.Serialize(player);
                command.ExecuteNonQuery();
            }
            return player;
        }

        public void Save(string playerKey, PlayerState player)
        {
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(
                @"UPDATE dbo.ea_players
                  SET StateJson = @StateJson, UpdatedAt = SYSDATETIMEOFFSET()
                  WHERE PlayerKey = @PlayerKey", connection))
            {
                command.Parameters.Add("@PlayerKey", SqlDbType.NVarChar, 100).Value = playerKey;
                command.Parameters.Add("@StateJson", SqlDbType.NVarChar, -1).Value = Json.Serialize(player);
                command.ExecuteNonQuery();
            }
        }

        public void ValidateNickname(string playerKey, string nickname)
        {
            if (string.IsNullOrWhiteSpace(nickname) || !NicknamePattern.IsMatch(nickname.Trim()))
                throw new InvalidOperationException("닉네임은 한글, 영문, 숫자, 밑줄을 사용해 2~12자로 입력하세요.");

            using (var connection = OpenConnection())
            using (var command = new SqlCommand("SELECT PlayerKey, StateJson FROM dbo.ea_players WHERE PlayerKey <> @PlayerKey", connection))
            {
                command.Parameters.Add("@PlayerKey", SqlDbType.NVarChar, 100).Value = playerKey;
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var player = Json.Deserialize<PlayerState>(reader.GetString(1));
                        if (string.Equals(player.Nickname, nickname.Trim(), StringComparison.OrdinalIgnoreCase))
                            throw new InvalidOperationException("이미 사용 중인 닉네임입니다.");
                    }
                }
            }
        }

        public IList<object> GetRankings()
        {
            var players = new List<PlayerState>();
            using (var connection = OpenConnection())
            using (var command = new SqlCommand("SELECT StateJson FROM dbo.ea_players", connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read()) players.Add(Json.Deserialize<PlayerState>(reader.GetString(0)));
            }
            return players
                .OrderByDescending(player => player.Level)
                .ThenByDescending(player => player.WeaponLevel)
                .ThenByDescending(player => player.HighestWeaponLevel)
                .ThenBy(player => player.Nickname ?? "닉네임 미설정")
                .Take(100)
                .Select((player, index) => (object)new
                {
                    rank = index + 1,
                    nickname = string.IsNullOrWhiteSpace(player.Nickname) ? "닉네임 미설정" : player.Nickname,
                    level = player.Level,
                    weaponLevel = player.WeaponLevel,
                    highestWeaponLevel = player.HighestWeaponLevel
                }).ToList();
        }

        public void AddEnhancementAttempt(string playerKey, EnhancementAttemptLog attempt)
        {
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(
                @"INSERT INTO dbo.ea_enhancement_attempts
                  (PlayerKey, BeforeLevel, AfterLevel, Cost, SuccessRate, KeepRate, DestroyRate, Roll, UsedProtection, Result, AttemptedAt)
                  VALUES
                  (@PlayerKey, @BeforeLevel, @AfterLevel, @Cost, @SuccessRate, @KeepRate, @DestroyRate, @Roll, @UsedProtection, @Result, SYSDATETIMEOFFSET())", connection))
            {
                command.Parameters.Add("@PlayerKey", SqlDbType.NVarChar, 100).Value = playerKey;
                command.Parameters.Add("@BeforeLevel", SqlDbType.Int).Value = attempt.BeforeLevel;
                command.Parameters.Add("@AfterLevel", SqlDbType.Int).Value = attempt.AfterLevel;
                command.Parameters.Add("@Cost", SqlDbType.BigInt).Value = attempt.Cost;
                command.Parameters.Add("@SuccessRate", SqlDbType.Float).Value = attempt.SuccessRate;
                command.Parameters.Add("@KeepRate", SqlDbType.Float).Value = attempt.KeepRate;
                command.Parameters.Add("@DestroyRate", SqlDbType.Float).Value = attempt.DestroyRate;
                command.Parameters.Add("@Roll", SqlDbType.Float).Value = attempt.Roll;
                command.Parameters.Add("@UsedProtection", SqlDbType.Bit).Value = attempt.UsedProtection;
                command.Parameters.Add("@Result", SqlDbType.NVarChar, 20).Value = attempt.Result;
                command.ExecuteNonQuery();
            }
        }

        public void AddGameActionLog(
            string playerKey,
            string actionType,
            bool succeeded,
            string message,
            string beforeStateJson,
            string afterStateJson,
            string detailsJson)
        {
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(
                @"INSERT INTO dbo.ea_game_action_logs
                  (PlayerKey, ActionType, Succeeded, Message, BeforeStateJson, AfterStateJson, DetailsJson, CreatedAt)
                  VALUES
                  (@PlayerKey, @ActionType, @Succeeded, @Message, @BeforeStateJson, @AfterStateJson, @DetailsJson, SYSDATETIMEOFFSET())", connection))
            {
                command.Parameters.Add("@PlayerKey", SqlDbType.NVarChar, 100).Value = playerKey;
                command.Parameters.Add("@ActionType", SqlDbType.NVarChar, 50).Value = actionType;
                command.Parameters.Add("@Succeeded", SqlDbType.Bit).Value = succeeded;
                command.Parameters.Add("@Message", SqlDbType.NVarChar, 500).Value = message;
                command.Parameters.Add("@BeforeStateJson", SqlDbType.NVarChar, -1).Value = beforeStateJson;
                command.Parameters.Add("@AfterStateJson", SqlDbType.NVarChar, -1).Value = afterStateJson;
                command.Parameters.Add("@DetailsJson", SqlDbType.NVarChar, -1).Value =
                    string.IsNullOrWhiteSpace(detailsJson) ? (object)DBNull.Value : detailsJson;
                command.ExecuteNonQuery();
            }
        }

        public string GetOrCreateSocialPlayerKey(string provider, string externalId)
        {
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(
                "SELECT PlayerKey FROM dbo.ea_social_accounts WHERE Provider = @Provider AND ExternalId = @ExternalId", connection))
            {
                command.Parameters.Add("@Provider", SqlDbType.NVarChar, 20).Value = provider;
                command.Parameters.Add("@ExternalId", SqlDbType.NVarChar, 100).Value = externalId;
                var existing = command.ExecuteScalar() as string;
                if (!string.IsNullOrWhiteSpace(existing)) return existing;
            }

            var playerKey = provider + "-" + externalId;
            GetOrCreate(playerKey);
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(
                @"IF NOT EXISTS (
                      SELECT 1 FROM dbo.ea_social_accounts WHERE Provider = @Provider AND ExternalId = @ExternalId
                  )
                  INSERT INTO dbo.ea_social_accounts (Provider, ExternalId, PlayerKey, CreatedAt)
                  VALUES (@Provider, @ExternalId, @PlayerKey, SYSDATETIMEOFFSET());
                  SELECT PlayerKey FROM dbo.ea_social_accounts WHERE Provider = @Provider AND ExternalId = @ExternalId;", connection))
            {
                command.Parameters.Add("@Provider", SqlDbType.NVarChar, 20).Value = provider;
                command.Parameters.Add("@ExternalId", SqlDbType.NVarChar, 100).Value = externalId;
                command.Parameters.Add("@PlayerKey", SqlDbType.NVarChar, 100).Value = playerKey;
                return command.ExecuteScalar().ToString();
            }
        }

        private static SqlConnection OpenConnection()
        {
            var connection = new SqlConnection(ConnectionString);
            connection.Open();
            return connection;
        }
    }
}
