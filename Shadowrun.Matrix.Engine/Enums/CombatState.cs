namespace Shadowrun.Matrix.Enums;

/// <summary>
/// Tracks whether the Persona is currently in combat with ICE at the current Node.
/// Certain programs (e.g. Deception) can only be used in the None state.
/// </summary>
public enum CombatState
{
    /// <summary>
    /// No ICE has been triggered. The pre-combat window is open.
    /// All programs are available. Medic, Analyze, Shield etc. should be used here.
    /// </summary>
    None,

    /// <summary>
    /// Combat has been engaged — either by a failed program run or by attacking directly.
    /// Deception can no longer be used.
    /// </summary>
    Active
}
