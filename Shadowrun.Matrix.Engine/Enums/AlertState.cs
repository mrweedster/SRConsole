namespace Shadowrun.Matrix.Enums;

/// <summary>
/// Tracks the system-wide security status.
/// ICE effective rating = baseRating + (int)AlertState, capped at 9.
/// </summary>
public enum AlertState
{
    /// <summary>No alert active. ICE operates at base rating.</summary>
    Normal = 0,

    /// <summary>Passive alert triggered. ICE rating +1.</summary>
    Passive = 1,

    /// <summary>Active alert triggered. ICE rating +2.</summary>
    Active = 2
}
