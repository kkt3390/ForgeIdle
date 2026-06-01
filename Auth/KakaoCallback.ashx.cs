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
            context.Session["PlayerKey"] = playerKey;
            context.Session["LoginProvider"] = "kakao";
            context.Response.Redirect(VirtualPathUtility.ToAbsolute("~/"));
        }

        private static string RequestAccessToken(HttpContext context, string code)
        {
            using (var client = new WebClient())
            {
                var values = new NameValueCollection
                {
                    ["grant_type"] = "authorization_code",
                    ["client_id"] = KakaoSettings.ClientId,
                    ["redirect_uri"] = KakaoLogin.CallbackUrl(context),
                    ["code"] = code
                };
                if (!string.IsNullOrWhiteSpace(KakaoSettings.ClientSecret))
                    values["client_secret"] = KakaoSettings.ClientSecret;
                var body = client.UploadValues("https://kauth.kakao.com/oauth/token", values);
                var payload = Json.Deserialize<Dictionary<string, object>>(client.Encoding.GetString(body));
                return payload["access_token"].ToString();
            }
        }

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
