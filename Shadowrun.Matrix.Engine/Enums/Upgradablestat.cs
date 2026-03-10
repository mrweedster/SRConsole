namespace Shadowrun.Matrix.Enums;

/// <summary>
/// Identifies a Cyberdeck statistic that can be raised through hardware upgrades.
/// Used as the selector argument for <c>Cyberdeck.CanUpgradeStat</c> and
/// <c>Cyberdeck.UpgradeStat</c> to avoid stringly-typed dispatch.
///
/// Base values (MPCP, Hardening) are intentionally absent — they are immutable
/// and cannot be changed on an existing deck.
/// </summary>
public enum UpgradableStat
{
    // ── Hardware stats ────────────────────────────────────────────────────────

    /// <summary>
    /// Overall deck speed. Hard-capped at 3 across all decks, regardless of MPCP.
    /// </summary>
    Response,

    /// <summary>Current loaded-program capacity in Megapulses.</summary>
    Memory,

    /// <summary>Maximum memory ceiling — raises what Memory can be upgraded to.</summary>
    MemoryMax,

    /// <summary>Total storage in Megapulses (programs + data files).</summary>
    Storage,

    /// <summary>Maximum storage ceiling — raises what Storage can be upgraded to.</summary>
    StorageMax,

    /// <summary>In-session program loading speed.</summary>
    LoadIoSpeed,

    /// <summary>Maximum Load/IO Speed ceiling.</summary>
    LoadIoSpeedMax,

    // ── Attributes (all capped at MPCP) ──────────────────────────────────────

    /// <summary>Effective armor/defense rating. Distinct from the immutable Hardening base.</summary>
    Bod,

    /// <summary>Dodge rating against incoming ICE attacks.</summary>
    Evasion,

    /// <summary>Stealth rating. Reduces detection and multiplies Deception success chance.</summary>
    Masking,

    /// <summary>Improves Analyze program success rate.</summary>
    Sensor
}
