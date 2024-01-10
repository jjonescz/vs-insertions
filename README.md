# VSInsertions

VS insertions dashboard for use by Roslyn and Razor teams.

## Deployment

What you are going to need to deploy this app yourself.

### Azure DevOps OAuth app

1. Go to <https://app.vsaex.visualstudio.com/app/register>.
1. Select scopes "Code (read)" (`vso.code`), "PR threads" (`vso.threads_full`).
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
   - do nothing during development, then secrets of a testing OAuth app will be used from `appsettings.Development.json`,
   - use [Secret Manager](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets) during development,
   - configure environment variables on your server like:

     ```env
     AzureDevOpsOAuth__AppId=...
     AzureDevOpsOAuth__AppSecret=...
     AzureDevOpsOAuth__ClientSecret=...
     ```
