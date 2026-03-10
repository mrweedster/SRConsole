using Shadowrun.Matrix.Core;
using Shadowrun.Matrix.Enums;

namespace Shadowrun.Matrix.Models;

/// <summary>
/// The Persona is the Decker's electronic manifestation inside the Matrix.
/// It is the entity that travels between nodes, runs programs against ICE,
/// takes energy damage, and ultimately jacks out — cleanly or otherwise.
///
/// Health model:
/// <list type="bullet">
///   <item>
///     <b>Energy</b> — the Persona's online integrity. Depleted by most ICE attacks.
///     If energy reaches zero the Persona is dumped from the system.
///   </item>
///   <item>
///     <b>Physical health</b> — the Decker's body. Only damaged by BlackIce.
///     Damage is routed directly to <see cref="DeckerRef"/>.
///     When fighting BlackIce the portrait changes to the Decker's real face.
///   </item>
/// </list>
///
/// Key rules enforced here:
/// <list type="bullet">
///   <item>Deception cannot be run once <see cref="CombatState"/> is Active.</item>
///   <item>A program must be <see cref="Program.IsReadyToRun"/> before it can be run.</item>
///   <item>At most one program may be run per call (no batching).</item>
///   <item>Jack-out against live BlackIce triggers a block + physical damage roll.</item>
/// </list>
/// </summary>
public class Persona
{
    // ── Constants ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Probability that live BlackIce blocks a jack-out attempt,
    /// per point of the ICE's effective rating.
    /// Total block chance = effectiveRating × BlockChancePerRatingPoint.
    /// </summary>
    public const float BlockChancePerRatingPoint = 0.07f;

    /// <summary>
    /// Base success roll difficulty constant used in
    /// <see cref="ComputeSuccessChance"/>.
    /// </summary>
    public const float SuccessDifficultyConstant = 10f;

    /// <summary>
    /// The crossover point between a combat miss and a program failure for the
    /// Attack program. If the raw (unclamped) success chance is at or below this
    /// value, the program is too outmatched to execute reliably and any failure
    /// is treated as a program failure (triggering Tar) rather than a miss.
    ///
    /// Tuning guide:
    ///   0.05 — only crashes when completely hopeless (at the success floor)
    ///   0.10 — crashes against ICE roughly 2x the program's effective power (current)
    ///   0.15 — more forgiving loadouts required to avoid crashes
    ///   0.20 — aggressive: even moderately mismatched programs crash
    /// </summary>
    public const float SuccessFloor = 0.1f;

    // ── Identity ──────────────────────────────────────────────────────────────

    public string Id { get; } = Guid.NewGuid().ToString("N");

    // ── Health ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Current Persona energy. When this reaches zero the Persona is dumped.
    /// Restored by the Medic program. Not the same as physical health.
    /// </summary>
    public float Energy    { get; private set; }

    public float EnergyMax { get; private set; }

    /// <summary>
    /// True while the Persona is fighting BlackIce. During this time:
    /// <list type="bullet">
    ///   <item>The UI displays the Decker's real portrait instead of the Persona.</item>
    ///   <item>Incoming damage goes to the Decker's physical health, not Persona energy.</item>
    /// </list>
    /// </summary>
    public bool IsFightingBlackIce { get; private set; }

    // ── Navigation ────────────────────────────────────────────────────────────

    /// <summary>The ID of the Node the Persona currently occupies.</summary>
    public string CurrentNodeId { get; private set; }

    // ── Combat ────────────────────────────────────────────────────────────────

    public CombatState CombatState { get; private set; } = CombatState.None;

    // ── Dependencies ─────────────────────────────────────────────────────────

    /// <summary>
    /// The Cyberdeck driving this Persona. Provides program slots and stat values.
    /// </summary>
    public Cyberdeck Deck { get; }

    /// <summary>
    /// Back-reference to the real-world Decker. Used to route BlackIce damage
    /// and read skill ratings.
    /// </summary>
    public Decker DeckerRef { get; }

    // ── Random ────────────────────────────────────────────────────────────────

    private readonly Random _rng;

    // ── Construction ─────────────────────────────────────────────────────────

