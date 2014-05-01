using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.WindowsAzure;
using System;
using System.Threading;

namespace ScheduledWebJobCreator
{
    public class TokenCredentialHelper<T>
        where T : ITokenCredentialConfiguration
    {
        private static string GetAuthorizationHeader(T configuration)
        {
            AuthenticationResult result = null;

            var context = new AuthenticationContext(
                string.Format("https://login.windows.net/{0}",
                    configuration.GetTenantId()));

            var thread = new Thread(() =>
            {
                result = context.AcquireToken(
                    clientId: configuration.GetClientId(),
                    redirectUri: new Uri(configuration.GetRedirectUrl()),
                    resource: "https://management.core.windows.net/",
                    promptBehavior: PromptBehavior.Auto);
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Name = "AquireTokenThread";
            thread.Start();
            thread.Join();
            return result.CreateAuthorizationHeader().Substring("Bearer ".Length);
        }

        public static TokenCloudCredentials GetCredentials(T configuration, string subscriptionId = null)
        {
            var token = GetAuthorizationHeader(configuration);

            if (subscriptionId == null)
                return new TokenCloudCredentials(token);
            else
                return new TokenCloudCredentials(subscriptionId, token);
        }
    }
}
