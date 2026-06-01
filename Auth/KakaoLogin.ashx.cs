using System;
using System.Web;
using System.Web.SessionState;

namespace EnhanceAddiction.WebForms.Auth
{
    public sealed class KakaoLogin : IHttpHandler, IRequiresSessionState
    {
        public bool IsReusable { get { return false; } }

        public void ProcessRequest(HttpContext context)
        {
            if (!KakaoSettings.IsConfigured)
                throw new InvalidOperationException("카카오 REST API 키가 설정되지 않았습니다.");

            var state = Guid.NewGuid().ToString("N");
            context.Session["KakaoOAuthState"] = state;
            var authorizeUrl = "https://kauth.kakao.com/oauth/authorize"
                + "?response_type=code"
                + "&client_id=" + HttpUtility.UrlEncode(KakaoSettings.ClientId)
                + "&redirect_uri=" + HttpUtility.UrlEncode(CallbackUrl(context))
                + "&state=" + HttpUtility.UrlEncode(state);
            context.Response.Redirect(authorizeUrl);
        }

        internal static string CallbackUrl(HttpContext context)
        {
            return context.Request.Url.GetLeftPart(UriPartial.Authority)
                + VirtualPathUtility.ToAbsolute("~/Auth/KakaoCallback.ashx");
        }
    }
}