    /// <param name="decker">The Decker controlling this Persona.</param>
    /// <param name="deck">The Cyberdeck being used this session.</param>
    /// <param name="startNodeId">The Node ID where the Persona enters the system.</param>
    /// <param name="rng">Optional seeded random for deterministic tests.</param>
    public Persona(Decker decker, Cyberdeck deck, string startNodeId, Random? rng = null)
    {
        ArgumentNullException.ThrowIfNull(decker);
        ArgumentNullException.ThrowIfNull(deck);
        ArgumentException.ThrowIfNullOrWhiteSpace(startNodeId);

        DeckerRef     = decker;
        Deck          = deck;
        CurrentNodeId = startNodeId;
        _rng          = rng ?? Random.Shared;

        // Persona energy scales with deck MPCP
        EnergyMax = deck.Stats.Mpcp * 15f;
        Energy    = EnergyMax;
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    /// <summary>
    /// Moves the Persona to <paramref name="nodeId"/>.
    /// Does not validate adjacency — that is the session's responsibility.
    /// Resets combat state for the new node encounter.
    /// </summary>
    internal void MoveTo(string nodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        CurrentNodeId      = nodeId;
        CombatState        = CombatState.None;
        IsFightingBlackIce = false;
    }

    // ── Program execution ─────────────────────────────────────────────────────

    /// <summary>
    /// Attempts to run the program in <paramref name="slotIndex"/> against
    /// <paramref name="targetIce"/> (or null for non-combat programs).
    ///
    /// Returns a failed <see cref="ProgramRunResult"/> for expected in-game
    /// refusals (slot empty, program not ready, Deception attempted in combat).
    /// Throws <see cref="ArgumentOutOfRangeException"/> for invalid slot indices.
    /// </summary>
    public ProgramRunResult RunProgram(int slotIndex, Ice? targetIce = null)
    {
        if (slotIndex is < 0 or >= Cyberdeck.MaxLoadedPrograms)
            throw new ArgumentOutOfRangeException(nameof(slotIndex),
                $"Slot index must be 0–{Cyberdeck.MaxLoadedPrograms - 1}.");

        Program? program = Deck.GetSlot(slotIndex);

        if (program is null)
            return FailedRun(slotIndex, null, targetIce, "Slot is empty.");

        if (!program.IsReadyToRun)
            return FailedRun(slotIndex, program, targetIce,
                program.LoadProgress < 1f
                    ? $"'{program.Spec.Name}' is still loading ({program.LoadProgress:P0})."
                    : $"'{program.Spec.Name}' is still refreshing ({program.RefreshProgress:P0}).");

        // Deception is blocked once combat is active
        if (program.Spec.Name == ProgramName.Deception && CombatState == CombatState.Active)
            return FailedRun(slotIndex, program, targetIce,
                "Deception cannot be used once combat has started.");

        // Non-combat programs (no ICE target)
        if (targetIce is null)
            return RunNonCombatProgram(slotIndex, program);

        // Combat programs
        return RunCombatProgram(slotIndex, program, targetIce);
    }

    // ── Damage and healing ────────────────────────────────────────────────────

    /// <summary>
    /// Applies <paramref name="amount"/> damage to the correct health pool.
    /// BlackIce damage bypasses Persona energy and hits the Decker's body.
    /// All other ICE damage reduces <see cref="Energy"/>.
    /// </summary>
    /// <returns>Events produced (PersonaDumped or PhysicalDamage dealt).</returns>
    public IReadOnlyList<IceEvent> TakeDamage(float amount, Ice source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (amount <= 0f) return Array.Empty<IceEvent>();

        var events = new List<IceEvent>();

        if (source.Spec.Type == IceType.BlackIce)
        {
            // Route directly to physical health
            IsFightingBlackIce = true;
            DeckerRef.ReceivePhysicalDamage(amount);
            events.Add(new PhysicalDamageDealtEvent(amount));
        }
        else
        {
            Energy = Math.Max(0f, Energy - amount);

            if (Energy <= 0f)
            {
                var dumpCause = source.Spec.Type == IceType.BlackIce
                    ? DumpCause.TraceAndBurn
                    : DumpCause.EnergyDepleted;

                events.Add(new PersonaDumpedEvent(dumpCause));
            }
        }

        return events.AsReadOnly();
    }

    /// <summary>
    /// Restores <paramref name="amount"/> Persona energy up to <see cref="EnergyMax"/>.
    /// Called by the Medic program effect handler.
    /// </summary>
    public void Heal(float amount)
    {
        if (amount < 0f)
            throw new ArgumentOutOfRangeException(nameof(amount), "Heal amount must be non-negative.");

        Energy = Math.Min(EnergyMax, Energy + amount);
    }

    // ── Evasion ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Rolls an evasion check against an incoming ICE attack.
    /// Returns true if the Persona successfully dodges (no damage applied).
    /// </summary>
    public bool EvadeAttack(Ice ice)
    {
        ArgumentNullException.ThrowIfNull(ice);

        float evadeChance = ComputeEvadeChance(Deck.Stats.Evasion, ice.EffectiveRating);
        return _rng.NextDouble() <= evadeChance;
    }

    // ── Jack-out ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Attempts to disconnect from the Matrix.
    ///
    /// If the current node contains live BlackIce the ICE may block the
    /// attempt and deal physical damage to the Decker before the Persona
    /// escapes. Jack-out always eventually succeeds — BlackIce can delay
    /// and damage, but not permanently prevent disconnection.
    /// </summary>
    /// <param name="liveBlackIce">
    /// Any BlackIce currently alive at the Persona's node, or null if none.
    /// </param>
    public JackOutResult JackOut(Ice? liveBlackIce = null)
    {
        if (liveBlackIce is null || !liveBlackIce.IsAlive)
        {
            ResetCombatState();
            return JackOutResult.Clean();
        }

        // BlackIce block chance scales with effective rating
        float blockChance = Math.Clamp(
            liveBlackIce.EffectiveRating * BlockChancePerRatingPoint, 0f, 0.85f);

        bool blocked = _rng.NextDouble() <= blockChance;

        if (blocked)
        {
            // BlackIce deals physical damage even as the Persona escapes
            float damage = liveBlackIce.EffectiveRating * 2.5f;
            DeckerRef.ReceivePhysicalDamage(damage);
            ResetCombatState();
            return JackOutResult.BlockedWithDamage(damage);
        }

        ResetCombatState();
        return JackOutResult.Clean();
    }

    // ── Combat state ─────────────────────────────────────────────────────────

    /// <summary>Transitions the Persona into active combat. Called by the session on ICE trigger.</summary>
    internal void EnterCombat()
    {
        CombatState = CombatState.Active;
    }

    /// <summary>Resets combat state when leaving a node or jacking out.</summary>
    internal void ResetCombatState()
    {
        CombatState        = CombatState.None;
        IsFightingBlackIce = false;
    }

    // ── Success chance computation ────────────────────────────────────────────

    /// <summary>
    /// Computes the probability (0.05–0.95) that a given program will succeed
    /// against the given ICE.
    ///
    /// Formula:
    /// <code>
    /// base = (programLevel × computerSkill) / (iceEffectiveRating × DifficultyConstant)
    /// </code>
    /// Deception and Sleaze additionally multiply by (masking / 5) for a stealth bonus.
    /// </summary>
    public float ComputeSuccessChance(Program program, Ice ice)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(ice);

        return Math.Clamp(ComputeRawSuccessChance(program, ice), 0.05f, 0.95f);
    }

