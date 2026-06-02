using System;
using System.Web;
using EnhanceAddiction.WebForms.Data;

namespace EnhanceAddiction.WebForms
{
    public class Global : HttpApplication
    {
        // 애플리케이션 시작 시 운영 DB 스키마와 데이터 이전 상태를 준비합니다.
        protected void Application_Start(object sender, EventArgs e)
        {
            // 운영 서버 최초 실행에서도 필요한 전용 테이블을 자동으로 준비합니다.
            SchemaInitializer.EnsureCreated();
        }
    }
}
