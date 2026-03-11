using System.Text.Json.Nodes;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace VsInsertions;

public sealed class MaestroConfigService(ILogger<MaestroConfigService> logger)
{
    private static readonly string BaseUrl =
        "https://dev.azure.com/dnceng/internal/_apis/git/repositories/maestro-configuration";

    private readonly IDeserializer _yamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>
    /// Reads subscriptions and default channels from the maestro-configuration repo.
    /// </summary>
    public async Task<MaestroConfig> GetConfigAsync(HttpClient client)
    {
        // Get the repo tree to find YAML files.
        var treeUrl = $"{BaseUrl}/items?recursionLevel=Full&api-version=6.0&versionDescriptor.version=main";
        var treeJson = await client.GetStringAsync(treeUrl);
        var tree = JsonNode.Parse(treeJson);

        var subscriptions = new List<ArcadeSubscription>();
        var defaultChannels = new List<DefaultChannel>();

        foreach (var item in tree!["value"]!.AsArray())
        {
            var path = item!["path"]!.ToString();
            var isFolder = (bool)item["isFolder"]!;
            if (isFolder || !path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                var fileUrl = $"{BaseUrl}/items?path={Uri.EscapeDataString(path)}&api-version=6.0&versionDescriptor.version=main";
                var yamlContent = await client.GetStringAsync(fileUrl);
                ParseYamlFile(path, yamlContent, subscriptions, defaultChannels);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to parse YAML file {Path}", path);
            }
        }

        return new MaestroConfig(subscriptions, defaultChannels);
    }

    internal void ParseYamlFile(
        string path,
        string yamlContent,
        List<ArcadeSubscription> subscriptions,
        List<DefaultChannel> defaultChannels)
    {
        // maestro-configuration uses YAML files that can contain either a single subscription
        // or a list of subscriptions, or default channel mappings.
        // We try to detect the format by looking at the content.
        var yaml = _yamlDeserializer.Deserialize<Dictionary<string, object>>(yamlContent);
        if (yaml is null)
            return;

        if (yaml.ContainsKey("channel") && yaml.ContainsKey("sourceRepository"))
        {
            // Single subscription.
            var sub = DeserializeSubscription(yamlContent, path);
            if (sub != null)
                subscriptions.Add(sub);
        }
        else if (yaml.ContainsKey("subscriptions"))
        {
            // List of subscriptions.
            var doc = _yamlDeserializer.Deserialize<SubscriptionsDocument>(yamlContent);
            if (doc?.Subscriptions != null)
            {
                foreach (var sub in doc.Subscriptions)
                {
                    sub.SourceFile = path;
                    subscriptions.Add(sub);
                }
            }
        }
        else if (yaml.ContainsKey("defaultChannels"))
        {
            var doc = _yamlDeserializer.Deserialize<DefaultChannelsDocument>(yamlContent);
            if (doc?.DefaultChannels != null)
                defaultChannels.AddRange(doc.DefaultChannels);
        }

        // Also try to parse as a list at top level (some files might be plain YAML lists).
        if (!yaml.ContainsKey("channel") && !yaml.ContainsKey("subscriptions") && !yaml.ContainsKey("defaultChannels"))
        {
            try
            {
                var list = _yamlDeserializer.Deserialize<List<ArcadeSubscription>>(yamlContent);
                if (list != null)
                {
                    foreach (var sub in list)
                    {
                        sub.SourceFile = path;
                        subscriptions.Add(sub);
                    }
                }
            }
            catch
            {
                // Not a list format, that's fine.
                logger.LogDebug("File {Path} is not in a recognized subscription format", path);
            }
        }
    }

    private ArcadeSubscription? DeserializeSubscription(string yamlContent, string path)
    {
        try
        {
            var sub = _yamlDeserializer.Deserialize<ArcadeSubscription>(yamlContent);
            if (sub != null)
                sub.SourceFile = path;
            return sub;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to deserialize subscription from {Path}", path);
            return null;
        }
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
    public string? Channel { get; set; }
    public string? SourceRepository { get; set; }
    public string? TargetRepository { get; set; }
    public string? TargetBranch { get; set; }
    public string? SourceDirectory { get; set; }
    public string? TargetDirectory { get; set; }
    public string? UpdateFrequency { get; set; }
    public bool Enabled { get; set; } = true;
    public bool Batchable { get; set; }
    public List<string>? MergePolicies { get; set; }
    public List<string>? ExcludedAssets { get; set; }
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
}

public sealed class DefaultChannel
{
    public string? Repository { get; set; }
    public string? Branch { get; set; }
    public string? Channel { get; set; }
}

internal sealed class SubscriptionsDocument
{
    public List<ArcadeSubscription>? Subscriptions { get; set; }
}

internal sealed class DefaultChannelsDocument
{
    public List<DefaultChannel>? DefaultChannels { get; set; }
}
