using System;
using System.Web;

namespace EnhanceAddiction.WebForms.Auth
{
    public static class AuthSession
    {
        public static bool IsAuthenticated(HttpContext context)
        {
            return !string.IsNullOrWhiteSpace(context.Session["PlayerKey"] as string);
        }

        public static bool IsLocalRequest(HttpContext context)
        {
            return context.Request.IsLocal
                || string.Equals(context.Request.Url.Host, "localhost", StringComparison.OrdinalIgnoreCase)
                || string.Equals(context.Request.Url.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase);
        }
    }
}
