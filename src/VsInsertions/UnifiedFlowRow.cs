namespace VsInsertions;

public sealed class UnifiedFlowRow
{
    public required string OtherRepo { get; init; }
    public required bool IsIncoming { get; init; }
    public ArcadeSubscription? Subscription { get; init; }
    public string? SourceBranch { get; init; }
    public List<FlowPr> Prs { get; init; } = [];

    /// <summary>Whether this row represents a same-repo branch flow (no subscription).</summary>
    public bool IsSameRepo { get; init; }

    /// <summary>
    /// Whether the source repo/branch is up-to-date with the target's source manifest.
    /// null = not checked, true = source has no new changes, false = source has new changes.
    /// </summary>
    public bool? SourceUpToDate { get; set; }

    /// <summary>The commit SHA recorded in the target repo's source manifest for this source repo.</summary>
    public string? ManifestCommitSha { get; set; }

    /// <summary>The latest commit SHA on the source repo's branch.</summary>
    public string? LatestSourceCommitSha { get; set; }

    /// <summary>Stable key for expand/collapse state.</summary>
    public string RowKey => Subscription?.Id ?? $"{OtherRepo}|{IsIncoming}|{Subscription?.Channel}|{Subscription?.TargetBranch}|{Prs.FirstOrDefault()?.TargetBranch}";
}
