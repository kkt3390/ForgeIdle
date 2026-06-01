using System;
using System.Configuration;

namespace EnhanceAddiction.WebForms.Auth
{
    public static class KakaoSettings
    {
        public static string ClientId
        {
            get { return Read("Authentication__Kakao__ClientId", "KakaoClientId"); }
        }

        public static string ClientSecret
        {
            get { return Read("Authentication__Kakao__ClientSecret", "KakaoClientSecret"); }
        }

        public static bool IsConfigured
        {
            get { return !string.IsNullOrWhiteSpace(ClientId); }
        }

        private static string Read(string environmentVariable, string appSetting)
        {
            return Environment.GetEnvironmentVariable(environmentVariable)
                ?? ConfigurationManager.AppSettings[appSetting];
        }
    }
}
