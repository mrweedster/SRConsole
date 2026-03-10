namespace Shadowrun.Matrix.Enums;

/// <summary>
/// Identifies each program (utility) available in the game.
/// </summary>
public enum ProgramName
{
    /// <summary>Destroys (crashes) ICE. Mandatory for Barrier and BlackIce.</summary>
    Attack,

    /// <summary>Reduces ICE reaction speed. No effect on Trace ICE.</summary>
    Slow,

    /// <summary>Lowers a Node's security rating. Available at levels 3 and 6 only.</summary>
    Degrade,

    /// <summary>Bounces attacks back at the attacker. Degrades with each deflection.</summary>
    Rebound,

    /// <summary>Repairs Persona energy. Must fully reload after each use.</summary>
    Medic,

    /// <summary>Reduces incoming damage per hit. Useless against BlackIce.</summary>
    Shield,

    /// <summary>Applies difficulty penalty to ALL actions for 4 seconds. Must reload after use.</summary>
    Smoke,

    /// <summary>Reduces ICE attack accuracy for 4 seconds. Must reload after use.</summary>
    Mirrors,

    /// <summary>Bypasses a Node without affecting its ICE. Works on all ICE types.</summary>
    Sleaze,

    /// <summary>
    /// Creates passcodes to instantly defeat Access, Blaster, Killer, and Trace ICE.
    /// No effect on Barrier or BlackIce. Only usable before combat starts.
    /// The most important program in the game.
    /// </summary>
    Deception,

    /// <summary>Instantly defeats Trace ICE. Redundant with Deception pre-combat.</summary>
    Relocate,

    /// <summary>Scans Node and ICE for information. May require multiple runs.</summary>
    Analyze
}
