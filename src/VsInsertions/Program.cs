using Microsoft.AspNetCore.Authentication;
using VsInsertions;
using VsInsertions.Components;
using VsInsertions.Controllers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddHttpContextAccessor();

builder.Services.AddControllers();
CacheController.RegisterServices(builder.Services);

builder.Services.AddSingleton<TitleParser>();
builder.Services.AddSingleton<RpsParser>();
builder.Services.AddSingleton<MaestroConfigService>();
builder.Services.AddSingleton<GitHubFlowService>();
builder.Services.AddScoped<FlowsState>();

// GitHub OAuth (only when credentials are configured).
var gitHubClientId = builder.Configuration["GitHub:ClientId"];
var gitHubClientSecret = builder.Configuration["GitHub:ClientSecret"];
var authBuilder = builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = "Cookies";
        options.DefaultChallengeScheme = "GitHub";
    })
    .AddCookie("Cookies");

if (!string.IsNullOrEmpty(gitHubClientId) && !string.IsNullOrEmpty(gitHubClientSecret))
{
    authBuilder.AddGitHub("GitHub", options =>
    {
        options.ClientId = gitHubClientId;
        options.ClientSecret = gitHubClientSecret;
        options.Scope.Add("public_repo");
        options.SaveTokens = true;
    });
}

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

app.UseAuthentication();
app.UseAuthorization();

// GitHub OAuth sign-in/sign-out endpoints.
app.MapGet("/signin-github-start", (string? returnUrl) =>
    Results.Challenge(
        new AuthenticationProperties
        {
            RedirectUri = returnUrl ?? "/flows",
        },
        authenticationSchemes: ["GitHub"]));
app.MapGet("/signout-github", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync("Cookies");
    ctx.Response.Redirect("/flows");
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.UseCors();

app.MapControllers();

app.Run();
