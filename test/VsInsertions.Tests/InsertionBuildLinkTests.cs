using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using VsInsertions.Components.Pages;

namespace VsInsertions.Tests;

public class InsertionBuildLinkTests
{
    [Fact]
    public async Task LeavingCancelsWithoutShowingAnErrorAndReentryCanLoad()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        CancellationToken firstToken = default;
        await using var harness = new Harness();
        var root = await harness.RenderAsync(async token =>
        {
            calls++;
            if (calls == 1)
            {
                firstToken = token;
                entered.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            return Result("Loaded on reentry");
        });

        var pending = harness.SetActiveAsync(true);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await harness.SetActiveAsync(false);
        await pending;

        Assert.True(firstToken.IsCancellationRequested);
        Assert.DoesNotContain("Could not load", await harness.HtmlAsync(root));
        await harness.SetActiveAsync(true);
        Assert.Contains("Loaded on reentry", await harness.HtmlAsync(root));
        await harness.SetActiveAsync(false);
        await harness.SetActiveAsync(true);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task CanceledLateResultCannotOverwriteReenteredPreview()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stale = new TaskCompletionSource<InsertionChanges>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        await using var harness = new Harness();
        var root = await harness.RenderAsync(_ =>
        {
            if (++calls == 1)
            {
                entered.SetResult();
                return stale.Task;
            }
            return Task.FromResult(Result("Current result"));
        });

        var pending = harness.SetActiveAsync(true);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await harness.SetActiveAsync(false);
        await harness.SetActiveAsync(true);
        stale.SetResult(Result("Stale result"));
        await pending;

        var html = await harness.HtmlAsync(root);
        Assert.Contains("Current result", html);
        Assert.DoesNotContain("Stale result", html);
        Assert.DoesNotContain("Could not load", html);
    }

    [Fact]
    public async Task HttpTimeoutIsStillReportedAsAnError()
    {
        await using var harness = new Harness();
        var root = await harness.RenderAsync(_ => Task.FromException<InsertionChanges>(
            new TaskCanceledException("HTTP request timed out.")));

        await harness.SetActiveAsync(true);

        var html = await harness.HtmlAsync(root);
        Assert.Contains("Could not load or compare merged PRs: HTTP request timed out.", html);
        Assert.Contains("Retry", html);
    }

    [Fact]
    public async Task DisposingCancelsPendingWork()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken requestToken = default;
        var harness = new Harness();
        await harness.RenderAsync(async token =>
        {
            requestToken = token;
            entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return Result("Should not be displayed");
        });

        var pending = harness.SetActiveAsync(true);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await harness.DisposeAsync();
        await pending;

        Assert.True(requestToken.IsCancellationRequested);
    }

    private static InsertionChanges Result(string title) => new(
        [new InsertionChange(new MergedPullRequest(title, "https://github.com/dotnet/roslyn/pull/1"), true)],
        40, "20260909.40", null, null, null);

    private sealed class Harness : IAsyncDisposable, IComponentActivator
    {
        private readonly ServiceProvider services;
        private readonly HtmlRenderer renderer;
        private InsertionBuildLink component = null!;

        public Harness()
        {
            services = new ServiceCollection()
                .AddLogging()
                .AddSingleton<IComponentActivator>(this)
                .AddSingleton<IJSRuntime, UnusedJsRuntime>()
                .BuildServiceProvider();
            renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        }

        public IComponent CreateInstance(Type componentType)
        {
            Assert.Equal(typeof(InsertionBuildLink), componentType);
            return component = Assert.IsType<InsertionBuildLink>(Activator.CreateInstance(componentType));
        }

        public Task<HtmlRootComponent> RenderAsync(
            Func<CancellationToken, Task<InsertionChanges>> load) =>
            renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<InsertionBuildLink>(
                ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(InsertionBuildLink.Url)] = "https://example.com/insertion",
                    [nameof(InsertionBuildLink.BuildNumber)] = "20260909.50",
                    [nameof(InsertionBuildLink.Tooltip)] = "Insertion",
                    [nameof(InsertionBuildLink.LoadChanges)] = load,
                })));

        public Task SetActiveAsync(bool active) =>
            renderer.Dispatcher.InvokeAsync(() => component.SetPreviewActive(active));

        public Task<string> HtmlAsync(HtmlRootComponent root) =>
            renderer.Dispatcher.InvokeAsync(root.ToHtmlString);

        public async ValueTask DisposeAsync()
        {
            await renderer.DisposeAsync();
            await services.DisposeAsync();
        }
    }

    private sealed class UnusedJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            throw new InvalidOperationException("Static rendering must not invoke JavaScript.");

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            throw new InvalidOperationException("Static rendering must not invoke JavaScript.");
    }
}
