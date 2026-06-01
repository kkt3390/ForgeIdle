using System;
using System.Configuration;

namespace EnhanceAddiction.WebForms.Data
{
    public static class ConnectionSettings
    {
        // MonsterASP.NET에 이미 등록된 이전 환경 변수도 읽어 무중단 교체를 돕습니다.
        public static string Value
        {
            get
            {
                return Environment.GetEnvironmentVariable("ENHANCE_ADDICTION_DB_CONNECTION")
                    ?? Environment.GetEnvironmentVariable("FORGEIDLE_DB_CONNECTION")
                    ?? ConfigurationManager.ConnectionStrings["EnhanceAddiction"].ConnectionString;
            }
        }
    }
}
