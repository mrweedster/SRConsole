using Shadowrun.Matrix.Core;
using Shadowrun.Matrix.Enums;
using Shadowrun.Matrix.ValueObjects;

namespace Shadowrun.Matrix.Models;

/// <summary>
/// A live instance of ICE (Intrusion Countermeasures Electronics) guarding a Node.
///
/// Holds all mutable runtime state: current health, probe position, alive flag,
/// and combat triggers. The immutable blueprint (type, base rating, weaknesses,
/// graphic) lives in <see cref="Spec"/>.
///
/// Combat flow summary:
/// <list type="number">
///   <item>Persona arrives at a Node containing ICE.</item>
///   <item>Persona runs programs. Each run is either a success or failure.</item>
///   <item>On failure, <see cref="OnProgramRunFailed"/> is called — ICE reacts
///         according to its type (probes, Tar effects, combat engagement).</item>
///   <item>On success against a weak-against program, <see cref="TakeDamage"/>
///         is called with enough damage to destroy the ICE outright, or partial
///         damage is applied for Attack hits.</item>
///   <item>Trace ICE advances its probe each tick via <see cref="AdvanceProbe"/>;
///         reaching the edge triggers a dump event.</item>
/// </list>
/// </summary>
public class Ice
{
    // ── Constants ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Hard ceiling on effective ICE rating regardless of alert state.
    /// A base-7 ICE under Active Alert would be 9, not 10.
    /// </summary>
    public const int MaxEffectiveRating = 9;

    /// <summary>
    /// Base probability (0–1) that Trace &amp; Burn damages the deck MPCP
    /// when its probe reaches the edge.
    /// </summary>
    public const float TraceAndBurnMpcpDamageChance = 0.6f;

    /// <summary>
    /// Base probability (0–1) that a probe reaching the screen edge
    /// triggers an alert escalation on Access/Barrier ICE.
    /// Scales with effective ICE rating.
    /// </summary>
    public const float BaseProbeAlertChance = 0.1f;

    // ── Identity ──────────────────────────────────────────────────────────────

    /// <summary>Unique instance identifier within a Node.</summary>
    public string Id { get; } = Guid.NewGuid().ToString("N");

    /// <summary>Immutable blueprint for this ICE instance.</summary>
    public IceSpec Spec { get; }

    // ── Runtime state ─────────────────────────────────────────────────────────

    /// <summary>
    /// Effective rating = base rating + (int)alert state, capped at
    /// <see cref="MaxEffectiveRating"/>. Updated by <see cref="RecalculateEffectiveRating"/>.
    /// Governs all combat rolls: attack power, hit frequency, hardness to crack.
    /// </summary>
    public int EffectiveRating { get; private set; }

    /// <summary>
    /// Remaining structural integrity. Reaches 0 when the ICE is destroyed.
    /// Scales with <see cref="EffectiveRating"/> — set at construction.
    /// </summary>
    public float CurrentHealth { get; private set; }

    /// <summary>Maximum health this ICE started with.</summary>
    public float MaxHealth { get; private set; }

    /// <summary>Whether this ICE is still active. False once health reaches 0 or it self-destructs.</summary>
    public bool IsAlive { get; private set; } = true;

    /// <summary>
    /// For Trace &amp; Burn / Trace &amp; Dump only.
    /// 0.0 = probe is at the ICE origin; 1.0 = probe has reached the screen edge.
    /// Null for all non-Trace ICE types.
    /// </summary>
    public float? ProbePosition { get; private set; }

    /// <summary>
    /// Whether the Persona is currently in active combat with this ICE.
    /// Once true, Deception can no longer be run.
    /// </summary>
    public bool IsInCombat { get; private set; }

    /// <summary>
    /// For Rebound: whether an active Rebound shield is currently deflecting
    /// attacks from this ICE instance.
    /// </summary>
    public bool ReboundActive { get; private set; }

