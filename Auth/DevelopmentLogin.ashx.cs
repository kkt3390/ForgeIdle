using System;
using System.Web;
using System.Web.SessionState;

namespace EnhanceAddiction.WebForms.Auth
{
    public sealed class DevelopmentLogin : IHttpHandler, IRequiresSessionState
    {
        public bool IsReusable { get { return false; } }

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
