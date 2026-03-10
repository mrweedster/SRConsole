namespace Shadowrun.Matrix.Enums;

/// <summary>
/// Type of Intrusion Countermeasure Electronics (ICE) guarding a Node.
/// Determines combat behaviour, weaknesses, and triggered effects.
/// </summary>
public enum IceType
{
    /// <summary>
    /// Sends one probe per failed program run. Probe reaching the edge
    /// has a small chance of triggering an alert. Weak against Deception.
    /// </summary>
    Access,

    /// <summary>
    /// Sends probes like Access but must be destroyed via Attack.
    /// Immune to Deception and all other instant-kill programs.
    /// </summary>
    Barrier,

    /// <summary>
    /// Periodically attacks the Decker's physical health directly, bypassing
    /// Persona energy. Must be attacked. Immune to Shield.
    /// </summary>
    BlackIce,

    /// <summary>
    /// Periodically attacks the Persona's energy. Weak against Deception.
    /// Often hides behind Barrier.
    /// </summary>
    Blaster,

    /// <summary>
    /// Attacks the Persona more frequently than Blaster. Weak against Deception.
    /// Often hides behind Access.
    /// </summary>
    Killer,

    /// <summary>
    /// Hidden behind a primary ICE. On a failed program run: erases the program
    /// from memory only (not the deck), triggers Active Alert, then vanishes.
    /// </summary>
    TarPaper,

    /// <summary>
    /// Hidden behind a primary ICE. On a failed program run: permanently deletes
    /// the program from the deck, triggers Active Alert, then vanishes.
    /// </summary>
    TarPit,

    /// <summary>
    /// Sends a slow tracking probe across the screen. If probe reaches the edge:
    /// Persona is dumped AND deck MPCP may be permanently damaged.
    /// Weak against Deception and Relocate.
    /// </summary>
    TraceAndBurn,

    /// <summary>
    /// Sends a slow tracking probe across the screen. If probe reaches the edge:
    /// Persona is dumped (no deck damage).
    /// Weak against Deception and Relocate.
    /// </summary>
    TraceAndDump
}
