using System;
using System.Web;
using System.Web.SessionState;
using EnhanceAddiction.WebForms.Data;

namespace EnhanceAddiction.WebForms.Api
{
    public sealed class OperatorPage : IHttpHandler, IRequiresSessionState
    {
        public bool IsReusable { get { return false; } }

        // Api 폴더의 .ashx 경로는 현재 호스팅에서 정상 반영되므로 운영자 화면도 이 경로로 제공합니다.
        public void ProcessRequest(HttpContext context)
        {
            try
            {
                new AdminRepository().RequireOperator(context);
            }
            catch (UnauthorizedAccessException exception)
            {
                context.Response.StatusCode = 403;
                context.Response.ContentType = "text/plain; charset=utf-8";
                context.Response.Write(exception.Message);
                return;
            }

            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.Write(OperatorPageTemplate.PageHtml);
        }
    }
}
