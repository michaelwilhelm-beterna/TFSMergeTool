using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;

namespace MergeHelper
{
    /// <summary>
    /// Anmeldung an Azure DevOps Services mit Microsoft Entra ID über den Windows-Kontomanager (WAM).
    /// Dadurch wird das am Windows angemeldete Konto per SSO genutzt, ggf. erscheint einmalig ein Anmeldedialog.
    /// </summary>
    public static class AuthService
    {
        // Öffentlicher Client "Azure CLI" (Microsoft First-Party, für WAM registriert, darf Azure-DevOps-Tokens anfordern).
        private const string ClientId = "04b07795-8ddb-461a-bbee-02f9e1bf7b46";

        // Ressource "Azure DevOps".
        private static readonly string[] Scopes = { "499b84ac-1321-427f-aa17-267ca6975798/.default" };

        private static readonly Dictionary<string, IPublicClientApplication> Apps =
            new Dictionary<string, IPublicClientApplication>(StringComparer.OrdinalIgnoreCase);

        public static async Task<string> GetTokenAsync(string tenant, IntPtr parentWindow)
        {
            tenant = string.IsNullOrWhiteSpace(tenant) ? "organizations" : tenant.Trim();

            if (!Apps.TryGetValue(tenant, out var app))
            {
                app = PublicClientApplicationBuilder.Create(ClientId)
                    .WithAuthority($"https://login.microsoftonline.com/{tenant}")
                    .WithDefaultRedirectUri()
                    .WithBroker(new BrokerOptions(BrokerOptions.OperatingSystems.Windows) { Title = "MergeHelper" })
                    .WithParentActivityOrWindow(() => parentWindow)
                    .Build();
                Apps[tenant] = app;
            }

            var accounts = await app.GetAccountsAsync();
            var account = accounts.FirstOrDefault() ?? PublicClientApplication.OperatingSystemAccount;

            AuthenticationResult result;
            try
            {
                result = await app.AcquireTokenSilent(Scopes, account).ExecuteAsync();
            }
            catch (MsalUiRequiredException)
            {
                result = await app.AcquireTokenInteractive(Scopes)
                    .WithAccount(account)
                    .WithParentActivityOrWindow(parentWindow)
                    .ExecuteAsync();
            }
            return result.AccessToken;
        }
    }
}
