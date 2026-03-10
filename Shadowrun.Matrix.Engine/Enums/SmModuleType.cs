namespace Shadowrun.Matrix.Enums;

/// <summary>
/// Identifies what physical building system a Slave Module (SM Node) controls.
/// Every corporate system is guaranteed to have both a Maglocks SM and a Cameras SM.
/// </summary>
public enum SmModuleType
{
    /// <summary>Controls all magnetic locks in the building. Shutting it off opens all doors.</summary>
    Maglocks,

    /// <summary>Controls all security cameras. Shutting it off disables surveillance.</summary>
    Cameras,

    /// <summary>Controls the alert system. Note: has NO effect on Matrix alerts or building alarms.</summary>
    AlertControl,

    /// <summary>Generic / unknown slave module with no significant game effect.</summary>
    Generic
}
