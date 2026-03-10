namespace Shadowrun.Matrix.Enums;

/// <summary>
/// Functional type of a Node in a Matrix system.
/// Each type exposes a different set of available actions.
/// </summary>
public enum NodeType
{
    /// <summary>
    /// Central Processing Unit. One per system. Brain of the network.
    /// Actions: GoToNode, CancelAlert, CrashSystem.
    /// </summary>
    CPU,

    /// <summary>
    /// Datastore. Holds data files for transfer, upload, or erasure.
    /// Actions: LeaveNode, TransferData, Erase.
    /// </summary>
    DS,

    /// <summary>
    /// Input/Output Port. Entry point when jacking in from inside a building.
    /// Actions: LeaveNode, Lockout.
    /// </summary>
    IOP,

    /// <summary>
    /// System Access Node. Entry point from a public terminal. One per system.
    /// Actions: EnterSystem.
    /// </summary>
    SAN,

    /// <summary>
    /// Slave Module. Controls physical building systems (cameras, maglocks).
    /// Actions: LeaveNode, TurnOffNode.
    /// </summary>
    SM,

    /// <summary>
    /// Sub-Processor Unit. Structural backbone node with no special function.
    /// Actions: LeaveNode.
    /// </summary>
    SPU
}
