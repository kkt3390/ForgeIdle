using System.Web;
using System.Web.SessionState;

namespace EnhanceAddiction.WebForms.Auth
{
    public sealed class Logout : IHttpHandler, IRequiresSessionState
    {
        public bool IsReusable { get { return false; } }

        // 현재 로그인 세션을 제거하고 첫 화면으로 이동합니다.
        public void ProcessRequest(HttpContext context)
        {
            context.Session.Clear();
            context.Session.Abandon();
            context.Response.Redirect(VirtualPathUtility.ToAbsolute("~/"));
        }
    }
}
