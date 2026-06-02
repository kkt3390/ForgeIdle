using System;
using System.Configuration;

namespace EnhanceAddiction.WebForms.Game
{
    public static class GameFeatureSettings
    {
        // 운영 환경 변수 또는 Web.config에서 도감 공개 여부를 읽습니다.
        public static bool CollectionEnabled
        {
            get
            {
                var value = Environment.GetEnvironmentVariable("ENHANCE_ADDICTION_COLLECTION_ENABLED")
                    ?? ConfigurationManager.AppSettings["CollectionEnabled"];
                bool enabled;
                return bool.TryParse(value, out enabled) && enabled;
            }
        }
    }
}
