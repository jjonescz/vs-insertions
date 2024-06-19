using Microsoft.AspNetCore.DataProtection;
using System.Text.Json.Nodes;
using VsInsertions;
using VsInsertions.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddHttpContextAccessor();

builder.Services.AddSingleton<TitleParser>();
builder.Services.AddSingleton<RpsParser>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapGet("/oauth/callback", async (HttpContext context, string code, IConfiguration configuration, IDataProtectionProvider dataProtectionProvider) =>
{
    // Authorize app.
    // https://learn.microsoft.com/en-us/azure/devops/integrate/get-started/authentication/azure-devops-oauth?view=azure-devops#3-get-an-access-and-refresh-token-for-the-user
    var config = configuration.GetSection("AzureDevOpsOAuth");
    var client = new HttpClient();
    var response = await client.PostAsync("https://app.vssps.visualstudio.com/oauth2/token", new FormUrlEncodedContent([
        new("client_assertion_type", "urn:ietf:params:oauth:client-assertion-type:jwt-bearer"),
        new("client_assertion", config.GetValue<string>("ClientSecret")),
        new("grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer"), 
        new("assertion", code),
        new("redirect_uri", $"{context.Request.Scheme}://{context.Request.Host}/oauth/callback")]));
    var str = await response.Content.ReadAsStringAsync();
    var json = JsonNode.Parse(str)!;
    var accessToken = json["access_token"]!.ToString();

    // Encrypt access token.
    var protector = dataProtectionProvider.CreateProtector("access_token");
    var encryptedAccessToken = protector.Protect(accessToken);

    // Store access token in cookie.
    context.Response.Cookies.Append("access_token", encryptedAccessToken, new CookieOptions
    {
        HttpOnly = false,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        // CONSIDER: Store also the refresh token and use it instead of this.
        Expires = DateTimeOffset.UtcNow.AddSeconds(int.Parse(json["expires_in"]!.ToString()))
    });

    return Results.LocalRedirect("/");
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
