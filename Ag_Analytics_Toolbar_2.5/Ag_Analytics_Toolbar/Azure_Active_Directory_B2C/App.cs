using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Identity.Client;


namespace Ag_Analytics_Toolbar.Azure_Active_Directory_B2C
{
    class App
    {
   
        private static readonly string Tenant = "aganalyticsdev.onmicrosoft.com";
        private static readonly string AzureAdB2CHostname = "aganalyticsdev.b2clogin.com";
        private static readonly string ClientId = "4943d6c5-cbae-46f9-b136-76e2d58df443";
        private static readonly string RedirectUri = "https://aganalyticsdev.b2clogin.com/oauth2/nativeclient";
        public static string PolicySignUpSignIn = "b2c_1_siupin";
        public static string PolicyEditProfile = "b2c_1_sipe";
        public static string PolicyResetPassword = "b2c_1_SSPR";

        public static string[] ApiScopes = { "https://aganalyticsdev.onmicrosoft.com/api/user.read", "https://aganalyticsdev.onmicrosoft.com/api/user.write" };
        public static string ApiEndpoint = "https://aganalyticsdev.eastus2.cloudapp.azure.com/api/ProductPrices/GetUserOrganizations";
        private static string AuthorityBase = $"https://{AzureAdB2CHostname}/tfp/{Tenant}/";
        public static string AuthoritySignUpSignIn = $"{AuthorityBase}{PolicySignUpSignIn}";
        public static string AuthorityEditProfile = $"{AuthorityBase}{PolicyEditProfile}";
        public static string AuthorityResetPassword = $"{AuthorityBase}{PolicyResetPassword}";

        public static IPublicClientApplication PublicClientApp { get; private set; }

        static App()
        {
            PublicClientApp = PublicClientApplicationBuilder.Create(ClientId)
                .WithB2CAuthority(AuthoritySignUpSignIn)
                .WithRedirectUri(RedirectUri)
                .WithLogging(Log, LogLevel.Info, false) // don't log PII details on a regular basis
                .Build();

            TokenCacheHelper.Bind(PublicClientApp.UserTokenCache);
        }
        private static void Log(LogLevel level, string message, bool containsPii)
        {
            string logs = ($"{level} {message}");
            StringBuilder sb = new StringBuilder();
            sb.Append(logs);
            File.AppendAllText(System.Reflection.Assembly.GetExecutingAssembly().Location + ".msalLogs.txt", sb.ToString());
            sb.Clear();
        }
    }
}
