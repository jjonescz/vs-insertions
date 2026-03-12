namespace VsInsertions;

public sealed class UnifiedFlowRow
{
    public required string OtherRepo { get; init; }
    public required bool IsIncoming { get; init; }
    public ArcadeSubscription? Subscription { get; init; }
    public string? SourceBranch { get; init; }
    public List<FlowPr> Prs { get; init; } = [];

    /// <summary>Stable key for expand/collapse state.</summary>
    public string RowKey => Subscription?.Id ?? $"{OtherRepo}|{IsIncoming}|{Subscription?.Channel}|{Subscription?.TargetBranch}|{Prs.FirstOrDefault()?.TargetBranch}";
}
