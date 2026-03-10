using Shadowrun.Matrix.Core;
using Shadowrun.Matrix.Enums;
using Shadowrun.Matrix.ValueObjects;

namespace Shadowrun.Matrix.Models;

/// <summary>
/// Manages the complete runtime state of a single jack-in session.
///
/// A session is created by <see cref="Decker.JackIn"/> and lives until
/// <see cref="EndSession"/> is called. It wires together every other class:
/// driving the tick loop, routing program runs through
/// <see cref="ProgramEffectHandler"/>, applying <see cref="ProgramEffect"/>
/// mutations, tracking ICE combat timers, and enforcing all business rules
/// from the spec.
///
/// All public methods return typed result objects — they never throw for
/// expected in-game failure states.
/// </summary>
public class MatrixSession
{
    // ── Constants ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Seconds between periodic ICE attack rolls during active combat.
    /// Scales inversely with effective ICE rating (higher rating = faster attacks).
    /// Base interval at rating 1; minimum 0.5 s at rating 9.
    /// </summary>
    public const float BaseIceAttackInterval = 4.0f;

    /// <summary>
    /// Smoke effect success penalty applied to all program rolls while active.
    /// </summary>
    public const float SmokePenalty = 0.4f;

    /// <summary>
    /// Mirrors effect hit-chance reduction applied to ICE attack rolls while active.
    /// </summary>
    public const float MirrorsAccuracyPenalty = 0.35f;

    /// <summary>
    /// Minimum Computer skill to reliably find data files in DS nodes.
    /// Below this, find rate degrades sharply.
    /// </summary>
    public const int MinComputerForDataRuns = 5;

    // ── Core references ───────────────────────────────────────────────────────

    public Decker       Decker   { get; }
    public MatrixSystem System   { get; }
    public Persona      Persona  { get; }
    public MatrixRun?   ActiveRun { get; private set; }

    // ── Session state ─────────────────────────────────────────────────────────

    public bool          IsActive    { get; private set; } = true;
    public DateTimeOffset StartTime  { get; } = DateTimeOffset.UtcNow;

    /// <summary>Set by <see cref="EndSession"/> — reason the session ended.</summary>
    public string        EndReason   { get; private set; } = "jack_out";

    /// <summary>Append-only event log — full record of the session.</summary>
    public IReadOnlyList<SessionEvent> SessionLog => _log.AsReadOnly();
    private readonly List<SessionEvent> _log = [];

    // ── ICE combat timers ─────────────────────────────────────────────────────
    // Keyed by Ice.Id — tracks seconds until the next attack tick for each ICE.

    private readonly Dictionary<string, float> _iceAttackTimers = new();

    // ── Active status effects ─────────────────────────────────────────────────

    private float _smokeDurationRemaining;
    private float _mirrorsDurationRemaining;

    // ── Shield state ──────────────────────────────────────────────────────────
    // Remaining shield absorption; reset to 0 when shield breaks.

    private float _shieldRemaining;

    // ── Analyze scan progress ─────────────────────────────────────────────────
    // Tracks partial scan state per node (nodeId → bool fully scanned).

    private readonly Dictionary<string, bool> _nodeScanned = new();

    // ── Random ────────────────────────────────────────────────────────────────

    private readonly Random _rng;

    // ── DS node state ─────────────────────────────────────────────────────────
    // Nodes whose data has already been downloaded; further searches yield nothing.
    private readonly HashSet<string> _extractedDataNodes = new();

    // ── Fog-of-war: nodes the persona has physically visited ──────────────────
    private readonly HashSet<string> _visitedNodes = new();

    /// <summary>Nodes the Persona has bypassed via Sleaze. ICE persists but travel is allowed.</summary>
    private readonly HashSet<string> _bypassedNodes = new();

    /// <summary>Returns true if the given node has been bypassed via Sleaze (ICE still lives).</summary>
    public bool IsNodeBypassed(string nodeId) => _bypassedNodes.Contains(nodeId);

    /// <summary>
    /// All node IDs the persona has visited during this session (including the
    /// starting node). Used by the UI for fog-of-war map rendering.
    /// </summary>
    public IReadOnlySet<string> VisitedNodes => _visitedNodes;

    // ── ICE identification tracking ───────────────────────────────────────────
    // ICE is revealed (type name shown) when:
    //   • the persona lands a successful hit on it,
    //   • it fires an attack at the persona (hit, miss, or evaded),
    //   • or a successful Analyze program scan completes on the node.
    private readonly HashSet<string> _revealedIce = new();

    /// <summary>
    /// Returns true when the persona has identified this ICE instance
    /// (i.e. the type and rating may be displayed in the UI).
    /// Always returns true in developer mode.
    /// </summary>
    public bool IsIceRevealed(Ice ice) => _revealedIce.Contains(ice.Id);

    /// <summary>Marks a specific ICE instance as identified.</summary>
    private void RevealIce(Ice ice) => _revealedIce.Add(ice.Id);

    // ── Construction ─────────────────────────────────────────────────────────

