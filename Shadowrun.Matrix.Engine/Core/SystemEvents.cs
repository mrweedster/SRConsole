namespace Shadowrun.Matrix.Core;

// ── Base ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Base class for side-effects that Node actions can produce on the broader
/// game world (building systems, alert state, session flow).
/// Consumed by MatrixSession and passed up to the game layer.
/// </summary>
public abstract class SystemEvent
{
    public abstract string Description { get; }
}

// ── CPU actions ───────────────────────────────────────────────────────────────

/// <summary>
/// The CPU's CancelAlert action has reset the system alert state to Normal.
/// Also cancels any active building alarm in the associated corp.
/// </summary>
public sealed class AlertCancelledEvent : SystemEvent
{
    public override string Description => "System alert cancelled via CPU. All ICE ratings return to base.";
}

/// <summary>
/// The CPU has been crashed. The Persona is ejected, all cameras and maglocks
/// in the associated building are deactivated, and the CPU is disabled.
/// </summary>
public sealed class SystemCrashedEvent : SystemEvent
{
    /// <summary>
    /// True when the crash was triggered from inside the physical building,
    /// meaning the disabling of cameras and maglocks is immediately relevant.
    /// </summary>
    public bool CrashedFromInsideBuilding { get; }

    public SystemCrashedEvent(bool crashedFromInsideBuilding)
        => CrashedFromInsideBuilding = crashedFromInsideBuilding;

    public override string Description =>
        CrashedFromInsideBuilding
            ? "CPU crashed from inside building — cameras and maglocks deactivated."
            : "CPU crashed — system is down.";
}

/// <summary>
/// The CPU's GoToNode action has teleported the Persona to a target Node.
/// This is a one-way trip; the Persona must fight through intermediate Nodes to return.
/// </summary>
public sealed class TeleportedToNodeEvent : SystemEvent
{
    public string TargetNodeId { get; }

    public TeleportedToNodeEvent(string targetNodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetNodeId);
        TargetNodeId = targetNodeId;
    }

    public override string Description => $"Persona teleported to Node '{TargetNodeId}'.";
}

// ── SM actions ────────────────────────────────────────────────────────────────

/// <summary>
/// A Slave Module has been taken offline.
/// The physical building system it controls is now disabled.
/// </summary>
public sealed class SlaveModuleDisabledEvent : SystemEvent
{
    public string ModuleNodeId   { get; }
    public string ModuleTypeName { get; }

    public SlaveModuleDisabledEvent(string moduleNodeId, string moduleTypeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleNodeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleTypeName);
        ModuleNodeId   = moduleNodeId;
        ModuleTypeName = moduleTypeName;
    }

    public override string Description =>
        $"Slave Module '{ModuleNodeId}' ({ModuleTypeName}) disabled — building system is offline.";
}

// ── Data transfer ─────────────────────────────────────────────────────────────

/// <summary>
/// A data file was successfully downloaded to the Decker's deck during a DS visit.
/// If the file is plot-relevant it will be moved to the Decker's notebook on jack-out.
/// </summary>
public sealed class DataFileDownloadedEvent : SystemEvent
{
    public string FileId         { get; }
    public string FileName       { get; }
    public bool   IsPlotRelevant { get; }

    public DataFileDownloadedEvent(string fileId, string fileName, bool isPlotRelevant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        FileId         = fileId;
        FileName       = fileName;
        IsPlotRelevant = isPlotRelevant;
    }

    public override string Description =>
        IsPlotRelevant
            ? $"Interesting file '{FileName}' downloaded — check notebook after jack-out."
            : $"Data file '{FileName}' downloaded. Available for sale at Roscoe's.";
}

// ── Session flow ──────────────────────────────────────────────────────────────

/// <summary>
/// The Persona has disconnected from the Matrix cleanly via Jack Out.
/// </summary>
public sealed class PersonaDisconnectedEvent : SystemEvent
{
    public override string Description => "Persona disconnected — jack-out successful.";
}
