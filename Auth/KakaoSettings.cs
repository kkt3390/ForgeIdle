using System;
using System.Configuration;

namespace EnhanceAddiction.WebForms.Auth
{
    public static class KakaoSettings
    {
        // 카카오 로그인 요청에 사용하는 REST API 키를 반환합니다.
        public static string ClientId
        {
            get { return Read("Authentication__Kakao__ClientId", "KakaoClientId"); }
        }

        // 카카오 토큰 발급 요청에 사용하는 클라이언트 시크릿을 반환합니다.
        public static string ClientSecret
        {
            get { return Read("Authentication__Kakao__ClientSecret", "KakaoClientSecret"); }
        }

        // 카카오 로그인을 화면에 노출할 수 있는 설정 상태인지 확인합니다.
        public static bool IsConfigured
        {
            get { return !string.IsNullOrWhiteSpace(ClientId); }
        }

        // 운영 환경 변수 값을 우선하고 없으면 Web.config 값을 읽습니다.
        private static string Read(string environmentVariable, string appSetting)
        {
            return Environment.GetEnvironmentVariable(environmentVariable)
                ?? ConfigurationManager.AppSettings[appSetting];
        }
    }
}
