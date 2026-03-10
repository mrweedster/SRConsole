using Shadowrun.Matrix.Core;
using Shadowrun.Matrix.Enums;

namespace Shadowrun.Matrix.Models;

/// <summary>
/// The full outcome of a single program run attempt by the Persona.
///
/// Carries whether the run succeeded, which program and ICE were involved,
/// all <see cref="IceEvent"/>s emitted during the resolution, and whether
/// the encounter transitioned into active combat.
/// </summary>
public class ProgramRunResult
{
    // ── Outcome ───────────────────────────────────────────────────────────────

    /// <summary>Whether the program execution succeeded against the target ICE.</summary>
    public bool Succeeded { get; }

    /// <summary>
    /// True when the run was rejected before the dice were ever rolled —
    /// e.g. slot empty, program still loading, Deception attempted in combat.
    /// False for all real game outcomes (hits and misses alike).
    /// Used by the session to skip side-effects (Tar trigger, combat log) that
    /// are only meaningful for actual attempts.
    /// </summary>
    public bool IsPreflightFailure { get; }

    /// <summary>
    /// True when the Attack program failed its dice roll against ICE — a combat
    /// miss. Distinguished from a program failure (e.g. Slow, Deception failing)
    /// because a miss does not trigger hidden Tar ICE.
    /// Always false for non-Attack programs.
    /// </summary>
    public bool IsMiss { get; }

    // ── Context ───────────────────────────────────────────────────────────────

    /// <summary>The program that was run.</summary>
    public Program Program { get; }

    /// <summary>
    /// The ICE that was targeted. Null for non-combat programs
    /// (Medic, Analyze, Shield) that do not target ICE directly.
    /// </summary>
    public Ice? TargetIce { get; }

    /// <summary>Slot index (0–4) the program was run from.</summary>
    public int SlotIndex { get; }

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>
    /// All <see cref="IceEvent"/>s produced during this run, in the order they
    /// occurred. May include probe spawns, alert triggers, Tar effects,
    /// physical damage, and dump events. Empty for non-combat programs.
    /// </summary>
    public IReadOnlyList<IceEvent> Events { get; }

    // ── State changes ─────────────────────────────────────────────────────────

    /// <summary>
    /// True if this run caused a <see cref="CombatState"/> transition from
    /// <see cref="CombatState.None"/> to <see cref="CombatState.Active"/>.
    /// </summary>
    public bool CombatStateChanged { get; }

    /// <summary>
    /// True if the targeted ICE was destroyed as a result of this run.
    /// </summary>
    public bool IceDestroyed { get; }

    // ── Construction ─────────────────────────────────────────────────────────

    public ProgramRunResult(
        bool                   succeeded,
        Program                program,
        int                    slotIndex,
        Ice?                   targetIce,
        IEnumerable<IceEvent>  events,
        bool                   combatStateChanged,
        bool                   iceDestroyed,
        bool                   isPreflightFailure = false,
        bool                   isMiss             = false)
    {
        ArgumentNullException.ThrowIfNull(program);
        if (slotIndex is < 0 or > 4)
            throw new ArgumentOutOfRangeException(nameof(slotIndex), "Slot index must be 0–4.");

        Succeeded           = succeeded;
        IsPreflightFailure  = isPreflightFailure;
        IsMiss              = isMiss;
        Program             = program;
        SlotIndex           = slotIndex;
        TargetIce           = targetIce;
        Events              = events.ToList().AsReadOnly();
        CombatStateChanged  = combatStateChanged;
        IceDestroyed        = iceDestroyed;
    }

    // ── Convenience queries ───────────────────────────────────────────────────

    /// <summary>Returns true if the Persona was dumped as a result of this run.</summary>
    public bool PersonaWasDumped =>
        Events.OfType<PersonaDumpedEvent>().Any();

    /// <summary>Returns true if a Tar Pit permanently destroyed the program.</summary>
    public bool ProgramWasPermanentlyDeleted =>
        Events.OfType<PermanentEraseEvent>().Any();

    /// <summary>Returns true if Tar Paper erased the program from memory.</summary>
    public bool ProgramWasMemoryErased =>
        Events.OfType<MemoryEraseEvent>().Any();

    /// <summary>Returns true if Active Alert was triggered during this run.</summary>
    public bool ActiveAlertTriggered =>
        Events.OfType<ActiveAlertTriggeredEvent>().Any();

    // ── Display ───────────────────────────────────────────────────────────────

    public override string ToString()
    {
        string outcome  = Succeeded ? "SUCCESS" : "FAILURE";
        string ice      = TargetIce is not null ? $" vs {TargetIce.Spec.Type}" : "";
        string eventSummary = Events.Count > 0
            ? $" [{string.Join(", ", Events.Select(e => e.GetType().Name))}]"
            : "";

        return $"ProgramRunResult: {outcome} — {Program.Spec.Name} L{Program.Spec.Level}" +
               $"{ice}{eventSummary}";
    }
}

/// <summary>
/// The outcome of a jack-out attempt.
/// A clean jack-out always succeeds eventually, but BlackIce may block it
/// and deal physical damage before the Persona escapes.
/// </summary>
public class JackOutResult
{
    public bool  Succeeded              { get; }
    public bool  BlockedByBlackIce      { get; }
    public float PhysicalDamageDealt    { get; }
    public string? ErrorReason          { get; }

    private JackOutResult(
        bool    succeeded,
        bool    blockedByBlackIce,
        float   physicalDamageDealt,
        string? errorReason)
    {
        Succeeded           = succeeded;
        BlockedByBlackIce   = blockedByBlackIce;
        PhysicalDamageDealt = physicalDamageDealt;
        ErrorReason         = errorReason;
    }

    public static JackOutResult Clean() =>
        new(true, false, 0f, null);

    public static JackOutResult BlockedWithDamage(float damage) =>
        new(true, true, damage, null);

    public static JackOutResult Failed(string reason) =>
        new(false, false, 0f, reason);

    public override string ToString() =>
        Succeeded
            ? BlockedByBlackIce
                ? $"JackOutResult: OK (blocked by BlackIce — {PhysicalDamageDealt:F1} physical dmg taken)"
                : "JackOutResult: Clean disconnect"
            : $"JackOutResult: FAIL — {ErrorReason}";
}
