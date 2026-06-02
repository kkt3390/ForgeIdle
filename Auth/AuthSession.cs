using System;
using System.Web;

namespace EnhanceAddiction.WebForms.Auth
{
    public static class AuthSession
    {
        // 현재 세션에 로그인된 플레이어 키가 있는지 확인합니다.
        public static bool IsAuthenticated(HttpContext context)
        {
            return !string.IsNullOrWhiteSpace(context.Session["PlayerKey"] as string);
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
