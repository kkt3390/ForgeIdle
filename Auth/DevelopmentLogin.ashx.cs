using System;
using System.Web;
using System.Web.SessionState;

namespace EnhanceAddiction.WebForms.Auth
{
    public sealed class DevelopmentLogin : IHttpHandler, IRequiresSessionState
    {
        public bool IsReusable { get { return false; } }

        // 로컬 개발 환경에서만 사용할 임시 테스트 계정을 만듭니다.
        public void ProcessRequest(HttpContext context)
        {
            if (!AuthSession.IsLocalRequest(context))
            {
                context.Response.StatusCode = 404;
                return;
            }
            context.Session["PlayerKey"] = "development-" + Guid.NewGuid().ToString("N");
            context.Session["LoginProvider"] = "development";
            context.Response.Redirect(VirtualPathUtility.ToAbsolute("~/"));
        }
    }
}
