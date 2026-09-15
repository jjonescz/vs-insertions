using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using VsInsertions.Components.Pages;

namespace VsInsertions.Tests;

public class FlowsTests
{
    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, false, false)]
    [InlineData(true, true, true)]
    [InlineData(true, true, false)]
    public async Task DisabledDefaultChannelDoesNotReuseCachedManifestStatus(
        bool incoming, bool circular, bool sourceUpToDate)
    {
        var sub = new ArcadeSubscription
        {
            Id = "test-subscription",
            SourceRepository = "https://github.com/dotnet/roslyn",
            TargetRepository = circular ? "https://github.com/dotnet/roslyn" : "https://github.com/dotnet/dotnet",
            Channel = ".NET 11 Dev",
            TargetBranch = "target-branch",
            UpdateFrequency = "EveryBuild",
        };
        var repo = incoming ? sub.TargetRepoShort : sub.SourceRepoShort;
        var state = new FlowsState { CurrentRepo = repo, Repos = [repo] };
        var cachedResult = new SourceManifestCheckResult
        {
            ManifestCommitSha = "abcdef1",
            LatestSourceCommitSha = sourceUpToDate ? "abcdef1" : "1234567",
            SourceUpToDate = sourceUpToDate,
        };
        state.ManifestCheckResults["dotnet/roslyn|source-branch"] = cachedResult;

        await using var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(state)
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().Build())
            .AddSingleton<NavigationManager, TestNavigationManager>()
            .AddSingleton<IJSRuntime, UnusedJsRuntime>()
            .AddSingleton<MaestroConfigService>()
            .AddSingleton<GitHubFlowService>()
            .AddSingleton<AdoTokenProvider>()
            .AddSingleton<GitHubTokenProvider>()
            .BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());

        SetConfig(enabled: true);
        var enabledHtml = await RenderAsync();
        Assert.Contains("Source manifest:", enabledHtml);
        Assert.Contains("abcdef1", enabledHtml);
        Assert.Contains(sourceUpToDate ? "Up-to-date" : "Source has new changes", enabledHtml);

        SetConfig(enabled: false);
        var disabledHtml = await RenderAsync();
        Assert.Same(cachedResult, Assert.Single(state.ManifestCheckResults).Value);
        Assert.Contains("1 disabled", disabledHtml);
        Assert.Contains("<code>source-branch</code>", disabledHtml);
        Assert.DoesNotContain("Source manifest:", disabledHtml);
        Assert.DoesNotContain("abcdef1", disabledHtml);
        Assert.DoesNotContain("1234567", disabledHtml);
        Assert.DoesNotContain("Source is up-to-date", disabledHtml);
        Assert.DoesNotContain("Source has new changes", disabledHtml);

        void SetConfig(bool enabled)
        {
            state.Config = new MaestroConfig([sub],
                [new DefaultChannel
                {
                    Repository = sub.SourceRepository,
                    Channel = sub.Channel,
                    Branch = "source-branch",
                    Enabled = enabled,
                }]);
            state.RepoPrCaches.Clear();
            state.GetOrCreatePrCache(repo).ExpandedRows.Add(sub.Id);
        }

        Task<string> RenderAsync() => renderer.Dispatcher.InvokeAsync(async () =>
        {
            var result = await renderer.RenderComponentAsync<Flows>();
            return result.ToHtmlString();
        });
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager() => Initialize("http://localhost/", "http://localhost/flows");

        protected override void NavigateToCore(string uri, bool forceLoad) =>
            throw new InvalidOperationException("Static rendering must not navigate.");
    }

    private sealed class UnusedJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            throw new InvalidOperationException("Static rendering must not invoke JavaScript.");

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            throw new InvalidOperationException("Static rendering must not invoke JavaScript.");
    }
}
