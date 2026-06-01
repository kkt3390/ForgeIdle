using System.Web;
using System.Web.SessionState;

namespace EnhanceAddiction.WebForms.Auth
{
    public sealed class Logout : IHttpHandler, IRequiresSessionState
    {
        public bool IsReusable { get { return false; } }

        public void ProcessRequest(HttpContext context)
        {
            context.Session.Clear();
            context.Session.Abandon();
            context.Response.Redirect(VirtualPathUtility.ToAbsolute("~/"));
        }
    }
}
