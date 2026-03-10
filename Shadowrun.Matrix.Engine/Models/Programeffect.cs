using Shadowrun.Matrix.Core;
using Shadowrun.Matrix.Enums;

namespace Shadowrun.Matrix.Models;

// ═════════════════════════════════════════════════════════════════════════════
// ProgramEffect
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// The resolved outcome of a program's effect on the game world.
/// Produced by <see cref="ProgramEffectHandler"/> and consumed by
/// <see cref="MatrixSession"/> to update all affected state.
///
/// A ProgramEffect is the *what happened* after a successful or failed run;
/// <see cref="ProgramRunResult"/> is the *how it went* from the Persona's
/// perspective.
/// </summary>
public class ProgramEffect
{
    // ── Outcome ───────────────────────────────────────────────────────────────

    /// <summary>Whether the program's effect was applied (distinct from whether the run succeeded).</summary>
    public bool       Applied       { get; }

    public ProgramName ProgramName  { get; }

    /// <summary>Human-readable summary for the session log and UI.</summary>
    public string     Narrative     { get; }

    // ── State mutations ───────────────────────────────────────────────────────

    /// <summary>ICE events emitted by this effect (damage dealt, probes, alerts).</summary>
    public IReadOnlyList<IceEvent>    IceEvents     { get; }

    /// <summary>System-level side-effects (alert cancelled, SM disabled, etc.).</summary>
    public IReadOnlyList<SystemEvent> SystemEvents  { get; }

    /// <summary>Amount of Persona energy healed (Medic effect).</summary>
    public float EnergyHealed { get; }

    /// <summary>Amount of damage dealt to the target ICE (Attack, Rebound reflect).</summary>
    public float DamageToIce  { get; }

    /// <summary>
    /// For Degrade: the security rating reduction applied to the current node.
    /// </summary>
    public int SecurityRatingDelta { get; }

    /// <summary>
    /// For Slow / Mirrors: the duration in seconds the effect lasts.
    /// Zero when not applicable.
    /// </summary>
    public float StatusEffectDuration { get; }

    /// <summary>
    /// For Slow: the speed multiplier applied to the target ICE (0.1–1.0).
    /// </summary>
    public float SlowSpeedMultiplier { get; }

    /// <summary>
    /// True when Sleaze successfully bypassed the node without defeating its ICE.
    /// The node is passable but not conquered.
    /// </summary>
    public bool NodeBypassed { get; }

    /// <summary>
    /// True when Analyze completed a full scan of the node.
    /// </summary>
    public bool NodeFullyScanned { get; }

    /// <summary>
    /// For Shield: the total damage the shield can absorb before breaking.
    /// Zero when not applicable.
    /// </summary>
    public float ShieldStrength { get; }

    // ── Construction ─────────────────────────────────────────────────────────

    private ProgramEffect(
        bool                        applied,
        ProgramName                 programName,
        string                      narrative,
        IEnumerable<IceEvent>?      iceEvents           = null,
        IEnumerable<SystemEvent>?   systemEvents        = null,
        float                       energyHealed        = 0f,
        float                       damageToIce         = 0f,
        int                         securityRatingDelta = 0,
        float                       statusEffectDuration = 0f,
        float                       slowSpeedMultiplier  = 1f,
        bool                        nodeBypassed         = false,
        bool                        nodeFullyScanned     = false,
        float                       shieldStrength       = 0f)
    {
        Applied               = applied;
        ProgramName           = programName;
        Narrative             = narrative;
        IceEvents             = (iceEvents    ?? []).ToList().AsReadOnly();
        SystemEvents          = (systemEvents ?? []).ToList().AsReadOnly();
        EnergyHealed          = energyHealed;
        DamageToIce           = damageToIce;
        SecurityRatingDelta   = securityRatingDelta;
        StatusEffectDuration  = statusEffectDuration;
        SlowSpeedMultiplier   = slowSpeedMultiplier;
        NodeBypassed          = nodeBypassed;
        NodeFullyScanned      = nodeFullyScanned;
        ShieldStrength        = shieldStrength;
    }

