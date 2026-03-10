namespace Shadowrun.Matrix.Enums;

/// <summary>
/// All possible actions a Persona can perform inside a Node.
/// Available actions depend on the Node's type and current state.
/// </summary>
public enum NodeAction
{
    // ── Shared ──────────────────────────────────────────────────────────────

    /// <summary>Exit the Node and travel to an adjacent Node. (DS, IOP, SM, SPU)</summary>
    LeaveNode,

    // ── SAN ─────────────────────────────────────────────────────────────────

    /// <summary>Move from the SAN into the system proper. (SAN only)</summary>
    EnterSystem,

    // ── CPU ─────────────────────────────────────────────────────────────────

    /// <summary>Teleport directly to any Node on the system map. (CPU only)</summary>
    GoToNode,

    /// <summary>Reset Passive and Active alert states to Normal. (CPU only)</summary>
    CancelAlert,

    /// <summary>
    /// Crash the CPU, ejecting the Persona and disabling all cameras
    /// and maglocks in the associated building. (CPU only)
    /// </summary>
    CrashSystem,

    // ── DS ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Upload, download, or search for random data files. (DS only)
    /// Behaviour varies depending on whether a Matrix run is active.
    /// </summary>
    TransferData,

    /// <summary>Delete a specific file from the DS. (DS only)</summary>
    Erase,

    // ── IOP ──────────────────────────────────────────────────────────────────

    /// <summary>Lock out the IOP. Effect appears cosmetic only. (IOP only)</summary>
    Lockout,

    // ── SM ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Take the Slave Module offline. Disables the physical system it controls
    /// (cameras or maglocks). (SM only)
    /// </summary>
    TurnOffNode
}
