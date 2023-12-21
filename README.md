# VSInsertions

VS insertions dashboard for use by Roslyn and Razor teams.

## Deployment

What you are going to need to deploy this app yourself.

### Azure DevOps OAuth app

1. Go to <https://app.vsaex.visualstudio.com/app/register>.
1. Select scopes `vso.code`, `vso.threads_full`.
1. Set callback URL to `https://<your-web-app-name>.azurewebsites.net/oauth/callback`.
   During development, set the callback URL to `https://localhost:7200/oauth/callback`
   (or create another OAuth app just for development).
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
   - use [Secret Manager](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets) during development,
   - configure environment variables on your server like:

     ```env
     AzureDevOpsOAuth__AppId=...
     AzureDevOpsOAuth__AppSecret=...
     AzureDevOpsOAuth__ClientSecret=...
     ```
