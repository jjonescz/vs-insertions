using Microsoft.Extensions.Logging.Abstractions;
using YamlDotNet.Serialization;

namespace VsInsertions.Tests;

public class MaestroConfigServiceTests
{
    private readonly IDeserializer _deserializer = new DeserializerBuilder()
        .IgnoreUnmatchedProperties()
        .Build();

    [Fact]
    public void ParseSingleSubscription()
    {
        var yaml = """
            - Channel: .NET 11 Dev
              Source Repository URL: https://github.com/dotnet/roslyn
              Target Repository URL: https://github.com/dotnet/dotnet
              Target Branch: main
              Update Frequency: EveryBuild
              Source Enabled: true
              Batchable: false
              Merge Policies:
              - Name: Standard
            """;

        var list = _deserializer.Deserialize<List<ArcadeSubscription>>(yaml);

        Assert.Single(list);
        var sub = list[0];
        Assert.Equal(".NET 11 Dev", sub.Channel);
        Assert.Equal("https://github.com/dotnet/roslyn", sub.SourceRepository);
        Assert.Equal("https://github.com/dotnet/dotnet", sub.TargetRepository);
        Assert.Equal("main", sub.TargetBranch);
        Assert.Equal("EveryBuild", sub.UpdateFrequency);
        Assert.True(sub.SourceEnabled);
        Assert.False(sub.Batchable);
        Assert.NotNull(sub.MergePolicies);
        Assert.Single(sub.MergePolicies);
        Assert.Equal("Standard", sub.MergePolicies[0].Name);
    }

    [Fact]
    public void ParseMultipleSubscriptions()
    {
        var yaml = """
            - Channel: .NET 11 Dev
              Source Repository URL: https://github.com/dotnet/roslyn
              Target Repository URL: https://github.com/dotnet/dotnet
              Target Branch: main
              Update Frequency: EveryBuild
              Source Enabled: true
            - Channel: .NET 11 Dev
              Source Repository URL: https://github.com/dotnet/razor
              Target Repository URL: https://github.com/dotnet/dotnet
              Target Branch: main
              Update Frequency: EveryBuild
              Source Enabled: true
            """;

        var list = _deserializer.Deserialize<List<ArcadeSubscription>>(yaml);

        Assert.Equal(2, list.Count);
        Assert.Equal("dotnet/roslyn", list[0].SourceRepoShort);
        Assert.Equal("dotnet/razor", list[1].SourceRepoShort);
    }

    [Fact]
    public void ParseDefaultChannels()
    {
        var yaml = """
            - Repository: https://github.com/dotnet/roslyn
              Branch: main
              Channel: .NET 11 Dev
              Enabled: true
            - Repository: https://github.com/dotnet/razor
              Branch: main
              Channel: .NET 11 Dev
              Enabled: true
            """;

        var list = _deserializer.Deserialize<List<DefaultChannel>>(yaml);

        Assert.Equal(2, list.Count);
        Assert.Equal("https://github.com/dotnet/roslyn", list[0].Repository);
        Assert.Equal("main", list[0].Branch);
        Assert.Equal(".NET 11 Dev", list[0].Channel);
        Assert.True(list[0].Enabled);
    }

    [Fact]
    public void ParseDisabledDefaultChannel()
    {
        var yaml = """
            - Repository: https://github.com/dotnet/roslyn
              Branch: main
              Channel: .NET Core Tooling Dev
              Enabled: false
            """;

        var channel = Assert.Single(_deserializer.Deserialize<List<DefaultChannel>>(yaml));

        Assert.False(channel.Enabled);
        Assert.Equal("main", channel.Branch);
    }

