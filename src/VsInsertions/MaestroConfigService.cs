using System.Text.Json.Nodes;
using YamlDotNet.Serialization;

namespace VsInsertions;

public sealed class MaestroConfigService(ILogger<MaestroConfigService> logger)
{
    private static readonly string BaseUrl =
        "https://dev.azure.com/dnceng/internal/_apis/git/repositories/maestro-configuration";
    private const string Branch = "production";

    private readonly IDeserializer _yamlDeserializer = new DeserializerBuilder()
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>
    /// Reads subscriptions and default channels from the maestro-configuration repo (production branch).
    /// </summary>
    public async Task<MaestroConfig> GetConfigAsync(HttpClient client)
    {
        var treeUrl = $"{BaseUrl}/items?recursionLevel=Full&api-version=6.0&versionDescriptor.version={Branch}";
        logger.LogInformation("Fetching maestro-configuration tree (branch: {Branch})...", Branch);
        var treeJson = await client.GetStringAsync(treeUrl);
        var tree = JsonNode.Parse(treeJson);

        var items = tree!["value"]!.AsArray();
        var allFiles = items
            .Where(item => !(item!["isFolder"]?.GetValue<bool>() ?? false))
            .Select(item => item!["path"]!.ToString())
            .ToList();

        var subscriptionFiles = allFiles
            .Where(p => p.StartsWith("/configuration/subscriptions/", StringComparison.OrdinalIgnoreCase)
                     && (p.EndsWith(".yml", StringComparison.OrdinalIgnoreCase)
                      || p.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        var defaultChannelFiles = allFiles
            .Where(p => p.StartsWith("/configuration/default-channels/", StringComparison.OrdinalIgnoreCase)
                     && (p.EndsWith(".yml", StringComparison.OrdinalIgnoreCase)
                      || p.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        logger.LogInformation("Found {SubFiles} subscription files and {DcFiles} default-channel files",
            subscriptionFiles.Count, defaultChannelFiles.Count);

        var subscriptions = new List<ArcadeSubscription>();
        var defaultChannels = new List<DefaultChannel>();

        foreach (var path in subscriptionFiles)
        {
            try
            {
                var fileUrl = $"{BaseUrl}/items?path={Uri.EscapeDataString(path)}&api-version=6.0&versionDescriptor.version={Branch}";
                var yamlContent = await client.GetStringAsync(fileUrl);
                var list = _yamlDeserializer.Deserialize<List<ArcadeSubscription>>(yamlContent);
                if (list != null)
                {
                    foreach (var sub in list)
                        sub.SourceFile = path;
                    subscriptions.AddRange(list);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to parse subscription file {Path}", path);
            }
        }

        foreach (var path in defaultChannelFiles)
        {
            try
            {
                var fileUrl = $"{BaseUrl}/items?path={Uri.EscapeDataString(path)}&api-version=6.0&versionDescriptor.version={Branch}";
                var yamlContent = await client.GetStringAsync(fileUrl);
                var list = _yamlDeserializer.Deserialize<List<DefaultChannel>>(yamlContent);
                if (list != null)
                    defaultChannels.AddRange(list);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to parse default-channel file {Path}", path);
            }
        }

        logger.LogInformation("Loaded {SubCount} subscriptions and {ChannelCount} default channels",
            subscriptions.Count, defaultChannels.Count);
        return new MaestroConfig(subscriptions, defaultChannels);
    }

    /// <summary>
    /// Extracts unique repository names (GitHub short forms like "dotnet/roslyn") from subscriptions.
    /// </summary>
    public static List<string> GetUniqueRepos(IReadOnlyList<ArcadeSubscription> subscriptions)
    {
        var repos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sub in subscriptions)
        {
            if (!string.IsNullOrEmpty(sub.SourceRepository))
                repos.Add(NormalizeRepoName(sub.SourceRepository));
            if (!string.IsNullOrEmpty(sub.TargetRepository))
                repos.Add(NormalizeRepoName(sub.TargetRepository));
        }
        return repos.OrderBy(r => r, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Converts full GitHub URLs to short form (e.g., "https://github.com/dotnet/roslyn" → "dotnet/roslyn").
    /// </summary>
    public static string NormalizeRepoName(string repo)
    {
        if (repo.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase))
            return repo["https://github.com/".Length..].TrimEnd('/');
        if (repo.StartsWith("https://dev.azure.com/", StringComparison.OrdinalIgnoreCase))
        {
            // e.g., "https://dev.azure.com/dnceng/internal/_git/dotnet-wpf" → "dnceng/dotnet-wpf"
            var parts = repo["https://dev.azure.com/".Length..].Split('/');
            if (parts.Length >= 4 && parts[2] == "_git")
                return $"{parts[0]}/{parts[3]}";
        }
        return repo;
    }
}

public sealed record MaestroConfig(
    IReadOnlyList<ArcadeSubscription> Subscriptions,
    IReadOnlyList<DefaultChannel> DefaultChannels);

public sealed class ArcadeSubscription
{
    [YamlMember(Alias = "Channel")]
    public string? Channel { get; set; }

    [YamlMember(Alias = "Source Repository URL")]
    public string? SourceRepository { get; set; }

    [YamlMember(Alias = "Target Repository URL")]
    public string? TargetRepository { get; set; }

    [YamlMember(Alias = "Target Branch")]
    public string? TargetBranch { get; set; }

    [YamlMember(Alias = "Source Directory")]
    public string? SourceDirectory { get; set; }

    [YamlMember(Alias = "Target Directory")]
    public string? TargetDirectory { get; set; }

    [YamlMember(Alias = "Update Frequency")]
    public string? UpdateFrequency { get; set; }

    [YamlMember(Alias = "Source Enabled")]
    public bool Enabled { get; set; } = true;

    [YamlMember(Alias = "Batchable")]
    public bool Batchable { get; set; }

    [YamlMember(Alias = "Merge Policies")]
    public List<MergePolicy>? MergePolicies { get; set; }

    [YamlMember(Alias = "Excluded Assets")]
    public List<string>? ExcludedAssets { get; set; }

    [YamlMember(Alias = "Failure Notification Tags")]
    public string? FailureNotificationTags { get; set; }

    /// <summary>Path of the YAML file this subscription was parsed from.</summary>
    [YamlIgnore]
    public string? SourceFile { get; set; }

    /// <summary>Short display name for the source repo.</summary>
    [YamlIgnore]
    public string SourceRepoShort => string.IsNullOrEmpty(SourceRepository) ? "<unknown>" : MaestroConfigService.NormalizeRepoName(SourceRepository);

    /// <summary>Short display name for the target repo.</summary>
    [YamlIgnore]
    public string TargetRepoShort => string.IsNullOrEmpty(TargetRepository) ? "<unknown>" : MaestroConfigService.NormalizeRepoName(TargetRepository);

    [YamlIgnore]
    public string MergePolicySummary => MergePolicies is { Count: > 0 }
        ? string.Join(", ", MergePolicies.Select(mp => mp.Name))
        : "—";
}

public sealed class MergePolicy
{
    [YamlMember(Alias = "Name")]
    public string? Name { get; set; }

    [YamlMember(Alias = "Properties")]
    public Dictionary<string, object>? Properties { get; set; }
}

public sealed class DefaultChannel
{
    [YamlMember(Alias = "Repository")]
    public string? Repository { get; set; }

    [YamlMember(Alias = "Branch")]
    public string? Branch { get; set; }

    [YamlMember(Alias = "Channel")]
    public string? Channel { get; set; }

    [YamlMember(Alias = "Enabled")]
    public bool Enabled { get; set; } = true;
}
