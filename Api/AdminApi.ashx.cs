using System;
using System.Collections.Generic;
using System.IO;
using System.Web;
using System.Web.Script.Serialization;
using System.Web.SessionState;
using EnhanceAddiction.WebForms.Data;

namespace EnhanceAddiction.WebForms.Api
{
    public sealed class AdminApi : IHttpHandler, IRequiresSessionState
    {
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

        public bool IsReusable { get { return false; } }

        // 관리자 API는 모든 요청마다 DB에서 운영자 권한을 다시 확인합니다.
        public void ProcessRequest(HttpContext context)
        {
            context.Response.ContentType = "application/json; charset=utf-8";
            var repository = new AdminRepository();
            try
            {
                var operatorKey = repository.RequireOperator(context);
                var action = (context.Request.QueryString["action"] ?? "state").ToLowerInvariant();
                object result;
                switch (action)
                {
                    case "state":
                        result = repository.GetState();
                        break;
                    case "search-players":
                        result = repository.SearchPlayers(context.Request.QueryString["q"] ?? "");
                        break;
                    case "save-hottime":
                        result = SaveHotTime(repository, operatorKey, Body(context));
                        break;
                    case "set-operator":
                        result = SetOperator(repository, operatorKey, Body(context));
                        break;
                    case "set-ban":
                        result = SetBan(repository, operatorKey, Body(context));
                        break;
                    case "save-monster":
                        repository.UpsertMonster(operatorKey, Body(context));
                        result = new { ok = true, message = "도감 데이터를 저장했습니다." };
                        break;
                    case "save-weapon":
                        repository.UpsertWeapon(operatorKey, Body(context));
                        result = new { ok = true, message = "무기 데이터를 저장했습니다." };
                        break;
                    case "save-enhancement":
                        repository.UpsertEnhancementRule(operatorKey, Body(context));
                        result = new { ok = true, message = "강화 확률을 저장했습니다." };
                        break;
                    default:
                        context.Response.StatusCode = 404;
                        result = new { message = "없는 관리자 요청입니다." };
                        break;
                }
                context.Response.Write(Json.Serialize(result));
            }
            catch (UnauthorizedAccessException exception)
            {
                context.Response.StatusCode = 403;
                context.Response.Write(Json.Serialize(new { message = exception.Message }));
            }
            catch (InvalidOperationException exception)
            {
                context.Response.StatusCode = 400;
                context.Response.Write(Json.Serialize(new { message = exception.Message }));
            }
            catch (ArgumentException)
            {
                context.Response.StatusCode = 400;
                context.Response.Write(Json.Serialize(new { message = "요청 데이터 형식이 올바르지 않습니다." }));
            }
        }

        private static object SaveHotTime(AdminRepository repository, string operatorKey, Dictionary<string, object> body)
        {
            repository.SaveHotTime(
                operatorKey,
                BoolBody(body, "enabled"),
                DoubleBody(body, "goldMultiplier", 1),
                DoubleBody(body, "experienceMultiplier", 1),
                StringBody(body, "startsAtKst", "startsAtUtc"),
                StringBody(body, "endsAtKst", "endsAtUtc"));
            return new { ok = true, message = "핫타임 배율을 저장했습니다." };
        }

        private static object SetOperator(AdminRepository repository, string operatorKey, Dictionary<string, object> body)
        {
            var targetPlayerKey = StringBody(body, "targetPlayerKey");
            repository.SetOperator(operatorKey, targetPlayerKey, BoolBody(body, "isOperator"));
            return new { ok = true, message = "운영자 권한을 변경했습니다." };
        }

        private static object SetBan(AdminRepository repository, string operatorKey, Dictionary<string, object> body)
        {
            var targetPlayerKey = StringBody(body, "targetPlayerKey");
            repository.SetBan(operatorKey, targetPlayerKey, BoolBody(body, "isBanned"), StringBody(body, "reason"));
            return new { ok = true, message = "유저 접속 제한 상태를 변경했습니다." };
        }

        private static Dictionary<string, object> Body(HttpContext context)
        {
            context.Request.InputStream.Position = 0;
            using (var reader = new StreamReader(context.Request.InputStream))
            {
                var json = reader.ReadToEnd();
                return string.IsNullOrWhiteSpace(json)
                    ? new Dictionary<string, object>()
                    : Json.Deserialize<Dictionary<string, object>>(json);
            }
        }

        private static string StringBody(Dictionary<string, object> body, string key)
        {
            return body.ContainsKey(key) && body[key] != null ? body[key].ToString().Trim() : "";
        }

        private static string StringBody(Dictionary<string, object> body, string key, string fallbackKey)
        {
            var value = StringBody(body, key);
            return string.IsNullOrWhiteSpace(value) ? StringBody(body, fallbackKey) : value;
        }

        private static bool BoolBody(Dictionary<string, object> body, string key)
        {
            bool value;
            return body.ContainsKey(key) && body[key] != null && bool.TryParse(body[key].ToString(), out value) && value;
        }

        private static double DoubleBody(Dictionary<string, object> body, string key, double fallback)
        {
            double value;
            return body.ContainsKey(key) && body[key] != null && double.TryParse(body[key].ToString(), out value) ? value : fallback;
        }
    }
}