    /// <summary>
    /// For Slow: current speed multiplier (1.0 = full speed, lower = slowed).
    /// </summary>
    public float SpeedMultiplier { get; private set; } = 1.0f;

    /// <summary>Remaining duration of an active Slow effect, in seconds.</summary>
    public float SlowDurationRemaining { get; private set; }

    /// <summary>
    /// For Barrier and Access: number of active probes currently crossing the screen.
    /// Each failed program run spawns one additional probe.
    /// </summary>
    public int ActiveProbeCount { get; private set; }

    // ── Random source ─────────────────────────────────────────────────────────

    private readonly Random _rng;

    // ── Construction ─────────────────────────────────────────────────────────

    /// <param name="spec">The ICE blueprint.</param>
    /// <param name="alertState">Current system alert at the moment of construction.</param>
    /// <param name="rng">
    /// Optional seeded random — inject for deterministic tests;
    /// omit to use a default unseeded instance.
    /// </param>
    public Ice(IceSpec spec, AlertState alertState = AlertState.Normal, Random? rng = null)
    {
        ArgumentNullException.ThrowIfNull(spec);

        Spec = spec;
        _rng = rng ?? Random.Shared;

        EffectiveRating = ComputeEffectiveRating(spec.BaseRating, alertState);
        MaxHealth       = ComputeMaxHealth(EffectiveRating);
        CurrentHealth   = MaxHealth;

        // Trace ICE initialises its probe at the origin
        if (spec.IsTraceType)
            ProbePosition = 0.0f;
    }

    // ── Rating recalculation ──────────────────────────────────────────────────

    /// <summary>
    /// Recalculates <see cref="EffectiveRating"/> and scales <see cref="MaxHealth"/>
    /// proportionally. Called whenever the system alert state changes.
    /// </summary>
    public void RecalculateEffectiveRating(AlertState alertState)
    {
        int newRating = ComputeEffectiveRating(Spec.BaseRating, alertState);
        if (newRating == EffectiveRating) return;

        // Scale current health proportionally to the new rating
        float healthRatio  = MaxHealth > 0 ? CurrentHealth / MaxHealth : 1f;
        EffectiveRating    = newRating;
        MaxHealth          = ComputeMaxHealth(newRating);
        CurrentHealth      = MaxHealth * healthRatio;
    }

    /// <summary>
    /// Computes effective rating from base rating and alert state.
    /// Capped at <see cref="MaxEffectiveRating"/>.
    /// </summary>
    public static int ComputeEffectiveRating(int baseRating, AlertState alertState) =>
        Math.Min(MaxEffectiveRating, baseRating + (int)alertState);

