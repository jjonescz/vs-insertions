using Microsoft.Extensions.Logging.Abstractions;

namespace VsInsertions.Tests;

public class MaestroConfigServiceTests
{
    private readonly MaestroConfigService _service = new(NullLogger<MaestroConfigService>.Instance);

    [Fact]
    public void ParseSingleSubscription()
    {
        var yaml = """
            channel: .NET 11 Dev
            sourceRepository: https://github.com/dotnet/roslyn
            targetRepository: https://github.com/dotnet/dotnet
            targetBranch: main
            updateFrequency: EveryBuild
            enabled: true
            batchable: false
            mergePolicies:
              - Standard
            """;

        var subscriptions = new List<ArcadeSubscription>();
        var defaultChannels = new List<DefaultChannel>();
        _service.ParseYamlFile("/subscriptions/roslyn.yaml", yaml, subscriptions, defaultChannels);

        Assert.Single(subscriptions);
        var sub = subscriptions[0];
        Assert.Equal(".NET 11 Dev", sub.Channel);
        Assert.Equal("https://github.com/dotnet/roslyn", sub.SourceRepository);
        Assert.Equal("https://github.com/dotnet/dotnet", sub.TargetRepository);
        Assert.Equal("main", sub.TargetBranch);
        Assert.Equal("EveryBuild", sub.UpdateFrequency);
        Assert.True(sub.Enabled);
        Assert.False(sub.Batchable);
        Assert.Equal(["Standard"], sub.MergePolicies);
        Assert.Equal("/subscriptions/roslyn.yaml", sub.SourceFile);
    }

    [Fact]
    public void ParseSubscriptionsDocument()
    {
        var yaml = """
            subscriptions:
              - channel: .NET 11 Dev
                sourceRepository: https://github.com/dotnet/roslyn
                targetRepository: https://github.com/dotnet/dotnet
                targetBranch: main
                updateFrequency: EveryBuild
                enabled: true
              - channel: .NET 11 Dev
                sourceRepository: https://github.com/dotnet/razor
                targetRepository: https://github.com/dotnet/dotnet
                targetBranch: main
                updateFrequency: EveryBuild
                enabled: true
            """;

        var subscriptions = new List<ArcadeSubscription>();
        var defaultChannels = new List<DefaultChannel>();
        _service.ParseYamlFile("/subs.yaml", yaml, subscriptions, defaultChannels);

        Assert.Equal(2, subscriptions.Count);
        Assert.Equal("dotnet/roslyn", subscriptions[0].SourceRepoShort);
        Assert.Equal("dotnet/razor", subscriptions[1].SourceRepoShort);
        Assert.All(subscriptions, s => Assert.Equal("/subs.yaml", s.SourceFile));
    }

    [Fact]
    public void ParseDefaultChannelsDocument()
    {
        var yaml = """
            defaultChannels:
              - repository: https://github.com/dotnet/roslyn
                branch: main
                channel: .NET 11 Dev
              - repository: https://github.com/dotnet/razor
                branch: main
                channel: .NET 11 Dev
            """;

        var subscriptions = new List<ArcadeSubscription>();
        var defaultChannels = new List<DefaultChannel>();
        _service.ParseYamlFile("/defaults.yaml", yaml, subscriptions, defaultChannels);

        Assert.Empty(subscriptions);
        Assert.Equal(2, defaultChannels.Count);
        Assert.Equal("https://github.com/dotnet/roslyn", defaultChannels[0].Repository);
        Assert.Equal("main", defaultChannels[0].Branch);
        Assert.Equal(".NET 11 Dev", defaultChannels[0].Channel);
    }

    [Fact]
    public void ParseDisabledSubscription()
    {
        var yaml = """
            channel: VS 17.14
            sourceRepository: https://github.com/dotnet/roslyn
            targetRepository: https://github.com/dotnet/dotnet
            targetBranch: release/17.14
            updateFrequency: EveryDay
            enabled: false
            batchable: true
            """;

        var subscriptions = new List<ArcadeSubscription>();
        var defaultChannels = new List<DefaultChannel>();
        _service.ParseYamlFile("/disabled.yaml", yaml, subscriptions, defaultChannels);

        Assert.Single(subscriptions);
        Assert.False(subscriptions[0].Enabled);
        Assert.True(subscriptions[0].Batchable);
    }

    [Fact]
    public void NormalizeRepoName_GitHub()
    {
        Assert.Equal("dotnet/roslyn", MaestroConfigService.NormalizeRepoName("https://github.com/dotnet/roslyn"));
        Assert.Equal("dotnet/razor", MaestroConfigService.NormalizeRepoName("https://github.com/dotnet/razor/"));
    }

    [Fact]
    public void NormalizeRepoName_AzureDevOps()
    {
        Assert.Equal("dnceng/dotnet-wpf", MaestroConfigService.NormalizeRepoName("https://dev.azure.com/dnceng/internal/_git/dotnet-wpf"));
    }

    [Fact]
    public void NormalizeRepoName_PlainString()
    {
        Assert.Equal("some-repo", MaestroConfigService.NormalizeRepoName("some-repo"));
    }

    [Fact]
    public void GetUniqueRepos_DeduplicatesAndSorts()
    {
        var subscriptions = new List<ArcadeSubscription>
        {
            new() { SourceRepository = "https://github.com/dotnet/roslyn", TargetRepository = "https://github.com/dotnet/dotnet" },
            new() { SourceRepository = "https://github.com/dotnet/razor", TargetRepository = "https://github.com/dotnet/dotnet" },
            new() { SourceRepository = "https://github.com/dotnet/roslyn", TargetRepository = "https://github.com/dotnet/aspnetcore" },
        };

        var repos = MaestroConfigService.GetUniqueRepos(subscriptions);

        Assert.Equal(["dotnet/aspnetcore", "dotnet/dotnet", "dotnet/razor", "dotnet/roslyn"], repos);
    }

    [Fact]
    public void ParseWithExcludedAssets()
    {
        var yaml = """
            channel: .NET 11 Dev
            sourceRepository: https://github.com/dotnet/roslyn
            targetRepository: https://github.com/dotnet/dotnet
            targetBranch: main
            updateFrequency: EveryBuild
            enabled: true
            excludedAssets:
              - Microsoft.CodeAnalysis.Test.Utilities
              - Microsoft.CodeAnalysis.CSharp.Test.Utilities
            """;

        var subscriptions = new List<ArcadeSubscription>();
        var defaultChannels = new List<DefaultChannel>();
        _service.ParseYamlFile("/excluded.yaml", yaml, subscriptions, defaultChannels);

        Assert.Single(subscriptions);
        Assert.Equal(2, subscriptions[0].ExcludedAssets!.Count);
    }
}
