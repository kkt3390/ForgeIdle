using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Web;
using System.Web.Script.Serialization;
using System.Web.SessionState;
using EnhanceAddiction.WebForms.Data;

namespace EnhanceAddiction.WebForms.Auth
{
    public sealed class KakaoCallback : IHttpHandler, IRequiresSessionState
    {
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
        public bool IsReusable { get { return false; } }

        // 카카오 인증 콜백을 검증하고 소셜 계정을 게임 계정과 연결합니다.
        public void ProcessRequest(HttpContext context)
        {
            var expectedState = context.Session["KakaoOAuthState"] as string;
            var receivedState = context.Request.QueryString["state"];
            if (string.IsNullOrWhiteSpace(expectedState) || !string.Equals(expectedState, receivedState, StringComparison.Ordinal))
                throw new InvalidOperationException("카카오 로그인 검증값이 올바르지 않습니다.");

            var code = context.Request.QueryString["code"];
            if (string.IsNullOrWhiteSpace(code))
                throw new InvalidOperationException("카카오 로그인 승인 코드를 받지 못했습니다.");

            var accessToken = RequestAccessToken(context, code);
            var externalId = RequestKakaoUserId(accessToken);
            var playerKey = new PlayerRepository().GetOrCreateSocialPlayerKey("kakao", externalId);
            context.Session.Remove("KakaoOAuthState");
            AuthSession.SignIn(context, playerKey, "kakao");
            context.Response.Redirect(VirtualPathUtility.ToAbsolute("~/"));
        }

        // 카카오 승인 코드를 액세스 토큰으로 교환합니다.
        private static string RequestAccessToken(HttpContext context, string code)
        {
            using (var client = new WebClient())
            {
                var values = new NameValueCollection();
                values["grant_type"] = "authorization_code";
                values["client_id"] = KakaoSettings.ClientId;
                values["redirect_uri"] = KakaoLogin.CallbackUrl(context);
                values["code"] = code;
                if (!string.IsNullOrWhiteSpace(KakaoSettings.ClientSecret))
                    values["client_secret"] = KakaoSettings.ClientSecret;
                var body = client.UploadValues("https://kauth.kakao.com/oauth/token", values);
                var payload = Json.Deserialize<Dictionary<string, object>>(client.Encoding.GetString(body));
                return payload["access_token"].ToString();
            }
        }

        // 액세스 토큰으로 카카오 사용자 고유 ID를 조회합니다.
        private static string RequestKakaoUserId(string accessToken)
        {
            using (var client = new WebClient())
            {
                client.Headers[HttpRequestHeader.Authorization] = "Bearer " + accessToken;
                var payload = Json.Deserialize<Dictionary<string, object>>(
                    client.DownloadString("https://kapi.kakao.com/v2/user/me"));
                return payload["id"].ToString();
            }
        }
    }
}
