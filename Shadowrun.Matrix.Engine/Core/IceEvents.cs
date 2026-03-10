namespace Shadowrun.Matrix.Core;

// ── Base ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Base class for all events that ICE can emit during a Matrix encounter.
/// Events are produced by Ice methods and consumed by MatrixSession to update
/// system state, Persona health, deck integrity, and alert levels.
/// </summary>
public abstract class IceEvent
{
    /// <summary>Human-readable description for logging / UI display.</summary>
    public abstract string Description { get; }
}

// ── Alert events ─────────────────────────────────────────────────────────────

/// <summary>
/// A probe spawned by Access or Barrier ICE is travelling across the screen.
/// Each probe that reaches the edge has <see cref="AlertChance"/> probability
/// of escalating the system's alert state.
/// </summary>
public sealed class ProbeSpawnedEvent : IceEvent
{
    /// <summary>0.0–1.0 probability that this probe triggers an alert if it reaches the edge.</summary>
    public float AlertChance { get; }

    public ProbeSpawnedEvent(float alertChance)
    {
        if (alertChance is < 0f or > 1f)
            throw new ArgumentOutOfRangeException(nameof(alertChance),
                "Alert chance must be between 0.0 and 1.0.");
        AlertChance = alertChance;
    }

    public override string Description =>
        $"Probe spawned — {AlertChance:P0} chance of triggering alert if it reaches the edge.";
}

/// <summary>
/// The system alert state has been escalated to Active.
/// Triggered immediately by Tar Paper and Tar Pit.
/// </summary>
public sealed class ActiveAlertTriggeredEvent : IceEvent
{
    public override string Description => "Active alert triggered by ICE.";
}

// ── Program destruction events ───────────────────────────────────────────────

/// <summary>
/// Tar Paper has erased a program from the Persona's loaded memory slots.
/// The program still exists on the deck and can be reloaded.
/// </summary>
public sealed class MemoryEraseEvent : IceEvent
{
    /// <summary>Name of the program that was erased from memory.</summary>
    public string ProgramName { get; }

    public MemoryEraseEvent(string programName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(programName);
        ProgramName = programName;
    }

    public override string Description =>
        $"Tar Paper erased '{ProgramName}' from memory. Program must be reloaded.";
}

/// <summary>
/// Tar Pit has permanently deleted a program from the deck's storage.
/// The program cannot be recovered without repurchasing it.
/// </summary>
public sealed class PermanentEraseEvent : IceEvent
{
    /// <summary>Name of the program that was permanently destroyed.</summary>
    public string ProgramName { get; }

    public PermanentEraseEvent(string programName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(programName);
        ProgramName = programName;
    }

    public override string Description =>
        $"Tar Pit permanently deleted '{ProgramName}' from the deck!";
}

// ── Persona / Decker damage events ───────────────────────────────────────────

/// <summary>
/// The Persona has been dumped from the system. Occurs when:
/// - Persona energy reaches zero, or
/// - A Trace probe reaches the edge of the screen.
/// </summary>
public sealed class PersonaDumpedEvent : IceEvent
{
    /// <summary>Root cause for UI / logging purposes.</summary>
    public DumpCause Cause { get; }

    public PersonaDumpedEvent(DumpCause cause) => Cause = cause;

    public override string Description => $"Persona dumped from system. Cause: {Cause}.";
}

/// <summary>Reason a Persona was ejected from the Matrix.</summary>
public enum DumpCause
{
    /// <summary>Persona energy was drained to zero.</summary>
    EnergyDepleted,

    /// <summary>A Trace &amp; Dump probe reached the screen edge.</summary>
    TraceAndDump,

    /// <summary>A Trace &amp; Burn probe reached the screen edge.</summary>
    TraceAndBurn,

    /// <summary>Persona was blocked while trying to jack out against live BlackIce.</summary>
    JackOutBlocked
}

/// <summary>
/// Trace &amp; Burn ICE has damaged the deck's MPCP chip upon dumping the Persona.
/// The deck is now broken and cannot be used until repaired at a computer shop.
/// </summary>
public sealed class DeckMpcpDamagedEvent : IceEvent
{
    /// <summary>
    /// 0.0–1.0 probability that was rolled to determine whether the deck was fried.
    /// Included for audit / replay purposes.
    /// </summary>
    public float RollProbability { get; }

    public DeckMpcpDamagedEvent(float rollProbability)
    {
        if (rollProbability is < 0f or > 1f)
            throw new ArgumentOutOfRangeException(nameof(rollProbability));
        RollProbability = rollProbability;
    }

    public override string Description =>
        $"Deck MPCP damaged by Trace & Burn (roll: {RollProbability:P0}). Deck must be repaired.";
}

/// <summary>
/// BlackIce has dealt damage directly to the Decker's physical health,
/// bypassing the Persona's energy meter entirely.
/// </summary>
public sealed class PhysicalDamageDealtEvent : IceEvent
{
    public float Amount { get; }

    public PhysicalDamageDealtEvent(float amount)
    {
        if (amount < 0f)
            throw new ArgumentOutOfRangeException(nameof(amount), "Damage must be non-negative.");
        Amount = amount;
    }

    public override string Description =>
        $"BlackIce dealt {Amount:F1} physical damage directly to the Decker.";
}

// ── Combat state events ───────────────────────────────────────────────────────

/// <summary>
/// ICE has become hostile. Combat state transitions from None → Active.
/// Deception can no longer be used.
/// </summary>
public sealed class CombatEngagedEvent : IceEvent
{
    public override string Description => "Combat engaged — ICE is now hostile.";
}
