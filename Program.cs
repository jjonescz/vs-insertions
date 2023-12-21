using System.Text.Json.Nodes;
using System.Web;
using VsInsertions.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

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

app.MapGet("/oauth/callback", async (HttpContext context, string code, IConfiguration configuration) =>
{
    // Authorize app.
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

    return Results.LocalRedirect("/?token=" + HttpUtility.UrlEncode(json["access_token"]!.ToString()));
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
