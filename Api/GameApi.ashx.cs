using System;
using System.Collections.Generic;
using System.IO;
using System.Web;
using System.Web.Script.Serialization;
using System.Web.SessionState;
using EnhanceAddiction.WebForms.Data;
using EnhanceAddiction.WebForms.Game;
using EnhanceAddiction.WebForms.Auth;

namespace EnhanceAddiction.WebForms.Api
{
    public sealed class GameApi : IHttpHandler, IRequiresSessionState
    {
        // .ashx 하나가 SPA 화면의 API 역할을 합니다.
        // 게임 데이터를 바꿀 수 있는 요청은 반드시 Execute 메서드를 거쳐 감사 로그를 남깁니다.
        private static readonly GameCatalog Catalog = new GameCatalog();
        private static readonly GameService Game = new GameService(Catalog);
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

        public bool IsReusable { get { return false; } }

        // 요청 action을 해석해 대응하는 게임 기능을 실행하고 JSON으로 응답합니다.
        public void ProcessRequest(HttpContext context)
        {
            context.Response.ContentType = "application/json; charset=utf-8";
            var action = (context.Request.QueryString["action"] ?? "state").ToLowerInvariant();
            try
            {
                object result;
                switch (action)
                {
                    case "auth": result = Auth(context); break;
                    case "catalog": result = Game.CatalogSnapshot(); break;
                    case "state": result = State(context); break;
                    case "rankings": result = new PlayerRepository().GetRankings(context.Request.QueryString["category"]); break;
                    case "nickname": result = Nickname(context); break;
                    case "hunt-start":
                        result = Execute(context, "AutomaticHuntStart",
                            player => Game.StartAutomaticHunt(player, IntBody(context, "areaId")));
                        break;
                    case "hunt-claim":
                        result = Execute(context, "AutomaticHuntClaim", player => Game.ClaimAutomaticHunt(player));
                        break;
                    case "hunt-manual":
                        result = Execute(context, "ManualHunt",
                            player => Game.ManualHunt(player, IntBody(context, "areaId")));
                        break;
                    case "enhance":
                        result = Execute(context, "Enhance",
                            player => Game.Enhance(player, BoolBody(context, "useProtection")));
                        break;
                    case "boss":
                        result = Execute(context, "BossChallenge", player => Game.ChallengeBoss(player));
                        break;
                    case "stats-invest":
                        result = Execute(context, "StatInvest",
                            player => Game.InvestStat(player, StringBody(context, "stat")));
                        break;
                    case "stats-reset":
                        result = Execute(context, "StatReset", player => Game.ResetStats(player));
                        break;
                    default:
                        context.Response.StatusCode = 404;
                        result = new { message = "알 수 없는 요청입니다." };
                        break;
                }
                context.Response.Write(Json.Serialize(result));
            }
            catch (UnauthorizedAccessException exception)
            {
                context.Response.StatusCode = 401;
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

        // 현재 세션의 로그인 여부와 로컬 테스트 로그인 허용 여부를 반환합니다.
        private static object Auth(HttpContext context)
        {
            var playerKey = context.Session["PlayerKey"] as string;
            var admin = new AdminRepository();
            var authenticated = AuthSession.IsAuthenticated(context) && AuthSession.IsCurrentLogin(context);
            if (AuthSession.IsAuthenticated(context) && !authenticated)
            {
                context.Session.Clear();
            }
            var banned = false;
            var banMessage = "";
            if (authenticated)
            {
                try
                {
                    admin.EnsureNotBanned(playerKey);
                }
                catch (UnauthorizedAccessException exception)
                {
                    banned = true;
                    banMessage = exception.Message;
                }
            }
            return new
            {
                authenticated = authenticated,
                provider = context.Session["LoginProvider"] as string,
                allowDevelopmentLogin = AuthSession.IsLocalRequest(context),
                kakaoConfigured = KakaoSettings.IsConfigured,
                isOperator = authenticated && !banned && admin.IsOperator(playerKey),
                isBanned = banned,
                banMessage = banMessage
            };
        }

        // 로그인한 사용자의 최신 게임 상태를 읽고 정규화된 값을 저장합니다.
        private static object State(HttpContext context)
        {
            string playerKey;
            var repository = new PlayerRepository();
            var player = GetPlayer(context, repository, out playerKey);
            var snapshot = Game.Snapshot(player);
            repository.Save(playerKey, player);
            return snapshot;
        }

        // 닉네임 형식을 검사한 뒤 사용자 상태와 감사 로그에 저장합니다.
        private static object Nickname(HttpContext context)
        {
            var nickname = StringBody(context, "nickname").Trim();
            string playerKey;
            var repository = new PlayerRepository();
            var player = GetPlayer(context, repository, out playerKey);
            repository.ValidateNickname(playerKey, nickname);
            return Execute(context, "NicknameChange", current =>
            {
                current.Nickname = nickname;
                return new GameResult
                {
                    Ok = true,
                    Message = "닉네임을 저장했습니다.",
                    State = Game.Snapshot(current),
                    Details = new { nickname = nickname }
                };
            });
        }

        // 게임 상태를 바꾸는 행동을 실행하고 전후 상태와 결과를 감사 로그에 기록합니다.
        private static GameResult Execute(HttpContext context, string actionType, Func<PlayerState, GameResult> execute)
        {
            string playerKey;
            var repository = new PlayerRepository();
            var player = GetPlayer(context, repository, out playerKey);
            var beforeStateJson = Json.Serialize(player);
            var result = execute(player);
            var afterStateJson = Json.Serialize(player);

            // 성공과 실패를 모두 남겨야 밸런스 검증과 문제 역추적이 가능합니다.
            repository.AddGameActionLog(
                playerKey, actionType, result.Ok, result.Message,
                beforeStateJson, afterStateJson,
                result.EnhancementAttempt != null ? Json.Serialize(result.EnhancementAttempt) :
                result.Details != null ? Json.Serialize(result.Details) : null);

            if (result.Ok) repository.Save(playerKey, player);
            if (result.EnhancementAttempt != null) repository.AddEnhancementAttempt(playerKey, result.EnhancementAttempt);
            return result;
        }

        // 로그인 세션의 플레이어 키로 DB 상태를 조회하거나 새 상태를 만듭니다.
        private static PlayerState GetPlayer(HttpContext context, PlayerRepository repository, out string playerKey)
        {
            AuthSession.EnsureCurrentLogin(context);
            playerKey = context.Session["PlayerKey"] as string;
            if (string.IsNullOrWhiteSpace(playerKey))
                throw new UnauthorizedAccessException("로그인이 필요합니다.");
            new AdminRepository().EnsureNotBanned(playerKey);
            return repository.GetOrCreate(playerKey);
        }

        // JSON 요청 본문을 키와 값 형태로 읽습니다.
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

        // JSON 요청 본문에서 문자열 값을 읽습니다.
        private static string StringBody(HttpContext context, string key)
        {
            var body = Body(context);
            return body.ContainsKey(key) && body[key] != null ? body[key].ToString() : "";
        }

        // JSON 요청 본문에서 정수 값을 읽고 잘못된 값은 거부합니다.
        private static int IntBody(HttpContext context, string key)
        {
            int value;
            if (!int.TryParse(StringBody(context, key), out value)) throw new InvalidOperationException("숫자 요청값이 올바르지 않습니다.");
            return value;
        }

        // JSON 요청 본문에서 참·거짓 값을 읽습니다.
        private static bool BoolBody(HttpContext context, string key)
        {
            bool value;
            return bool.TryParse(StringBody(context, key), out value) && value;
        }
    }
}
