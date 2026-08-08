namespace suggu.Core.Planning;

/// <summary>What happened to a single operation when the executor applied (or previewed) it.</summary>
public enum OperationStatus
{
    Created,
    Skipped,
    Overwritten,
    Deleted,
    Failed,
}

/// <summary>One operation's outcome. <paramref name="Message"/> carries the failure reason or skip cause.</summary>
public sealed record OperationResult(Operation Operation, OperationStatus Status, string? Message = null);

/// <summary>
/// The one report shape every command returns. The CLI renders it as human output;
/// --json serializes it. Decided once, reused everywhere.
/// </summary>
public sealed record ExecutionReport(
    string PlanDescription,
    bool DryRun,
    IReadOnlyList<OperationResult> Results)
{
    public IEnumerable<OperationResult> Created => Results.Where(r => r.Status == OperationStatus.Created);
    public IEnumerable<OperationResult> Skipped => Results.Where(r => r.Status == OperationStatus.Skipped);
    public IEnumerable<OperationResult> Overwritten => Results.Where(r => r.Status == OperationStatus.Overwritten);
    public IEnumerable<OperationResult> Deleted => Results.Where(r => r.Status == OperationStatus.Deleted);
    public IEnumerable<OperationResult> Failed => Results.Where(r => r.Status == OperationStatus.Failed);

    public bool Success => !Failed.Any();
}
