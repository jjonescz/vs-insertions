namespace VsInsertions;

/// <summary>
/// Per-repo cache for flow PRs and UI state.
/// </summary>
public sealed class RepoPrCache
{
    public List<FlowPr>? FlowPrs { get; set; }
    public List<FlowPr>? OutgoingFlowPrs { get; set; }
    public List<FlowPr>? ClosedFlowPrs { get; set; }
    public List<FlowPr>? ClosedOutgoingFlowPrs { get; set; }
    public List<FlowPr>? LocPrs { get; set; }
    public List<FlowPr>? ClosedLocPrs { get; set; }
    public List<FlowPr>? MergePrs { get; set; }
    public List<FlowPr>? ClosedMergePrs { get; set; }
    public HashSet<string> ExpandedRows { get; set; } = [];
}

/// <summary>
/// Scoped (per-circuit) state for the Flows page, preserved across navigations.
/// </summary>
public sealed class FlowsState
{
    public string? AdoAccessToken { get; set; }
    public string? GitHubPatToken { get; set; }
    public MaestroConfig? Config { get; set; }
    public List<string> Repos { get; set; } = [];
    public string? CurrentRepo { get; set; }
    public Dictionary<string, RepoPrCache> RepoPrCaches { get; } = new(StringComparer.OrdinalIgnoreCase);

    public RepoPrCache GetOrCreatePrCache(string repo)
    {
        if (!RepoPrCaches.TryGetValue(repo, out var cache))
        {
            cache = new RepoPrCache();
            RepoPrCaches[repo] = cache;
        }
        return cache;
    }
}