    // ── Damage ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Applies <paramref name="amount"/> damage to this ICE.
    /// Sets <see cref="IsAlive"/> to <c>false</c> when health reaches zero.
    /// </summary>
    /// <returns>True if this hit destroyed the ICE.</returns>
    public bool TakeDamage(float amount)
    {
        if (!IsAlive) return false;
        if (amount < 0f)
            throw new ArgumentOutOfRangeException(nameof(amount), "Damage must be non-negative.");

        CurrentHealth = Math.Max(0f, CurrentHealth - amount);

        if (CurrentHealth <= 0f)
        {
            Destroy();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Instantly destroys the ICE regardless of remaining health.
    /// Used for instant-kill program effects (Deception, Relocate).
    /// </summary>
    public void InstantKill()
    {
        CurrentHealth = 0f;
        Destroy();
    }

    // ── Program run responses ─────────────────────────────────────────────────

    /// <summary>
    /// Resolves this ICE's reaction to a failed program run by the Persona.
    /// Returns a list of <see cref="IceEvent"/>s for the session to process.
    ///
    /// Note: Tar ICE self-destructs after triggering; combat-type ICE may
    /// transition to an active combat state if not already there.
    /// </summary>
    public IReadOnlyList<IceEvent> OnProgramRunFailed(Program failedProgram)
    {
        ArgumentNullException.ThrowIfNull(failedProgram);

        if (!IsAlive)
            return Array.Empty<IceEvent>();

        var events = new List<IceEvent>();

        switch (Spec.Type)
        {
            case IceType.Access:
            case IceType.Barrier:
                events.AddRange(HandleProbeSpawn());
                break;

            case IceType.BlackIce:
            case IceType.Blaster:
            case IceType.Killer:
                events.AddRange(HandleCombatEngage());
                break;

            case IceType.TarPaper:
                events.AddRange(HandleTarPaper(failedProgram));
                break;

            case IceType.TarPit:
                events.AddRange(HandleTarPit(failedProgram));
                break;

            case IceType.TraceAndBurn:
            case IceType.TraceAndDump:
                // Trace ICE begins advancing its probe the moment it's triggered.
                // The probe was initialised at construction; combat is now active.
                events.AddRange(HandleCombatEngage());
                break;
        }

        return events.AsReadOnly();
    }

    /// <summary>
    /// Called when a program run that targets this ICE succeeds.
    /// Handles instant-kill for programs in <see cref="IceSpec.WeakAgainst"/>.
    /// </summary>
    /// <returns>True if the program instantly destroyed the ICE.</returns>
    public bool OnProgramRunSucceeded(ProgramName programName)
    {
        if (!IsAlive) return false;

        if (Spec.WeakAgainst.Contains(programName))
        {
            InstantKill();
            return true;
        }

        return false;
    }

    // ── Probe advancement (Trace ICE) ─────────────────────────────────────────

    /// <summary>
    /// Advances the Trace probe position by the distance it would travel in
    /// <paramref name="deltaTime"/> seconds. Only meaningful for Trace ICE.
    ///
    /// Returns a list of events — specifically a <see cref="PersonaDumpedEvent"/>
    /// and possibly a <see cref="DeckMpcpDamagedEvent"/> when the probe reaches
    /// the screen edge (position >= 1.0).
    /// </summary>
    public IReadOnlyList<IceEvent> AdvanceProbe(float deltaTime)
    {
        if (!IsAlive || !Spec.IsTraceType || ProbePosition is null)
            return Array.Empty<IceEvent>();

        if (deltaTime <= 0f) return Array.Empty<IceEvent>();

        float speed    = ComputeProbeSpeed();
        float newPos   = ProbePosition.Value + speed * deltaTime * SpeedMultiplier;
        ProbePosition  = Math.Min(1.0f, newPos);

        if (ProbePosition.Value < 1.0f)
            return Array.Empty<IceEvent>();

        // Probe reached the edge ─────────────────────────────────────────────
        return OnProbeReachesEdge();
    }

    /// <summary>
    /// Cancels the active probe — called when the ICE is hit while its probe
    /// is in transit (hitting Access/Barrier mid-probe cancels the probe).
    /// </summary>
    public void CancelProbe()
    {
        if (Spec.IsTraceType)
            ProbePosition = 0.0f;    // Reset to origin for Trace
        else
            ActiveProbeCount = Math.Max(0, ActiveProbeCount - 1);
    }

    // ── Status effects ────────────────────────────────────────────────────────

    /// <summary>
    /// Applies a Slow effect, reducing the ICE's effective speed for
    /// <paramref name="durationSeconds"/> seconds.
    /// </summary>
    public void ApplySlow(float speedMultiplier, float durationSeconds)
    {
        if (Spec.IsTraceType) return; // Slow has no effect on Trace ICE

        SpeedMultiplier       = Math.Clamp(speedMultiplier, 0.1f, 1.0f);
        SlowDurationRemaining = durationSeconds;
    }

    /// <summary>
    /// Activates a Rebound shield against this ICE's attacks.
    /// </summary>
    public void ActivateRebound() => ReboundActive = true;

    /// <summary>
    /// Deactivates the Rebound shield (called when all charges are consumed).
    /// </summary>
    public void DeactivateRebound() => ReboundActive = false;

    /// <summary>
    /// Advances time-based status effects (Slow duration).
    /// Called every tick by the session.
    /// </summary>
    public void TickStatusEffects(float deltaTime)
    {
        if (SlowDurationRemaining > 0f)
        {
            SlowDurationRemaining -= deltaTime;
            if (SlowDurationRemaining <= 0f)
            {
                SlowDurationRemaining = 0f;
                SpeedMultiplier       = 1.0f;
            }
        }
    }

    // ── Attack resolution ─────────────────────────────────────────────────────

    /// <summary>
    /// Resolves a periodic attack from this ICE against the Persona.
    /// Returns the damage dealt (0 if the attack missed or was deflected).
    ///
    /// Called by the session combat loop; does not apply damage directly —
    /// that is the session's responsibility so it can route BlackIce damage
    /// to physical health.
    /// </summary>
    /// <param name="evasionRating">Persona's current Evasion stat.</param>
    /// <param name="bodRating">Persona's current Bod stat for damage reduction.</param>
    /// <param name="hardening">Deck's Hardening value for damage reduction.</param>
    /// <returns>Net damage to apply; 0 if missed or fully absorbed.</returns>
    public float RollAttack(int evasionRating, int bodRating, int hardening)
    {
        if (!IsAlive || !IsInCombat) return 0f;
        if (Spec.IsTarType || Spec.IsTraceType) return 0f;

        // Miss check — higher evasion vs higher rating = more misses
        float hitChance = ComputeHitChance(evasionRating);
        if (_rng.NextDouble() > hitChance) return 0f;

        // Base damage scales with effective rating, varied by 2d6 roll
        float rawDamage = EffectiveRating * 1.5f * Roll2d6Scale();

        // Damage reduction from Bod + Hardening
        int totalDefense = bodRating + hardening;
        float reduction  = totalDefense * 0.4f;
        float netDamage  = Math.Max(0f, rawDamage - reduction);

        // Rebound: bounce damage back at the ICE instead of applying to Persona
        if (ReboundActive)
        {
            TakeDamage(netDamage);
            return 0f; // Persona takes no damage
        }

        return netDamage;
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if the given program instantly defeats this ICE on success.
    /// </summary>
    public bool IsInstantlyDefeatedBy(ProgramName programName) =>
        Spec.WeakAgainst.Contains(programName);

    /// <summary>
    /// Returns true if this ICE type must be defeated with the Attack program
    /// (Barrier and BlackIce — immune to all instant-kill programs).
    /// </summary>
    public bool MustBeAttacked =>
        Spec.Type is IceType.Barrier or IceType.BlackIce;

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Resets this ICE instance to full health and alive state for a node revisit.
    /// Called exclusively by <see cref="Node.ResetIceForRevisit"/>.
    /// </summary>
    internal void ResetHealth()
    {
        CurrentHealth     = MaxHealth;
        IsAlive           = true;
        IsInCombat        = false;
        ActiveProbeCount  = 0;
        ReboundActive     = false;
        SpeedMultiplier   = 1.0f;
        SlowDurationRemaining = 0f;

        if (Spec.IsTraceType)
            ProbePosition = 0.0f;
    }

    internal void ForceDestroy() => Destroy();

    private void Destroy()
    {
        IsAlive       = false;
        IsInCombat    = false;
        ProbePosition = null;
    }

    private IReadOnlyList<IceEvent> HandleProbeSpawn()
    {
        ActiveProbeCount++;

        // Alert chance scales slightly with effective rating
        float alertChance = BaseProbeAlertChance * (EffectiveRating / 4f);
        alertChance = Math.Clamp(alertChance, BaseProbeAlertChance, 0.5f);

        var events = new List<IceEvent> { new ProbeSpawnedEvent(alertChance) };

        // Entering combat if not already in it
        events.AddRange(HandleCombatEngage());

        return events;
    }

    private IReadOnlyList<IceEvent> HandleCombatEngage()
    {
        if (IsInCombat) return Array.Empty<IceEvent>();

        IsInCombat = true;
        return [new CombatEngagedEvent()];
    }

    /// <summary>
    /// Forces this ICE into combat state (called when the persona successfully
    /// damages but does not destroy it — the ICE retaliates immediately).
    /// </summary>
    internal void EngageCombat()
    {
        IsInCombat = true;
    }

    private IReadOnlyList<IceEvent> HandleTarPaper(Program failedProgram)
    {
        // Erase from memory only; program remains on deck and can be reloaded.
        // NOTE: Do NOT also emit ActiveAlertTriggeredEvent — ApplyIceEvent's
        // MemoryEraseEvent handler calls System.TriggerActiveAlert() directly,
        // which would cause a duplicate alert escalation.
        var events = new List<IceEvent>
        {
            new MemoryEraseEvent(failedProgram.Spec.Name.ToString()),
        };

        Destroy(); // Tar Paper vanishes after triggering
        return events;
    }

    private IReadOnlyList<IceEvent> HandleTarPit(Program failedProgram)
    {
        // Permanently delete program from deck.
        // NOTE: Do NOT also emit ActiveAlertTriggeredEvent — ApplyIceEvent's
        // PermanentEraseEvent handler calls System.TriggerActiveAlert() directly,
        // which would cause a duplicate alert escalation.
        var events = new List<IceEvent>
        {
            new PermanentEraseEvent(failedProgram.Spec.Name.ToString()),
        };

        Destroy(); // Tar Pit vanishes after triggering
        return events;
    }

    private IReadOnlyList<IceEvent> OnProbeReachesEdge()
    {
        var events = new List<IceEvent>();

        var dumpCause = Spec.Type == IceType.TraceAndBurn
            ? DumpCause.TraceAndBurn
            : DumpCause.TraceAndDump;

        events.Add(new PersonaDumpedEvent(dumpCause));

        // Trace & Burn has a chance to fry the deck's MPCP chip
        if (Spec.Type == IceType.TraceAndBurn)
        {
            float roll = (float)_rng.NextDouble();
            if (roll <= TraceAndBurnMpcpDamageChance)
                events.Add(new DeckMpcpDamagedEvent(roll));
        }

        Destroy();
        return events;
    }

    private float ComputeProbeSpeed()
    {
        // Probe crosses the screen in (10 - effectiveRating) seconds at base speed.
        // Minimum 1 second at rating 9.
        float baseDuration = Math.Max(1f, 10f - EffectiveRating);
        return 1.0f / baseDuration;
    }

    private float ComputeHitChance(int evasionRating)
    {
        // Hit chance = rating / (rating + evasion); always at least 5%
        float raw = (float)EffectiveRating / (EffectiveRating + Math.Max(0, evasionRating));
        return Math.Clamp(raw, 0.05f, 0.95f);
    }

    private static float ComputeMaxHealth(int effectiveRating) =>
        effectiveRating * 20f; // 20 HP per rating point — clean scaling

    /// <summary>
    /// Rolls 2d6 normalised to mean 1.0 (range ≈ 0.286–1.714).
    /// Applied to damage rolls so each attack feels distinct.
    /// </summary>
    private float Roll2d6Scale() => (_rng.Next(1, 7) + _rng.Next(1, 7)) / 7f;

    // ── Display ───────────────────────────────────────────────────────────────

    public override string ToString()
    {
        string status = IsAlive
            ? IsInCombat ? "COMBAT" : "Passive"
            : "Destroyed";

        string probe = ProbePosition.HasValue
            ? $" Probe:{ProbePosition.Value:P0}"
            : string.Empty;

        return $"[Ice] {Spec.Type} Rating:{EffectiveRating} " +
               $"HP:{CurrentHealth:F0}/{MaxHealth:F0} {status}{probe}";
    }
}
