using System;
using System.Web;
using EnhanceAddiction.WebForms.Data;

namespace EnhanceAddiction.WebForms
{
    public partial class Operator : System.Web.UI.Page
    {
        // 운영자 화면은 렌더링 전에도 DB 권한을 확인합니다.
        protected void Page_Load(object sender, EventArgs e)
        {
            try
            {
                new AdminRepository().RequireOperator(HttpContext.Current);
            }
            catch (UnauthorizedAccessException exception)
            {
                Response.StatusCode = 403;
                Response.ContentType = "text/plain; charset=utf-8";
                Response.Write(exception.Message);
                Response.End();
            }
        }
    }
}
