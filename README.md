# VSInsertions

VS insertions dashboard for use by Roslyn and Razor teams.

## Development

```ps1
dotnet watch -lp https --project src/VsInsertions
```

## Deployment

What you are going to need to deploy this app yourself.

### Entra OAuth app

Follow [the official docs](https://learn.microsoft.com/en-us/azure/devops/integrate/get-started/authentication/oauth?view=azure-devops) or this overview:

1. Go to <https://entra.microsoft.com/>.
1. Register a new app.
1. In "API permissions", add Azure DevOps scopes "Code (read and write)" (`vso.code_write`).
1. In "Authentication":
   1. Add redirect URLs `https://<your-web-app-name>.azurewebsites.net/signin-oidc` and `https://localhost:7200/signin-oidc`.
   1. Set logout URL to `https://<your-web-app-name>.azurewebsites.net/signout-oidc`.
   1. Check ID tokens.
1. Create a certificate:

   ```ps1
   New-SelfSignedCertificate -Subject "CN=VsInsertions" -CertStoreLocation "Cert:\CurrentUser\My" -KeyExportPolicy Exportable -KeySpec Signature
   ```

   1. Export the `.cer` public key file using "Manage User Certificate" MMC snap-in accessible from the Windows Control Panel.
   1. Upload the `.cer` file to the Entra admin center for the app under "Certificates & secrets".
   1. Export the `.pfx` file similarly and make it available to the app (see the step below).

1. Make sure the following config is available to the app:
   - TenantId, ClientId can be found in Overview of the app in the Entra admin center.

   ```json
   {
     "AzureAd": {
       "Instance": "...",
       "TenantId": "...",
       "ClientId": "...",
     }
   }
   ```

   For example,
   - use [Secret Manager](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets) during development,
   - configure environment variables on your server like:

     ```env
     AzureAd__Instance=...
     AzureAd__TenantId=...
     AzureAd__ClientId=...
     ```

### Azure DevOps OAuth app (deprecated)

1. Go to <https://app.vsaex.visualstudio.com/app/register>.
1. Select scopes "Code (read and write)" (`vso.code_write`), "PR threads" (`vso.threads_full`).
1. Set callback URL to `https://<your-web-app-name>.azurewebsites.net/oauth/callback`.
   During development, set the callback URL to `https://localhost:7200/oauth/callback`.
1. Make sure the following config is available to the app:

   ```json
   {
     "AzureDevOpsOAuth": {
       "AppId": "...",
       "AppSecret": "...",
       "ClientSecret": "..."
     }
   }
   ```

   For example,
   - do nothing during development, then secrets of a testing OAuth app will be used from `appsettings.Development.json` (note that the OAuth app expects the port 7200 which is configured in the `https` launch profile),
   - use [Secret Manager](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets) during development,
   - configure environment variables on your server like:

     ```env
     AzureDevOpsOAuth__AppId=...
     AzureDevOpsOAuth__AppSecret=...
     AzureDevOpsOAuth__ClientSecret=...
     ```