    // ── Static factories ──────────────────────────────────────────────────────

    public static ProgramEffect Success(
        ProgramName               name,
        string                    narrative,
        IEnumerable<IceEvent>?    iceEvents    = null,
        IEnumerable<SystemEvent>? systemEvents = null,
        float                     energyHealed = 0f,
        float                     damageToIce  = 0f,
        int   securityRatingDelta  = 0,
        float statusEffectDuration = 0f,
        float slowSpeedMultiplier  = 1f,
        bool  nodeBypassed         = false,
        bool  nodeFullyScanned     = false,
        float shieldStrength       = 0f) =>
        new(true, name, narrative, iceEvents, systemEvents,
            energyHealed, damageToIce, securityRatingDelta,
            statusEffectDuration, slowSpeedMultiplier,
            nodeBypassed, nodeFullyScanned, shieldStrength);

    public static ProgramEffect Failure(ProgramName name, string narrative) =>
        new(false, name, narrative);

    // ── Display ───────────────────────────────────────────────────────────────

    public override string ToString() =>
        $"[ProgramEffect] {ProgramName} — {(Applied ? "Applied" : "Failed")}: {Narrative}";
}

// ═════════════════════════════════════════════════════════════════════════════
// SessionEvent  —  append-only run log
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// An entry in the <see cref="MatrixSession"/> event log.
/// Provides a timestamped, typed record of everything that happened
/// during a jack-in session for debugging, replay, and UI display.
/// </summary>
public class SessionEvent
{
    public SessionEventType Type        { get; }
    public string           Description { get; }
    public DateTimeOffset   Timestamp   { get; } = DateTimeOffset.UtcNow;

    public SessionEvent(SessionEventType type, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Type        = type;
        Description = description;
    }

    public override string ToString() =>
        $"[{Timestamp:HH:mm:ss.fff}] {Type,-22} {Description}";
}

/// <summary>Broad categories for session log entries.</summary>
public enum SessionEventType
{
    SessionStarted,
    SessionEnded,
    NodeEntered,
    NodeConquered,
    NodeBypassed,
    CombatEngaged,
    CombatMiss,
    ProgramRun,
    ProgramFailed,
    IceDefeated,
    PersonaDamaged,
    PersonaDumped,
    DeckDamaged,
    AlertEscalated,
    AlertCancelled,
    DataFileFound,
    DataFileTransferred,
    RunObjectiveMet,
    JackOutAttempted,
    JackOutSucceeded,
    TarEffectTriggered,
    SystemCrashed,
    SlaveModeDisabled,
    NodeActionResult,
    NodeActionSuccess,   // DS transfer / erase succeeded  → green
    NodeActionFailure,   // DS transfer / erase failed     → red
}

// ═════════════════════════════════════════════════════════════════════════════
// RunCompletionResult
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// The outcome of completing a contracted Matrix run via
/// <see cref="MatrixSession.CompleteRun"/>.
/// </summary>
public class RunCompletionResult
{
    public bool   Success      { get; }
    public int    NuyenEarned  { get; }
    public int    KarmaEarned  { get; }
    public string? ErrorReason { get; }

    private RunCompletionResult(bool success, int nuyen, int karma, string? error)
    {
        Success     = success;
        NuyenEarned = nuyen;
        KarmaEarned = karma;
        ErrorReason = error;
    }

    public static RunCompletionResult Ok(int nuyen, int karma) =>
        new(true, nuyen, karma, null);

    public static RunCompletionResult Fail(string reason) =>
        new(false, 0, 0, reason);

    public override string ToString() =>
        Success
            ? $"RunCompletionResult: OK — {NuyenEarned}¥ +{KarmaEarned} Karma"
            : $"RunCompletionResult: FAIL — {ErrorReason}";
}
