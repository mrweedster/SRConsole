using Shadowrun.Matrix.Enums;

namespace Shadowrun.Matrix.Models;

/// <summary>
/// Lightweight read-only summary of a contracted Matrix run's objective.
/// Displayed on the CyberInfo screen when the Decker checks Run Info
/// while jacked into the target system.
/// </summary>
public class RunInfo
{
    /// <summary>Node ID the Decker must reach to complete the objective.</summary>
    public string               TargetNodeId    { get; }

    /// <summary>Human-readable label of the target node (e.g. "Research DS-3").</summary>
    public string               TargetNodeTitle { get; }

    /// <summary>The action the Decker must perform once inside the target node.</summary>
    public MatrixRunObjective   RequiredAction  { get; }

    /// <summary>
    /// The filename involved in data operations.
    /// Null for <see cref="MatrixRunObjective.CrashCpu"/>.
    /// </summary>
    public string?              ContractedFilename { get; }

    public RunInfo(
        string             targetNodeId,
        string             targetNodeTitle,
        MatrixRunObjective requiredAction,
        string?            contractedFilename = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetNodeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetNodeTitle);

        TargetNodeId       = targetNodeId;
        TargetNodeTitle    = targetNodeTitle;
        RequiredAction     = requiredAction;
        ContractedFilename = contractedFilename;
    }

    public override string ToString() =>
        $"[RunInfo] {RequiredAction} @ '{TargetNodeTitle}' ({TargetNodeId})" +
        (ContractedFilename is not null ? $" — file: '{ContractedFilename}'" : "");
}