    /// <param name="decker">The Decker running the session.</param>
    /// <param name="system">The Matrix system being penetrated.</param>
    /// <param name="persona">The active Persona (created by Decker.JackIn).</param>
    /// <param name="activeRun">Optional contracted run objective.</param>
    /// <param name="rng">Optional seeded random for deterministic tests.</param>
    public MatrixSession(
        Decker       decker,
        MatrixSystem system,
        Persona      persona,
        MatrixRun?   activeRun = null,
        Random?      rng       = null)
    {
        ArgumentNullException.ThrowIfNull(decker);
        ArgumentNullException.ThrowIfNull(system);
        ArgumentNullException.ThrowIfNull(persona);

        Decker    = decker;
        System    = system;
        Persona   = persona;
        ActiveRun = activeRun;
        _rng      = rng ?? Random.Shared;

        // The starting node counts as visited from the moment you jack in
        _visitedNodes.Add(persona.CurrentNodeId);

        // Initialise attack timers for any ICE present at the entry node
        Node entryNode = system.GetNode(persona.CurrentNodeId);
        foreach (Ice ice in entryNode.GetLiveIce())
            _iceAttackTimers[ice.Id] = ComputeAttackInterval(ice.EffectiveRating);

        Log(SessionEventType.SessionStarted,
            $"Session started — {decker.Name} jacked into '{system.Name}' " +
            $"at node {persona.CurrentNodeId}.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TRAVEL
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Moves the Persona to an adjacent node.
    ///
    /// Validates adjacency. On arrival, resets combat state and surfaces any
    /// ICE at the destination. Travel is blocked if the Persona is currently
    /// in active combat at the current node (must resolve ICE first).
    /// </summary>
    public TravelResult TravelToNode(string nodeId)
    {
        GuardSessionActive();
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);

        // Block travel mid-combat — UNLESS this node was bypassed via Sleaze
        if (Persona.CombatState == CombatState.Active && !_bypassedNodes.Contains(Persona.CurrentNodeId))
            return TravelResult.Fail(nodeId,
                "Cannot travel while in active combat. Defeat or bypass the ICE first.");

        Node currentNode = System.GetNode(Persona.CurrentNodeId);

        // Validate adjacency (teleport via CPU.GoToNode bypasses this check)
        if (!currentNode.IsAdjacentTo(nodeId))
            return TravelResult.Fail(nodeId,
                $"Node '{nodeId}' is not adjacent to '{Persona.CurrentNodeId}'.");

        Node destination = System.GetNode(nodeId);

        // Clear per-node status effects
        _smokeDurationRemaining   = 0f;
        _mirrorsDurationRemaining = 0f;
        _shieldRemaining          = 0f;

        // Leaving a bypassed node — clear the bypass so a revisit requires Sleaze again
        _bypassedNodes.Remove(Persona.CurrentNodeId);

        // Move the persona
        Persona.MoveTo(nodeId);

        // Record as visited for fog-of-war
        _visitedNodes.Add(nodeId);

        // Revisit: if the node was already identified, mark it (game rule §10)
        if (destination.IsIdentified)
            _nodeScanned[nodeId] = true;

        Log(SessionEventType.NodeEntered,
            $"Persona arrived at {destination.Type} node '{destination.Label}' " +
            $"[{destination.Color} SR:{destination.SecurityRating}].");

        // Surface live ICE at the destination
        var liveIce = destination.GetLiveIce().ToList();

        if (liveIce.Count > 0)
        {
            // Initialise attack timers for new ICE
            foreach (Ice ice in liveIce)
                _iceAttackTimers[ice.Id] = ComputeAttackInterval(ice.EffectiveRating);
        }

        return TravelResult.Ok(nodeId, liveIce);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // NODE ACTIONS
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Executes a node action at the Persona's current node.
    ///
    /// Actions are blocked while live ICE is in active combat, except for
    /// jack-out (handled separately via <see cref="JackOut"/>).
    /// </summary>
    /// <param name="action">The action to perform.</param>
    /// <param name="targetNodeId">Required for <see cref="NodeAction.GoToNode"/>.</param>
    /// <param name="fromInsideBuilding">
    /// For <see cref="NodeAction.CrashSystem"/> — whether the Decker is
    /// physically inside the associated building.
    /// </param>
    public ActionResult InitiateNodeAction(
        NodeAction action,
        string?    targetNodeId        = null,
        bool       fromInsideBuilding  = false)
    {
        GuardSessionActive();

        Node currentNode = System.GetNode(Persona.CurrentNodeId);

        // Block in-node actions during active combat (rule: must resolve ICE first)
        if (Persona.CombatState == CombatState.Active &&
            action != NodeAction.LeaveNode)
        {
            return ActionResult.Fail(currentNode.Id, action,
                "Cannot perform node actions while in active combat.");
        }

        ActionResult result = currentNode.ExecuteAction(action, targetNodeId, fromInsideBuilding);

        if (!result.Success) return result;

        // Apply system-wide side-effects
        foreach (SystemEvent sysEvent in result.SideEffects)
            ApplySystemEvent(sysEvent, currentNode);

        // Special handling per action
        switch (action)
        {
            case NodeAction.CrashSystem:
                Log(SessionEventType.SystemCrashed,
                    $"CPU crashed by {Decker.Name}.");
                break;

            case NodeAction.CancelAlert:
                System.CancelAlert();
                Log(SessionEventType.AlertCancelled,
                    "System alert reset to Normal via CPU.");
                break;

            case NodeAction.TurnOffNode:
                Log(SessionEventType.SlaveModeDisabled,
                    $"Slave module '{currentNode.Label}' taken offline.");
                break;
        }

        // Check run objective after node actions
        // For Erase, pass the contracted filename so DeleteData objectives resolve correctly.
        if (ActiveRun is not null && !ActiveRun.IsComplete)
        {
            string? filename = action == NodeAction.Erase ? ActiveRun.ContractedFilename : null;
            CheckRunObjective(action, currentNode.Id, filename);
        }

        return result;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PROGRAM EXECUTION
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Runs the program in <paramref name="slotIndex"/> against the currently
    /// active ICE (or null for non-combat programs).
    ///
    /// Wires together: success roll → <see cref="ProgramEffectHandler"/> →
    /// state mutation → event logging → run objective check.
    /// </summary>
    public ProgramRunResult RunProgram(int slotIndex)
    {
        GuardSessionActive();

        Node currentNode = System.GetNode(Persona.CurrentNodeId);
        Ice? activeIce   = currentNode.GetActiveIce();

        // Peek at the program before running so we can special-case Analyze.
        Program? peeked = Persona.Deck.GetSlot(slotIndex);

        // ── Analyze: always runs as a non-combat program, with its own roll ──
        if (peeked?.Spec.Name == ProgramName.Analyze)
            return RunAnalyzeProgram(slotIndex, peeked, currentNode, activeIce);

        // Perform the run (Persona validates slot, ready state, combat rules).
        // Persona-only programs (Medic, Shield) must never be routed through the
        // combat success roll — pass null so they take the non-combat path regardless
        // of whether ICE is present.
        Ice? combatTarget = (peeked?.Spec.TargetsPersonaOnly == true) ? null : activeIce;
        ProgramRunResult runResult = Persona.RunProgram(slotIndex, combatTarget);

        if (runResult.IsPreflightFailure)
        {
            // Pre-flight validation failed (slot empty, program not ready, wrong state).
            // No dice were rolled — skip all side-effects including Tar secondary trigger.
            Log(SessionEventType.ProgramFailed, runResult.ToString());
            return runResult;
        }

        Program program = runResult.Program;

        // Compute actual effect via the handler
        bool succeeded = runResult.Succeeded;

        // Apply Smoke penalty to success if active (combat programs only)
        if (_smokeDurationRemaining > 0f && combatTarget is not null)
        {
            float penalisedChance = Persona.ComputeSuccessChance(program, combatTarget)
                                    * (1f - SmokePenalty);
            succeeded = _rng.NextDouble() <= penalisedChance;
        }

        ProgramEffect effect = ProgramEffectHandler.Handle(
            program, combatTarget, Persona, System, succeeded, _rng);

        // Apply the effect's mutations
        ApplyProgramEffect(effect, combatTarget, currentNode);

        // Propagate ICE events from the run result
        foreach (IceEvent evt in runResult.Events)
            ApplyIceEvent(evt, program);

        // Log
        string logMsg = $"{program.Spec.Name} L{program.Spec.Level}: {effect.Narrative}";
        if (succeeded)
        {
            Log(SessionEventType.ProgramRun, logMsg);
        }
        else
        {
            // All failures log as ProgramFailed — IsMiss only gates the Tar trigger,
            // not the log format. "Missed" is misleading when tar can also fire.
            Log(SessionEventType.ProgramFailed, logMsg);
        }

        // Combat state transition
        if (runResult.CombatStateChanged)
            Log(SessionEventType.CombatEngaged,
                $"Combat engaged with {activeIce?.Spec.Type} at '{currentNode.Label}'.");

        // ── Hidden Tar ICE trigger on program failure against ICE ─────────────
        // Fires on any failed ICE-targeting program EXCEPT Attack misses — a miss
        // means the roll failed but the attack was attempted; that is not a
        // program failure and should not trigger Tar.
        if (!succeeded && !runResult.IsMiss && combatTarget is not null)
        {
            Ice? tarSecondary = currentNode.GetSecondaryIce();
            if (tarSecondary is not null && tarSecondary.IsAlive && tarSecondary.Spec.IsTarType)
            {
                var tarEvents = tarSecondary.OnProgramRunFailed(program);
                foreach (IceEvent tarEvt in tarEvents)
                    ApplyIceEvent(tarEvt, program);
            }
        }

        // ICE destroyed
        if (runResult.IceDestroyed || (activeIce is not null && !activeIce.IsAlive))
        {
            Log(SessionEventType.IceDefeated,
                $"{activeIce?.Spec.Type} defeated at '{currentNode.Label}'.");

            // If the defeated ICE was the primary (non-hidden), also destroy any
            // lurking Tar Paper / Tar Pit — it was hidden behind the primary and
            // cannot survive without it.
            if (activeIce is not null && !activeIce.Spec.IsHidden)
            {
                Ice? secondary = currentNode.GetSecondaryIce();
                if (secondary is not null && secondary.IsAlive)
                {
                    secondary.ForceDestroy();
                    Log(SessionEventType.IceDefeated,
                        $"Hidden {secondary.Spec.Type} eliminated with {activeIce.Spec.Type}.");
                }
            }

            if (currentNode.AllIceDefeated())
            {
                currentNode.MarkConquered();
                Persona.ResetCombatState();
                // ICE fully defeated — bypass no longer needed (node is now fully open)
                _bypassedNodes.Remove(currentNode.Id);
                Log(SessionEventType.NodeConquered,
                    $"Node '{currentNode.Label}' conquered.");
            }
        }

        return runResult;
    }

    /// <summary>
    /// Special execution path for the Analyze program.
    ///
    /// Analyze is a non-combat scan — it never targets ICE directly and never
    /// triggers combat on a success. On a failure, however, the ICE detects the
    /// intrusion attempt and combat is engaged immediately.
    ///
    /// Success formula (combined Sensor + ComputerSkill):
    ///   chance = (level × (computerSkill + sensor)) / (iceRating × 10)   [with ICE]
    ///   chance = (level × (computerSkill + sensor)) / 20                  [no ICE]
    /// Clamped 0.05–0.95. Higher Sensor directly improves the roll.
    /// Full-scan probability (within the handler) also scales with Sensor.
    /// </summary>
    private ProgramRunResult RunAnalyzeProgram(
        int slotIndex, Program program, Node currentNode, Ice? activeIce)
    {
        if (!program.IsReadyToRun)
        {
            string notReady = program.LoadProgress < 1f
                ? $"'{program.Spec.Name}' is still loading ({program.LoadProgress:P0})."
                : $"'{program.Spec.Name}' is still refreshing ({program.RefreshProgress:P0}).";
            Log(SessionEventType.ProgramFailed, notReady);
            return new ProgramRunResult(false, program, slotIndex, null, [], false, false,
                isPreflightFailure: true);
        }

        int sensor       = Persona.Deck.Stats.Sensor;
        int computerSkill = Persona.DeckerRef.ComputerSkill;
        int level        = program.Spec.Level;

        // Success chance blends ComputerSkill (run success) with Sensor (scan quality)
        float numerator   = level * (Math.Max(1, computerSkill) + Math.Max(1, sensor));
        float denominator = activeIce is not null
            ? activeIce.EffectiveRating * 10f
            : 20f;
        float successChance = Math.Clamp(numerator / denominator, 0.05f, 0.95f);
        bool  succeeded     = _rng.NextDouble() <= successChance;

        // Always route through Persona as non-combat (targetIce = null) so the
        // result object is built correctly and reload/refresh is applied.
        // We override the succeeded flag from our own roll above.
        ProgramRunResult runResult = Persona.RunProgram(slotIndex, null);

        bool combatEngaged = false;

        if (succeeded)
        {
            // Run the effect handler so scan-completeness and ICE reveal fire.
            ProgramEffect effect = ProgramEffectHandler.Handle(
                program, null, Persona, System, true, _rng);
            ApplyProgramEffect(effect, activeIce, currentNode);
            Log(SessionEventType.ProgramRun,
                $"Analyze L{level}: {effect.Narrative}");
        }
        else
        {
            Log(SessionEventType.ProgramFailed,
                $"Analyze L{level}: Scan incomplete — node defences detected the probe.");

            // ICE detects the scan → engage combat immediately
            if (activeIce is not null && activeIce.IsAlive
                && Persona.CombatState == CombatState.None)
            {
                activeIce.EngageCombat();
                Persona.EnterCombat();
                combatEngaged = true;
                Log(SessionEventType.CombatEngaged,
                    $"Analyze detected by {activeIce.Spec.Type} at '{currentNode.Label}' — combat engaged.");
            }
        }

        return new ProgramRunResult(
            succeeded:          succeeded,
            program:            program,
            slotIndex:          slotIndex,
            targetIce:          null,
            events:             [],
            combatStateChanged: combatEngaged,
            iceDestroyed:       false);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DATA TRANSFER
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Performs a data transfer at the current DS node.
    ///
    /// For contracted runs (download/upload/erase), fulfils the mission objective.
    /// For open exploration, searches for random sellable data files.
    ///
    /// Find rate scales with the Decker's Computer skill (rule §13).
    /// </summary>
    public DataTransferResult PerformDataTransfer()
    {
        GuardSessionActive();

        Node currentNode = System.GetNode(Persona.CurrentNodeId);

        if (currentNode.Type != NodeType.DS)
            return DataTransferResult.Fail(currentNode.Id,
                $"Transfer Data is only valid on DS nodes (current: {currentNode.Type}).");

        if (Persona.CombatState == CombatState.Active)
            return DataTransferResult.Fail(currentNode.Id,
                "Cannot transfer data while in active combat.");

        // Check if this is a mission-critical transfer
        if (ActiveRun is not null && !ActiveRun.IsComplete &&
            currentNode.Id == ActiveRun.TargetNodeId)
        {
            return PerformObjectiveTransfer(currentNode);
        }

        // Open exploration: search for random files
        return SearchForData(currentNode);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // JACK-OUT
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Attempts to disconnect the Persona from the Matrix.
    ///
    /// Always succeeds eventually. Live BlackIce at the current node may block
    /// the attempt and deal physical damage to the Decker before escape.
    /// </summary>
    public JackOutResult JackOut()
    {
        GuardSessionActive();

        Log(SessionEventType.JackOutAttempted,
            $"Jack-out attempted from node '{Persona.CurrentNodeId}'.");

        // Find live BlackIce at current node
        Node currentNode = System.GetNode(Persona.CurrentNodeId);
        Ice? blackIce    = currentNode.GetLiveIce()
                                      .FirstOrDefault(i => i.Spec.Type == IceType.BlackIce);

        JackOutResult result = Persona.JackOut(blackIce);

        Log(SessionEventType.JackOutSucceeded, result.ToString());

        if (result.BlockedByBlackIce)
        {
            Log(SessionEventType.PersonaDamaged,
                $"BlackIce blocked jack-out — {result.PhysicalDamageDealt:F1} physical damage taken.");
        }

        EndSession("jack_out");
        return result;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // RUN COMPLETION
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Validates and finalises the contracted run.
    /// Awards nuyen and karma to the Decker on success.
    /// </summary>
    public RunCompletionResult CompleteRun()
    {
        GuardSessionActive();

        if (ActiveRun is null)
            return RunCompletionResult.Fail("No active contracted run to complete.");

        if (ActiveRun.IsComplete)
            return RunCompletionResult.Fail("Run has already been completed.");

        if (!ActiveRun.ObjectiveAchieved)
            return RunCompletionResult.Fail(
                "Run objective has not yet been met. Complete the required action first.");

        int nuyen = ActiveRun.ComputePay(Decker.NegotiationSkill);
        int karma = ActiveRun.KarmaReward;

        Decker.AddNuyen(nuyen);
        ActiveRun.MarkComplete(true);

        Log(SessionEventType.RunObjectiveMet,
            $"Run complete — {nuyen}¥ earned, +{karma} Karma.");

        return RunCompletionResult.Ok(nuyen, karma);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TICK LOOP
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Advances all time-driven systems by <paramref name="deltaTime"/> seconds.
    ///
    /// Called once per game frame / tick while the session is active.
    /// Order of operations:
    /// <list type="number">
    ///   <item>Advance deck program loading.</item>
    ///   <item>Advance Trace ICE probes.</item>
    ///   <item>Advance ICE periodic attacks (combat only).</item>
    ///   <item>Advance status effect durations (Slow, Mirrors, Smoke).</item>
    /// </list>
    /// </summary>
    public void TickFrame(float deltaTime)
    {
        if (!IsActive || deltaTime <= 0f) return;

        // 1. Program loading
        Decker.Deck.TickLoading(deltaTime);

        Node currentNode = System.GetNode(Persona.CurrentNodeId);

        // 2. Trace probes
        foreach (Ice ice in currentNode.GetLiveIce().Where(i => i.Spec.IsTraceType))
        {
            var probeEvents = ice.AdvanceProbe(deltaTime);
            foreach (IceEvent evt in probeEvents)
                ApplyIceEvent(evt, null);
        }

        // 3. ICE periodic attacks (only in active combat)
        if (Persona.CombatState == CombatState.Active)
        {
            foreach (Ice ice in currentNode.GetLiveIce().Where(i => i.IsInCombat))
                TickIceAttack(ice, deltaTime, currentNode);
        }

        // 4. Status effects on ICE
        foreach (Ice ice in currentNode.IceInstances)
            ice.TickStatusEffects(deltaTime);

        // 5. Global status effect durations
        TickStatusEffects(deltaTime);

        // 6. Program refresh timers — driven by Cyberdeck.TickLoading (already called above)
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SACRIFICIAL RUN (Tar Pit bait)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Deliberately runs a sacrificial program until it fails to bait a Tar Pit.
    ///
    /// Strategy: store low-level copies of cheap programs on the deck.
    /// When encountering a Tar Pit node, sacrifice the cheap program
    /// to preserve the expensive high-level programs intact.
    ///
    /// If <see cref="Smoke"/> helps cause the failure faster, run it first.
    /// </summary>
    /// <param name="sacrificialSlotIndex">
    /// The slot index of the expendable program to sacrifice.
    /// </param>
    /// <param name="useSmoke">
    /// If true and Smoke is loaded, fire it first to increase the chance of
    /// an immediate failure on the sacrificial run.
    /// </param>
    /// <returns>
    /// A failed <see cref="ProgramRunResult"/> confirming the sacrifice occurred,
    /// or an error result if no Tar Pit was present or the slot was invalid.
    /// </returns>
    public ProgramRunResult SacrificialRun(int sacrificialSlotIndex, bool useSmoke = false)
    {
        GuardSessionActive();

        Node currentNode = System.GetNode(Persona.CurrentNodeId);
        Ice? activeIce   = currentNode.GetActiveIce();

        if (activeIce is null || activeIce.Spec.Type != IceType.TarPit)
        {
            // Also check secondary ICE
            Ice? secondary = currentNode.GetSecondaryIce();
            if (secondary is null || secondary.Spec.Type != IceType.TarPit)
            {
                // Peek behind primary — Tar Pit is hidden
                return new ProgramRunResult(
                    succeeded:          false,
                    program:            Decker.Deck.GetSlot(sacrificialSlotIndex)
                                         ?? throw new InvalidOperationException("Slot is empty."),
                    slotIndex:          sacrificialSlotIndex,
                    targetIce:          null,
                    events:             [],
                    combatStateChanged: false,
                    iceDestroyed:       false);
            }
        }

        // Optionally fire Smoke first to inflate failure chance
        if (useSmoke)
        {
            Program? smokeProgram = Decker.Deck.GetReadyPrograms()
                .FirstOrDefault(p => p.Spec.Name == ProgramName.Smoke);

            if (smokeProgram is not null)
            {
                int smokeSlot = GetSlotIndex(smokeProgram);
                if (smokeSlot >= 0)
                    RunProgram(smokeSlot); // Result ignored — effect applied to session
            }
        }

        // Run the sacrificial program — expect it to fail and trigger Tar Pit
        Log(SessionEventType.TarEffectTriggered,
            $"Sacrificial run initiated on slot {sacrificialSlotIndex}.");

        return RunProgram(sacrificialSlotIndex);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SESSION ENDING
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Terminates the session for the given reason, transferring plot files to
    /// the Decker's notebook and recording the final log entry.
    ///
    /// <paramref name="reason"/> must be one of:
    /// "jack_out", "dumped", "deck_fried".
    /// </summary>
    public void EndSession(string reason)
    {
        if (!IsActive) return;

        IsActive  = false;
        EndReason = reason;

        // Transfer plot files from deck to notebook (only if still jacked in)
        if (Decker.IsJackedIn)
            Decker.JackOut(JackOutResult.Clean());

        // Mark incomplete run as failed if session ends abnormally
        if (ActiveRun is not null && !ActiveRun.IsComplete && reason != "jack_out")
            ActiveRun.MarkComplete(objectiveAchieved: false);

        Log(SessionEventType.SessionEnded,
            $"Session ended ({reason}). Duration: {DateTimeOffset.UtcNow - StartTime:mm\\:ss}.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PRIVATE — Effect application
    // ─────────────────────────────────────────────────────────────────────────

    private void ApplyProgramEffect(ProgramEffect effect, Ice? targetIce, Node currentNode)
    {
        if (!effect.Applied) return;

        // Heal Persona energy (Medic)
        if (effect.EnergyHealed > 0f)
            Persona.Heal(effect.EnergyHealed);

        // Deal damage to ICE (Attack, Deception instant-kill, Relocate, Rebound reflect)
        if (effect.DamageToIce > 0f && targetIce is not null)
        {
            // Any successful hit reveals the ICE's identity to the persona
            RevealIce(targetIce);

            bool killed;
            if (effect.DamageToIce >= float.MaxValue / 2f)
            {
                targetIce.InstantKill();
                killed = true;
            }
            else
            {
                killed = targetIce.TakeDamage(effect.DamageToIce);
            }

            // A successful hit on Trace ICE cancels the current dump probe.
            // The probe resets to origin and begins advancing again next tick.
            if (!killed && targetIce.Spec.IsTraceType)
            {
                targetIce.CancelProbe();
                Log(SessionEventType.CombatMiss,
                    $"{targetIce.Spec.Type} probe cancelled — trace reset to origin.");
            }

            if (killed)
                Log(SessionEventType.IceDefeated,
                    $"{targetIce.Spec.Type} destroyed by {effect.ProgramName}.");
        }

        // Security rating change (Degrade)
        if (effect.SecurityRatingDelta != 0)
            currentNode.DecrementSecurityRating();

        // Status effects
        if (effect.StatusEffectDuration > 0f)
        {
            switch (effect.ProgramName)
            {
                case ProgramName.Slow:
                    targetIce?.ApplySlow(effect.SlowSpeedMultiplier, effect.StatusEffectDuration);
                    break;
                case ProgramName.Smoke:
                    _smokeDurationRemaining = effect.StatusEffectDuration;
                    break;
                case ProgramName.Mirrors:
                    _mirrorsDurationRemaining = effect.StatusEffectDuration;
                    break;
            }
        }

        // Shield — set absorption from the effect's computed strength
        if (effect.ProgramName == ProgramName.Shield)
            _shieldRemaining = effect.ShieldStrength;

        // Rebound — initialise charges
        if (effect.ProgramName == ProgramName.Rebound && targetIce is not null)
        {
            var reboundProgram = Decker.Deck.GetReadyPrograms()
                .FirstOrDefault(p => p.Spec.Name == ProgramName.Rebound);
            reboundProgram?.InitialiseReboundCharges();
            targetIce.ActivateRebound();
        }

        // Sleaze bypass
        if (effect.NodeBypassed)
        {
            // Mark this node as bypassed — ICE persists but travel is now allowed.
            // Node actions remain locked until the ICE is actually defeated.
            _bypassedNodes.Add(currentNode.Id);
            Log(SessionEventType.NodeBypassed,
                $"Node '{currentNode.Label}' bypassed via Sleaze. Travel enabled; node actions still locked until ICE is defeated.");
        }

        // Full Analyze scan
        if (effect.NodeFullyScanned)
        {
            currentNode.MarkIdentified();
            _nodeScanned[currentNode.Id] = true;

            // A complete scan reveals all ICE in the node
            foreach (var ice in currentNode.IceInstances)
                RevealIce(ice);

            // Update success bars for all loaded programs
            if (targetIce is not null)
            {
                foreach (Program? slot in Decker.Deck.LoadedSlots)
                {
                    if (slot is null) continue;
                    float chance = Persona.ComputeSuccessChance(slot, targetIce);
                    slot.UpdateSuccessBar(chance);
                }
            }
        }
    }

    private void ApplyIceEvent(IceEvent evt, Program? triggeringProgram)
    {
        switch (evt)
        {
            case ActiveAlertTriggeredEvent:
                System.TriggerActiveAlert();
                Log(SessionEventType.AlertEscalated, "Active Alert triggered — ICE ratings +2.");
                break;

            case PersonaDumpedEvent dumpEvt:
                Log(SessionEventType.PersonaDumped,
                    $"Persona dumped from system. Cause: {dumpEvt.Cause}.");
                EndSession("dumped");
                break;

            case DeckMpcpDamagedEvent:
                Decker.Deck.BreakMpcp();
                Log(SessionEventType.DeckDamaged,
                    "Deck MPCP fried by Trace & Burn. Repair required before next jack-in.");
                EndSession("deck_fried");
                break;

            case PhysicalDamageDealtEvent dmgEvt:
                Log(SessionEventType.PersonaDamaged,
                    $"BlackIce dealt {dmgEvt.Amount:F1} physical damage to {Decker.Name}.");
                // Check if the Decker has died from the physical damage
                if (Decker.IsUnconscious)
                {
                    Log(SessionEventType.PersonaDumped,
                        $"{Decker.Name} flatlined — physical health depleted by BlackIce.");
                    EndSession("decker_dead");
                }
                break;

            case MemoryEraseEvent memEvt:
                // Find the program by name and unload it from memory
                Program? toUnload = Decker.Deck.Programs
                    .FirstOrDefault(p => p.Spec.Name.ToString() == memEvt.ProgramName && p.IsLoaded);

                if (toUnload is not null)
                {
                    int slot = GetSlotIndex(toUnload);
                    if (slot >= 0) Decker.Deck.UnloadProgram(slot);
                }

                System.TriggerActiveAlert();
                Log(SessionEventType.TarEffectTriggered,
                    $"Tar Paper erased '{memEvt.ProgramName}' from memory. Active Alert triggered.");
                break;

            case PermanentEraseEvent permEvt:
                // Find and permanently delete the program from the deck
                Program? toDelete = triggeringProgram
                    ?? Decker.Deck.Programs
                        .FirstOrDefault(p => p.Spec.Name.ToString() == permEvt.ProgramName);

                if (toDelete is not null)
                    Decker.Deck.DeleteProgram(toDelete);

                System.TriggerActiveAlert();
                Log(SessionEventType.TarEffectTriggered,
                    $"Tar Pit permanently deleted '{permEvt.ProgramName}'! Active Alert triggered.");
                break;

            case ProbeSpawnedEvent probeEvt:
                System.IncrementAlertChance(probeEvt.AlertChance);
                break;

            case CombatEngagedEvent:
                Persona.EnterCombat();
                break;
        }
    }

    private void ApplySystemEvent(SystemEvent sysEvent, Node sourceNode)
    {
        switch (sysEvent)
        {
            case AlertCancelledEvent:
                System.CancelAlert();
                break;

            case SystemCrashedEvent:
                // Teleport the Persona out and end session
                EndSession("jack_out");
                break;

            case TeleportedToNodeEvent teleEvt:
                // Teleport bypasses adjacency — move directly
                Persona.MoveTo(teleEvt.TargetNodeId);
                // Mark destination visible (fog-of-war: GoToNode reveals the target node)
                _visitedNodes.Add(teleEvt.TargetNodeId);
                Node dest = System.GetNode(teleEvt.TargetNodeId);
                Log(SessionEventType.NodeEntered,
                    $"Teleported to '{dest.Label}' via CPU GoToNode.");
                break;

            case SlaveModuleDisabledEvent:
                // Building-layer effect — propagated up to the game layer via log
                break;

            case DataFileDownloadedEvent:
                // Handled by PerformDataTransfer
                break;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PRIVATE — Data transfer helpers
    // ─────────────────────────────────────────────────────────────────────────

    private DataTransferResult PerformObjectiveTransfer(Node node)
    {
        if (ActiveRun is null) return DataTransferResult.Fail(node.Id, "No active run.");

        // Create a placeholder file for the contracted transfer
        var file = ActiveRun.Objective switch
        {
            MatrixRunObjective.DownloadData =>
                DataFile.CreateSellable(ActiveRun.ContractedFilename!, 50, 0),

            MatrixRunObjective.UploadData   =>
                DataFile.CreateSellable(ActiveRun.ContractedFilename!, 0,  0),

            MatrixRunObjective.DeleteData   => null, // Erase needs no file object
            MatrixRunObjective.CrashCpu     => null,
            _ => null
        };

        // Mark objective met
        NodeAction requiredAction = ActiveRun.Objective switch
        {
            MatrixRunObjective.DownloadData => NodeAction.TransferData,
            MatrixRunObjective.UploadData   => NodeAction.TransferData,
            MatrixRunObjective.DeleteData   => NodeAction.Erase,
            _ => NodeAction.TransferData
        };

        CheckRunObjective(requiredAction, node.Id, ActiveRun.ContractedFilename);

        if (file is not null)
        {
            Decker.ReceiveDataFile(file);
            _extractedDataNodes.Add(node.Id);   // node data consumed
            Log(SessionEventType.DataFileTransferred,
                $"Contracted file '{ActiveRun.ContractedFilename}' transferred.");
        }

        return DataTransferResult.Ok(node.Id, file, isObjectiveTransfer: true);
    }

    private DataTransferResult SearchForData(Node node)
    {
        // Each DS node only holds one extractable file — once taken, nothing remains.
        if (_extractedDataNodes.Contains(node.Id))
            return DataTransferResult.NotFound(node.Id);

        // Find rate: computerSkill / (10 + securityRating)
        // Below MinComputerForDataRuns, success rate degrades sharply (rule §13)
        int   comp     = Decker.ComputerSkill;
        float findRate = comp / (10f + node.SecurityRating);

        if (comp < MinComputerForDataRuns)
            findRate *= 0.3f; // Heavy penalty for low Computer skill

        // Additional detection check — low Computer skill = higher detection risk
        float detectionRisk = (10f - comp) / 20f;
        if ((float)_rng.NextDouble() <= detectionRisk)
        {
            System.TriggerPassiveAlert();
            Log(SessionEventType.AlertEscalated,
                $"Detected while searching DS — Passive Alert triggered. (Computer:{comp})");
        }

        if ((float)_rng.NextDouble() > findRate)
            return DataTransferResult.NotFound(node.Id);

        // Value range scaled by system difficulty + node color tier
        (int minVal, int maxVal) = DataValueRange(System.Difficulty, node.Color);
        int sizeInMp = DataSizeRange(System.Difficulty);
        int value    = _rng.Next(minVal, maxVal + 1);

        var dataFile  = DataFile.CreateSellable($"Data-{_rng.Next(1000, 9999)}", sizeInMp, value);
        var addResult = Decker.ReceiveDataFile(dataFile);

        if (addResult.IsFailure)
            return DataTransferResult.Fail(node.Id,
                $"File found but deck storage full: {addResult.Error}");

        // Mark this node's data as extracted — it cannot be looted again
        _extractedDataNodes.Add(node.Id);

        Log(SessionEventType.DataFileFound,
            $"Data file '{dataFile.Name}' found ({sizeInMp}Mp, {value}\u00a5).");

        return DataTransferResult.Ok(node.Id, dataFile);
    }

    /// <summary>
    /// Returns the nuyen value range for a data file based on dungeon difficulty and node colour.
    /// Colour acts as a sub-tier within each difficulty band:
    ///   Blue (0) → lowest third, Green (1) → lower-mid, Orange (2) → upper-mid, Red (3) → top.
    /// </summary>
    private static (int min, int max) DataValueRange(string difficulty, NodeColor color)
    {
        // Each difficulty defines [minFloor, maxCeiling] for the whole band.
        // Colour subdivides that range into four overlapping sub-bands.
        return difficulty switch
        {
            "simple" => color switch
            {
                NodeColor.Blue   => (400,  600),
                NodeColor.Green  => (480,  680),
                NodeColor.Orange => (600,  800),
                _                => (700,  900),   // Red
            },
            "moderate" => color switch
            {
                NodeColor.Blue   => (1_000, 1_400),
                NodeColor.Green  => (1_200, 1_700),
                NodeColor.Orange => (1_600, 2_100),
                _                => (2_000, 2_500), // Red
            },
            _ => color switch  // "expert" (and any future tier)
            {
                NodeColor.Blue   => (5_000,  6_500),
                NodeColor.Green  => (6_000,  7_500),
                NodeColor.Orange => (7_500,  9_000),
                _                => (8_000, 10_000), // Red
            },
        };
    }

    /// <summary>Returns a random data-file size in Megapulses, scaled to system difficulty.</summary>
    private int DataSizeRange(string difficulty) => difficulty switch
    {
        "simple"   => _rng.Next(20,  41),   // 20–40 Mp
        "moderate" => _rng.Next(40,  81),   // 40–80 Mp
        _          => _rng.Next(100, 141),  // expert: 100–140 Mp
    };

    // ─────────────────────────────────────────────────────────────────────────
    // QUERIES
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the remaining seconds until the given ICE's next attack, and the
    /// maximum interval between its attacks. Used by the UI to render cooldown bars.
    /// Returns (0, 1) when the ICE has no registered timer.
    /// </summary>
    public (float Remaining, float Max) GetIceAttackCooldown(Ice ice)
    {
        ArgumentNullException.ThrowIfNull(ice);
        float max       = ComputeAttackInterval(ice.EffectiveRating);
        float remaining = _iceAttackTimers.TryGetValue(ice.Id, out float t) ? t : max;
        return (Math.Max(0f, remaining), max);
    }



    private void TickIceAttack(Ice ice, float deltaTime, Node node)
    {
        if (!_iceAttackTimers.TryGetValue(ice.Id, out float timer)) return;

        timer -= deltaTime;
        if (timer > 0f)
        {
            _iceAttackTimers[ice.Id] = timer;
            return;
        }

        // Reset timer for next attack
        _iceAttackTimers[ice.Id] = ComputeAttackInterval(ice.EffectiveRating);

        // Firing an attack reveals this ICE's identity to the persona
        RevealIce(ice);

        // Apply Mirrors accuracy penalty
        float mirrorsPenalty = _mirrorsDurationRemaining > 0f ? MirrorsAccuracyPenalty : 0f;

        // Evasion check first
        if (Persona.EvadeAttack(ice))
        {
            Log(SessionEventType.CombatMiss,
                $"{ice.Spec.Type} attack evaded!");
            return;
        }

        // Roll the attack
        float rawDamage = ice.RollAttack(
            Persona.Deck.Stats.Evasion,
            Persona.Deck.Stats.Bod,
            Persona.Deck.Stats.Hardening);

        if (rawDamage <= 0f)
        {
            Log(SessionEventType.CombatMiss,
                $"{ice.Spec.Type} attack missed.");
            return;
        }

        // Mirrors reduces accuracy further
        if (mirrorsPenalty > 0f && (float)_rng.NextDouble() <= mirrorsPenalty)
        {
            Log(SessionEventType.CombatMiss,
                $"Mirrors deflected {ice.Spec.Type} attack!");
            return;
        }

        // Shield absorbs damage first
        if (_shieldRemaining > 0f && ice.Spec.Type != IceType.BlackIce)
        {
            float absorbed = Math.Min(_shieldRemaining, rawDamage);
            _shieldRemaining -= absorbed;
            rawDamage        -= absorbed;

            if (rawDamage <= 0f)
            {
                Log(SessionEventType.CombatMiss,
                    $"Shield fully absorbed {ice.Spec.Type} attack ({absorbed:F1} dmg blocked).");
                return;
            }
        }

        // Apply to Persona (BlackIce → physical; everything else → energy)
        var damageEvents = Persona.TakeDamage(rawDamage, ice);
        foreach (var evt in damageEvents)
            ApplyIceEvent(evt, null);

        // Log energy damage for non-BlackIce (BlackIce damage is logged via PhysicalDamageDealtEvent)
        if (ice.Spec.Type != IceType.BlackIce)
        {
            Log(SessionEventType.PersonaDamaged,
                $"{ice.Spec.Type} R{ice.EffectiveRating} hit for {rawDamage:F1} energy damage.");
        }
    }

    public static float ComputeAttackInterval(int effectiveRating)
    {
        // Higher rating = faster attacks; minimum 0.5 s
        float interval = BaseIceAttackInterval / (1f + effectiveRating * 0.2f);
        return Math.Max(0.5f, interval);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PRIVATE — Status effect tick
    // ─────────────────────────────────────────────────────────────────────────

    private void TickStatusEffects(float deltaTime)
    {
        if (_smokeDurationRemaining > 0f)
            _smokeDurationRemaining = Math.Max(0f, _smokeDurationRemaining - deltaTime);

        if (_mirrorsDurationRemaining > 0f)
            _mirrorsDurationRemaining = Math.Max(0f, _mirrorsDurationRemaining - deltaTime);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PRIVATE — Run objective checking
    // ─────────────────────────────────────────────────────────────────────────

    private void CheckRunObjective(NodeAction action, string nodeId, string? filename)
    {
        if (ActiveRun is null || ActiveRun.IsComplete) return;

        if (ActiveRun.CheckObjectiveMet(action, nodeId, System.Id, filename))
        {
            ActiveRun.MarkComplete(objectiveAchieved: true);
            Log(SessionEventType.RunObjectiveMet,
                $"Run objective met: {ActiveRun.Objective} at '{nodeId}'.");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PRIVATE — Utility
    // ─────────────────────────────────────────────────────────────────────────

    private void GuardSessionActive()
    {
        if (!IsActive)
            throw new InvalidOperationException(
                "This session has ended. Create a new session to jack in again.");
    }

    private void Log(SessionEventType type, string description)
    {
        _log.Add(new SessionEvent(type, description));
    }

    /// <summary>
    /// Adds a UI-originated note to the session log (e.g. node action results
    /// that should appear with a timestamp rather than as sticky local messages).
    /// </summary>
    public void LogNote(string description) =>
        Log(SessionEventType.NodeActionResult, description);

    /// <summary>Posts a green-coloured success note to the session log (DS transfers, SM shutdowns, etc.).</summary>
    public void LogSuccess(string description) =>
        Log(SessionEventType.NodeActionSuccess, description);

    /// <summary>Posts a red-coloured failure note to the session log.</summary>
    public void LogFailure(string description) =>
        Log(SessionEventType.NodeActionFailure, description);

    private int GetSlotIndex(Program program)
    {
        for (int i = 0; i < Cyberdeck.MaxLoadedPrograms; i++)
            if (Decker.Deck.GetSlot(i) == program) return i;
        return -1;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DISPLAY
    // ─────────────────────────────────────────────────────────────────────────

    public override string ToString() =>
        $"[MatrixSession] {Decker.Name} in '{System.Name}' | " +
        $"Node:{Persona.CurrentNodeId} Alert:{System.AlertState} " +
        $"Active:{IsActive} Events:{_log.Count}";
}
