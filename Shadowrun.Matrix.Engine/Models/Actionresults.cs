using Shadowrun.Matrix.Core;
using Shadowrun.Matrix.Enums;
using Shadowrun.Matrix.ValueObjects;

namespace Shadowrun.Matrix.Models;

/// <summary>
/// The outcome of a <see cref="Node.ExecuteAction"/> call.
/// Carries the success flag, which action was attempted, and any
/// side-effects on the broader game world.
/// </summary>
public class ActionResult
{
    public bool              Success     { get; }
    public string            NodeId      { get; }
    public NodeAction        Action      { get; }
    public string?           ErrorReason { get; }

    /// <summary>
    /// Side-effects produced by this action on the game world
    /// (e.g. cameras disabled, system crashed, teleported to node).
    /// Empty on failure.
    /// </summary>
    public IReadOnlyList<SystemEvent> SideEffects { get; }

    private ActionResult(
        bool                   success,
        string                 nodeId,
        NodeAction             action,
        IEnumerable<SystemEvent> sideEffects,
        string?                errorReason = null)
    {
        Success     = success;
        NodeId      = nodeId;
        Action      = action;
        SideEffects = sideEffects.ToList().AsReadOnly();
        ErrorReason = errorReason;
    }

    public static ActionResult Ok(string nodeId, NodeAction action,
        IEnumerable<SystemEvent>? sideEffects = null) =>
        new(true, nodeId, action, sideEffects ?? [], null);

    public static ActionResult Fail(string nodeId, NodeAction action, string reason) =>
        new(false, nodeId, action, [], reason);

    public override string ToString() =>
        Success
            ? $"ActionResult: OK [{Action} @ {NodeId}] — {SideEffects.Count} side-effects"
            : $"ActionResult: FAIL [{Action} @ {NodeId}] — {ErrorReason}";
}

/// <summary>
/// The outcome of a travel attempt between Nodes.
/// On success, carries the destination node ID and any ICE encountered there.
/// </summary>
public class TravelResult
{
    public bool              Success     { get; }
    public string            NodeId      { get; }
    public string?           ErrorReason { get; }

    /// <summary>
    /// ICE present at the destination node that must be dealt with.
    /// Empty if the node is unguarded or already conquered.
    /// </summary>
    public IReadOnlyList<Ice> EncounteredIce { get; }

    private TravelResult(
        bool          success,
        string        nodeId,
        IEnumerable<Ice> encounteredIce,
        string?       errorReason = null)
    {
        Success        = success;
        NodeId         = nodeId;
        EncounteredIce = encounteredIce.ToList().AsReadOnly();
        ErrorReason    = errorReason;
    }

    public static TravelResult Ok(string nodeId, IEnumerable<Ice>? encounteredIce = null) =>
        new(true, nodeId, encounteredIce ?? []);

    public static TravelResult Fail(string nodeId, string reason) =>
        new(false, nodeId, [], reason);

    public override string ToString() =>
        Success
            ? $"TravelResult: OK → {NodeId} (ICE: {EncounteredIce.Count})"
            : $"TravelResult: FAIL → {NodeId} — {ErrorReason}";
}

/// <summary>
/// The outcome of a data transfer attempt on a DS Node.
/// </summary>
public class DataTransferResult
{
    public bool      Success      { get; }
    public string    NodeId       { get; }
    public string?   ErrorReason  { get; }

    /// <summary>The file that was found and downloaded. Null if none was found or on failure.</summary>
    public DataFile? DownloadedFile { get; }

    /// <summary>Whether this was a mission-critical file transfer (upload/download/erase for a run).</summary>
    public bool      IsObjectiveTransfer { get; }

    private DataTransferResult(
        bool      success,
        string    nodeId,
        DataFile? downloadedFile,
        bool      isObjectiveTransfer,
        string?   errorReason)
    {
        Success             = success;
        NodeId              = nodeId;
        DownloadedFile      = downloadedFile;
        IsObjectiveTransfer = isObjectiveTransfer;
        ErrorReason         = errorReason;
    }

    public static DataTransferResult Ok(
        string    nodeId,
        DataFile? downloadedFile      = null,
        bool      isObjectiveTransfer = false) =>
        new(true, nodeId, downloadedFile, isObjectiveTransfer, null);

    /// <summary>No file found in this DS Node search, but no error occurred.</summary>
    public static DataTransferResult NotFound(string nodeId) =>
        new(true, nodeId, null, false, null);

    public static DataTransferResult Fail(string nodeId, string reason) =>
        new(false, nodeId, null, false, reason);

    public override string ToString() =>
        Success
            ? DownloadedFile is not null
                ? $"DataTransferResult: OK — Downloaded '{DownloadedFile.Name}'"
                : "DataTransferResult: OK — No file found this search"
            : $"DataTransferResult: FAIL — {ErrorReason}";
}
