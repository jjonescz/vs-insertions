using System.Diagnostics;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using VsInsertions;
using VsInsertions.Components;
using VsInsertions.Controllers;

// Default local HTTP endpoint. HTTP only (no HTTPS) to avoid certificate setup;
// uses a distinctive port to avoid clashing with other local dev servers.
const string defaultUrl = "http://localhost:47213";

// Auto-launch the browser by default; allow opting out via flag or env var.
var noBrowser = args.Contains("--no-browser", StringComparer.OrdinalIgnoreCase)
    || string.Equals(Environment.GetEnvironmentVariable("VSINSERTIONS_NO_BROWSER"), "1", StringComparison.Ordinal);
var appArgs = args.Where(a => !string.Equals(a, "--no-browser", StringComparison.OrdinalIgnoreCase)).ToArray();

// When run as an installed .NET tool the working directory is the user's shell location,
// so anchor the content root (appsettings.json, static web assets) to the install directory.
// When run from the project directory (dotnet run / watch) keep the default content root.
var runningAsTool = !File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"));

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = appArgs,
    ContentRootPath = runningAsTool ? AppContext.BaseDirectory : null,
});

builder.Logging.AddSimpleConsole(options =>
{
    options.TimestampFormat = "HH:mm:ss.fff ";
});

// Bind to the fixed HTTP endpoint unless the user overrides it (--urls / ASPNETCORE_URLS).
if (string.IsNullOrEmpty(builder.Configuration["urls"])
    && string.IsNullOrEmpty(builder.Configuration["ASPNETCORE_URLS"]))
{
    builder.WebHost.UseUrls(defaultUrl);
}

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
builder.Services.AddSingleton<AdoTokenProvider>();
builder.Services.AddSingleton<GitHubTokenProvider>();
builder.Services.AddScoped<FlowsState>();

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.UseCors();

app.MapControllers();

// Open the dashboard in the browser once the server is listening.
// Skipped in Development where the launch profile already opens the browser.
if (!noBrowser && !app.Environment.IsDevelopment())
{
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        var url = app.Services.GetRequiredService<IServer>().Features
            .Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault() ?? defaultUrl;
        OpenBrowser(url);
    });
}

app.Run();

static void OpenBrowser(string url)
{
    try
    {
        if (OperatingSystem.IsWindows())
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        else if (OperatingSystem.IsMacOS())
        {
            Process.Start("open", url);
        }
        else
        {
            Process.Start("xdg-open", url);
        }
    }
    catch
    {
        // Best effort — ignore if no browser is available.
    }
}
