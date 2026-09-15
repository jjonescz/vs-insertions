using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VsInsertions.Components.Pages;

namespace VsInsertions.Tests;

public class UnifiedFlowTableTests
{
    [Theory]
    [InlineData(true, "EveryBuild", true, true)]
    [InlineData(true, "EveryBuild", false, false)]
    [InlineData(true, "EveryBuild", null, true)]
    [InlineData(false, "EveryBuild", true, false)]
    [InlineData(false, "EveryBuild", null, false)]
    [InlineData(true, "None", true, false)]
    [InlineData(true, " none ", true, false)]
    [InlineData(true, null, true, false)]
    [InlineData(true, "", true, false)]
    [InlineData(true, " ", true, false)]
    public async Task DisabledStateCombinesSubscriptionAndDefaultChannel(
        bool enabled, string? frequency, bool? channelEnabled, bool expectedActive)
    {
        var row = new UnifiedFlowRow
        {
            OtherRepo = "dotnet/roslyn",
            IsIncoming = true,
            Subscription = new ArcadeSubscription
            {
                SourceRepository = "https://github.com/dotnet/roslyn",
                TargetRepository = "https://github.com/dotnet/dotnet",
                Enabled = enabled,
                UpdateFrequency = frequency,
            },
            DefaultChannel = channelEnabled is { } value ? new DefaultChannel { Enabled = value } : null,
        };

        var html = await RenderAsync(row);

        Assert.Equal(expectedActive, row.IsActive);
        Assert.Contains(expectedActive ? "badge bg-primary" : "badge border border-primary text-primary", html);
        if (expectedActive)
            Assert.DoesNotContain("1 disabled", html);
        else
            Assert.Contains("1 disabled", html);
    }

    [Theory]
    [InlineData(true, false, "primary")]
    [InlineData(false, false, "secondary")]
    [InlineData(true, true, "info")]
    public async Task DisabledDefaultChannelIsShownForEveryDirection(bool incoming, bool circular, string color)
    {
        var sub = new ArcadeSubscription
        {
            Id = "test-subscription",
            SourceRepository = "https://github.com/dotnet/roslyn",
            TargetRepository = circular ? "https://github.com/dotnet/roslyn" : "https://github.com/dotnet/dotnet",
            Channel = ".NET Core Tooling Dev",
            TargetBranch = "main",
            UpdateFrequency = "EveryBuild",
        };
        var channel = MaestroConfigService.ResolveDefaultChannel(
            [new() { Repository = sub.SourceRepository, Channel = sub.Channel, Branch = "main", Enabled = false }], sub);
        var row = new UnifiedFlowRow
        {
            OtherRepo = incoming ? sub.SourceRepoShort : sub.TargetRepoShort,
            IsIncoming = incoming,
            Subscription = sub,
            DefaultChannel = channel,
            SourceBranch = channel?.Branch,
            Prs = [OldPr("closed")],
        };

        var html = await RenderAsync(row, expanded: true);

        Assert.Contains($"badge border border-{color} text-{color}", html);
        Assert.Contains("default channel disabled", html);
        Assert.Contains("Default channel disabled", html);
        Assert.Contains(".NET Core Tooling Dev (disabled)", html);
        Assert.Contains("<code>main</code>", html);
        Assert.Contains("1 disabled", html);
        Assert.DoesNotContain("Possibly stuck", html);
    }

    [Theory]
    [InlineData(true, "closed", "Possibly stuck")]
    [InlineData(false, "closed", null)]
    [InlineData(true, "open", "PR open for a long time")]
    [InlineData(false, "open", "PR open for a long time")]
    public async Task StuckWarningsRequireActiveFlowButOldOpenPrsStillWarn(
        bool channelEnabled, string prState, string? expectedWarning)
    {
        var row = new UnifiedFlowRow
        {
            OtherRepo = "dotnet/roslyn",
            IsIncoming = true,
            Subscription = new ArcadeSubscription { UpdateFrequency = "EveryBuild" },
            DefaultChannel = new DefaultChannel { Enabled = channelEnabled },
            Prs = [OldPr(prState)],
        };

        var html = await RenderAsync(row);

        if (expectedWarning != null)
            Assert.Contains(expectedWarning, html);
        else
        {
            Assert.DoesNotContain("Possibly stuck", html);
            Assert.DoesNotContain("PR open for a long time", html);
        }
    }

    [Fact]
    public async Task SameRepoRowWithoutSubscriptionRemainsActive()
    {
        var row = new UnifiedFlowRow
        {
            OtherRepo = "dotnet/roslyn",
            IsIncoming = true,
            IsSameRepo = true,
            SourceBranch = "main",
        };

        var html = await RenderAsync(row);

        Assert.True(row.IsActive);
        Assert.Contains("badge bg-info text-dark", html);
        Assert.DoesNotContain("disabled", html);
    }

    private static FlowPr OldPr(string state) => new()
    {
        Number = 1,
        Repo = "dotnet/roslyn",
        Title = "Update dependencies",
        Url = "https://github.com/dotnet/roslyn/pull/1",
        State = state,
        CreatedAt = DateTimeOffset.UtcNow.AddDays(-60),
        UpdatedAt = DateTimeOffset.UtcNow.AddDays(-30),
    };

    private static async Task<string> RenderAsync(UnifiedFlowRow row, bool expanded = false)
    {
        await using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var result = await renderer.RenderComponentAsync<UnifiedFlowTable>(
                ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(UnifiedFlowTable.Rows)] = new List<UnifiedFlowRow> { row },
                    [nameof(UnifiedFlowTable.ExpandedRows)] = expanded ? new HashSet<string> { row.RowKey } : [],
                }));
            return result.ToHtmlString();
        });
    }
}
