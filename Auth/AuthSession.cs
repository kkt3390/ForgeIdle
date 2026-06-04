using System;
using System.Web;
using EnhanceAddiction.WebForms.Data;

namespace EnhanceAddiction.WebForms.Auth
{
    public static class AuthSession
    {
        // 현재 세션에 로그인한 플레이어 키가 있는지 확인합니다.
        public static bool IsAuthenticated(HttpContext context)
        {
            return !string.IsNullOrWhiteSpace(context.Session["PlayerKey"] as string);
        }

        // 로그인 성공 시 새 토큰을 발급하고 DB에 저장해 이전 기기 세션을 무효화합니다.
        public static void SignIn(HttpContext context, string playerKey, string provider)
        {
            var loginToken = Guid.NewGuid().ToString("N");
            new PlayerRepository().SetActiveLoginToken(playerKey, loginToken);
            context.Session["PlayerKey"] = playerKey;
            context.Session["LoginProvider"] = provider;
            context.Session["LoginToken"] = loginToken;
        }

        // 현재 세션이 DB에 기록된 최신 로그인인지 확인합니다.
        public static bool IsCurrentLogin(HttpContext context)
        {
            var playerKey = context.Session["PlayerKey"] as string;
            var loginToken = context.Session["LoginToken"] as string;
            return new PlayerRepository().IsActiveLoginToken(playerKey, loginToken);
        }

        // 오래된 기기에서 요청하면 세션을 비우고 다시 로그인하도록 막습니다.
        public static void EnsureCurrentLogin(HttpContext context)
        {
            if (!IsAuthenticated(context))
                throw new UnauthorizedAccessException("로그인이 필요합니다.");
            if (IsCurrentLogin(context)) return;

            context.Session.Clear();
            throw new UnauthorizedAccessException("다른 기기에서 로그인되어 현재 접속이 종료되었습니다.");
        }

        // 최신 세션에서 로그아웃할 때만 DB의 로그인 토큰을 비웁니다.
        public static void SignOut(HttpContext context)
        {
            var playerKey = context.Session["PlayerKey"] as string;
            var loginToken = context.Session["LoginToken"] as string;
            new PlayerRepository().ClearActiveLoginToken(playerKey, loginToken);
            context.Session.Clear();
            context.Session.Abandon();
        }

        // 로컬 개발 환경에서 들어온 요청인지 확인합니다.
        public static bool IsLocalRequest(HttpContext context)
        {
            return context.Request.IsLocal
                || string.Equals(context.Request.Url.Host, "localhost", StringComparison.OrdinalIgnoreCase)
                || string.Equals(context.Request.Url.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase);
        }
    }
}