    /// <summary>
    /// Returns the unclamped success chance. Used to detect when a program is
    /// so outmatched that it is at or below the floor — in which case any failure
    /// is a program failure rather than a miss.
    /// </summary>
    private float ComputeRawSuccessChance(Program program, Ice ice)
    {
        float numerator   = program.Spec.Level * Math.Max(1, DeckerRef.ComputerSkill);
        float denominator = ice.EffectiveRating * SuccessDifficultyConstant;
        float baseChance  = denominator > 0f ? numerator / denominator : 0f;

        // Stealth programs benefit from Masking
        if (program.Spec.Name is ProgramName.Deception or ProgramName.Sleaze)
        {
            float maskingBonus = Deck.Stats.Masking / 5f;
            baseChance *= (1f + maskingBonus);
        }

        // Attack and Analyze scale with Response for speed but not accuracy —
        // keep accuracy to the base formula here; Response affects tick cadence
        return baseChance;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private ProgramRunResult RunCombatProgram(int slotIndex, Program program, Ice targetIce)
    {
        var allEvents          = new List<IceEvent>();
        bool combatTransitioned = false;
        bool iceDestroyed       = false;

        float successChance = ComputeSuccessChance(program, targetIce);
        bool  succeeded     = _rng.NextDouble() <= successChance;

        // An Attack failure is a miss only when the program was genuinely capable
        // of executing — i.e. the raw (unclamped) chance was above the floor.
        // If the program is so outmatched that it was clamped up to the floor,
        // any failure is a program failure and should trigger Tar.
        bool isMiss = !succeeded
                      && program.Spec.Name == ProgramName.Attack
                      && ComputeRawSuccessChance(program, targetIce) > SuccessFloor;

        if (succeeded)
        {
            iceDestroyed = targetIce.OnProgramRunSucceeded(program.Spec.Name);

            // NOTE: Attack damage is NOT applied here — it is computed with 2d6
            // variance in ProgramEffectHandler.HandleAttack and applied by
            // MatrixSession.ApplyProgramEffect. Applying it here too would
            // double-hit the ICE and produce a deterministic log value.

            // Sleaze success: persona slipped past undetected — no combat, ICE untouched.
            // Only engage combat if the ICE survived a non-Sleaze hit.
            if (!iceDestroyed && CombatState == CombatState.None
                && program.Spec.Name != ProgramName.Sleaze)
            {
                targetIce.EngageCombat();
                CombatState        = CombatState.Active;
                combatTransitioned = true;

                if (targetIce.Spec.Type == IceType.BlackIce)
                    IsFightingBlackIce = true;
            }
        }
        else
        {
            // Primary ICE always reacts to any failure — whether a miss or a program
            // failure. The secondary Tar trigger is gated separately in MatrixSession
            // using IsMiss, so we do not need to suppress it here.
            var iceEvents = targetIce.OnProgramRunFailed(program);
            allEvents.AddRange(iceEvents);

            // Transition to combat if ICE engaged
            if (allEvents.OfType<CombatEngagedEvent>().Any() && CombatState == CombatState.None)
            {
                CombatState        = CombatState.Active;
                combatTransitioned = true;

                if (targetIce.Spec.Type == IceType.BlackIce)
                    IsFightingBlackIce = true;
            }
        }

        // Post-run: handle program reload / refresh
        ApplyPostRunProgramState(program);

        return new ProgramRunResult(
            succeeded:          succeeded,
            program:            program,
            slotIndex:          slotIndex,
            targetIce:          targetIce,
            events:             allEvents,
            combatStateChanged: combatTransitioned,
            iceDestroyed:       iceDestroyed,
            isMiss:             !succeeded && isMiss);
    }

    private ProgramRunResult RunNonCombatProgram(int slotIndex, Program program)
    {
        // Non-combat programs (Medic, Analyze, Shield, etc.) always
        // succeed mechanically — the effect handler decides the degree.
        // They do not interact with ICE directly, so no ICE events.
        ApplyPostRunProgramState(program);

        return new ProgramRunResult(
            succeeded:          true,
            program:            program,
            slotIndex:          slotIndex,
            targetIce:          null,
            events:             [],
            combatStateChanged: false,
            iceDestroyed:       false);
    }

    private static ProgramRunResult FailedRun(
        int      slotIndex,
        Program? program,
        Ice?     targetIce,
        string   reason)
    {
        // We need a dummy Program reference for the result — use a sentinel if null.
        // This path only occurs on programmer error (empty slot, wrong state).
        if (program is null)
            throw new InvalidOperationException($"RunProgram called on empty slot: {reason}");

        return new ProgramRunResult(
            succeeded:          false,
            program:            program,
            slotIndex:          slotIndex,
            targetIce:          targetIce,
            events:             [],
            combatStateChanged: false,
            iceDestroyed:       false,
            isPreflightFailure: true);
    }

    private static void ApplyPostRunProgramState(Program program)
    {
        if (program.Spec.ReloadsAfterUse)
            program.BeginReload();
        else
            program.Refresh();
    }

    private static float ComputeEvadeChance(int evasionRating, int iceEffectiveRating)
    {
        float raw = (float)evasionRating / (evasionRating + iceEffectiveRating + 1);
        return Math.Clamp(raw, 0.05f, 0.90f);
    }

    // ── Display ───────────────────────────────────────────────────────────────

    public override string ToString()
    {
        string blackIce = IsFightingBlackIce ? " [BlackIce!]" : "";
        return $"[Persona] {DeckerRef.Name}{blackIce} " +
               $"Energy:{Energy:F0}/{EnergyMax:F0} " +
               $"Node:{CurrentNodeId} Combat:{CombatState}";
    }
}
