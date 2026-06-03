using System;
using System.Web;
using EnhanceAddiction.WebForms.Data;

namespace EnhanceAddiction.WebForms
{
    public partial class Admin : System.Web.UI.Page
    {
        // 관리자 페이지는 화면 렌더링 전에도 운영자 권한을 확인합니다.
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