    [Theory]
    [InlineData(true, true, "main", true)]
    [InlineData(true, false, "main", true)]
    [InlineData(false, true, "validation", true)]
    [InlineData(false, false, "main", false)]
    [InlineData(true, null, "main", true)]
    [InlineData(false, null, "main", false)]
    [InlineData(null, true, "validation", true)]
    [InlineData(null, false, "validation", false)]
    [InlineData(null, null, null, null)]
    public void ResolveDefaultChannel_PrefersEnabledMappingsThenExactChannel(
        bool? enabled, bool? validationEnabled, string? expectedBranch, bool? expectedEnabled)
    {
        var sub = new ArcadeSubscription
        {
            SourceRepository = "https://github.com/dotnet/roslyn",
            Channel = ".NET 11 Dev",
        };
        var channels = new List<DefaultChannel>
        {
            new() { Repository = sub.SourceRepository, Channel = "Unrelated", Branch = "unrelated" },
            new() { Repository = "https://github.com/dotnet/razor", Channel = sub.Channel, Branch = "other-repo" },
        };
        if (validationEnabled is { } validation)
        {
            channels.Add(new DefaultChannel
            {
                Repository = "DOTNET/ROSLYN",
                Channel = ".net 11 dev - validation",
                Branch = "validation",
                Enabled = validation,
            });
        }
        if (enabled is { } exact)
        {
            channels.Add(new DefaultChannel
            {
                Repository = "https://github.com/DOTNET/ROSLYN/",
                Channel = ".net 11 dev",
                Branch = "main",
                Enabled = exact,
            });
        }

        var channel = MaestroConfigService.ResolveDefaultChannel(channels, sub);

        Assert.Equal(expectedBranch, channel?.Branch);
        Assert.Equal(expectedEnabled, channel?.Enabled);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ResolveDefaultChannel_PrefersEnabledBranchRegardlessOfOrder(bool enabledFirst)
    {
        var sub = new ArcadeSubscription
        {
            SourceRepository = "https://github.com/dotnet/roslyn",
            Channel = ".NET 11 Dev",
        };
        var enabled = new DefaultChannel
        {
            Repository = sub.SourceRepository, Channel = sub.Channel, Branch = "main", Enabled = true,
        };
        var disabled = new DefaultChannel
        {
            Repository = sub.SourceRepository, Channel = sub.Channel, Branch = "old", Enabled = false,
        };

        var channel = MaestroConfigService.ResolveDefaultChannel(
            enabledFirst ? [enabled, disabled] : [disabled, enabled], sub);

        Assert.Same(enabled, channel);
    }

    [Theory]
    [InlineData(null, ".NET 11 Dev")]
    [InlineData("", ".NET 11 Dev")]
    [InlineData("https://github.com/dotnet/roslyn", null)]
    [InlineData("https://github.com/dotnet/roslyn", "")]
    public void ResolveDefaultChannel_MissingSourceOrChannel(string? sourceRepository, string? channel)
    {
        var sub = new ArcadeSubscription { SourceRepository = sourceRepository, Channel = channel };
        DefaultChannel[] channels = [new() { Repository = sourceRepository, Channel = channel }];

        Assert.Null(MaestroConfigService.ResolveDefaultChannel(channels, sub));
    }

    [Fact]
    public void ParseDisabledSourceSubscription()
    {
        var yaml = """
            - Channel: VS 17.14
              Source Repository URL: https://github.com/dotnet/roslyn
              Target Repository URL: https://github.com/dotnet/dotnet
              Target Branch: release/17.14
              Update Frequency: EveryDay
              Source Enabled: false
              Batchable: true
            """;

        var list = _deserializer.Deserialize<List<ArcadeSubscription>>(yaml);

        Assert.Single(list);
        Assert.True(list[0].Enabled);
        Assert.False(list[0].SourceEnabled);
        Assert.True(list[0].Batchable);
    }

    [Fact]
    public void ParseDisabledSubscription()
    {
        var yaml = """
            - Channel: VS 17.14
              Source Repository URL: https://github.com/dotnet/roslyn
              Target Repository URL: https://github.com/dotnet/dotnet
              Target Branch: release/17.14
              Update Frequency: EveryDay
              Enabled: false
              Source Enabled: true
            """;

        var list = _deserializer.Deserialize<List<ArcadeSubscription>>(yaml);

        Assert.Single(list);
        Assert.False(list[0].Enabled);
        Assert.True(list[0].SourceEnabled);
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
            - Channel: .NET 11 Dev
              Source Repository URL: https://github.com/dotnet/roslyn
              Target Repository URL: https://github.com/dotnet/dotnet
              Target Branch: main
              Update Frequency: EveryBuild
              Source Enabled: true
              Excluded Assets:
              - Microsoft.CodeAnalysis.Test.Utilities
              - Microsoft.CodeAnalysis.CSharp.Test.Utilities
            """;

        var list = _deserializer.Deserialize<List<ArcadeSubscription>>(yaml);

        Assert.Single(list);
        Assert.Equal(2, list[0].ExcludedAssets!.Count);
    }

    [Fact]
    public void ParseMergePolicySummary()
    {
        var yaml = """
            - Channel: .NET 11 Dev
              Source Repository URL: https://github.com/dotnet/roslyn
              Target Repository URL: https://github.com/dotnet/dotnet
              Target Branch: main
              Update Frequency: EveryBuild
              Merge Policies:
              - Name: AllChecksSuccessful
                Properties:
                  ignoreChecks:
                  - roslyn-integration-corehost
              - Name: Standard
            """;

        var list = _deserializer.Deserialize<List<ArcadeSubscription>>(yaml);

        Assert.Single(list);
        Assert.Equal("AllChecksSuccessful, Standard", list[0].MergePolicySummary);
    }
}
