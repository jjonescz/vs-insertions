namespace VsInsertions;

/// <summary>
/// Scoped (per-circuit) state for the Flows page, preserved across navigations.
/// </summary>
public sealed class FlowsState
{
    public string? AdoAccessToken { get; set; }
    public MaestroConfig? Config { get; set; }
    public List<string> Repos { get; set; } = [];
    public string? CurrentRepo { get; set; }
    public List<FlowPr>? FlowPrs { get; set; }
    public List<FlowPr>? OutgoingFlowPrs { get; set; }
    public HashSet<int> ExpandedPrs { get; set; } = [];
}
