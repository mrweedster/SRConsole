using Shadowrun.Matrix.Enums;
using Shadowrun.Matrix.Models;
using Shadowrun.Matrix.UI;
using MatrixProgram = Shadowrun.Matrix.Models.Program;

namespace Shadowrun.Matrix.UI.Screens;

/// <summary>
/// Screen 4 — Run Program sub-menu.
/// Verbose narrative messages for every program action outcome.
/// </summary>
public sealed class MatrixRunProgramScreen : IScreen
{
    private readonly MatrixSession _session;
    private readonly int           _systemNumber;
    private readonly GameState     _gameState;

    private string? _actionNarrative;
    private string? _resultMessage;
    private bool    _sessionEndedAfterRun;

    public MatrixRunProgramScreen(MatrixSession session, int systemNumber, GameState? gameState = null)
    {
        _session      = session;
        _systemNumber = systemNumber;
        _gameState    = gameState ?? new GameState();
    }

    public void Render(int w, int h)
    {
        if (_resultMessage is not null) { RenderResult(w); return; }

        var slots     = _session.Decker.Deck.LoadedSlots;
        var activeIce = _session.System.GetNode(_session.Persona.CurrentNodeId).GetActiveIce();

        RenderHelper.DrawWindowOpen("[Run Program]", w);
        RenderHelper.DrawWindowDivider(w);
        RenderHelper.DrawWindowBlankLine(w);
        for (int i = 0; i < 5; i++)
            RenderSlot(i + 1, slots[i], activeIce, w);
        RenderHelper.DrawWindowBlankLine(w);
        RenderHelper.DrawWindowClose(w);
        VC.WriteLine();
        VC.WriteLine("  Select slot (1\u20135) or [0] to cancel:".PadRight(w));
    }

    public IScreen? HandleInput(ConsoleKeyInfo key)
    {
        if (_resultMessage is not null)
        {
            if (_sessionEndedAfterRun) { _gameState.ActiveSession = null; return new MatrixSessionEndScreen(_session, _systemNumber, "dumped", _gameState); }
            return NavigationToken.Back;
        }
        if (key.Key == ConsoleKey.Escape || key.KeyChar == '0') return NavigationToken.Back;
        if (key.KeyChar >= '1' && key.KeyChar <= '5') { ExecuteRun(key.KeyChar - '1'); return null; }
        return null;
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void ExecuteRun(int slotIndex)
    {
        var prog = _session.Decker.Deck.LoadedSlots[slotIndex];

        if (prog is null)
        {
            _actionNarrative = "You reach for an empty deck slot — nothing to run.";
            _resultMessage   = "Slot is empty. Load a program first.";
            return;
        }
        if (!prog.IsReadyToRun)
        {
            _actionNarrative = $"You try to activate {prog.Spec.Name} L{prog.Spec.Level} but it isn't ready.";
            _resultMessage   = prog.LoadProgress < 1.0f
                ? $"Still loading — {prog.LoadProgress:P0} complete. Wait for it to finish."
                : $"Refreshing — program cooling down. Give it a moment.";
            return;
        }

        var activeIce      = _session.System.GetNode(_session.Persona.CurrentNodeId).GetActiveIce();
        _actionNarrative   = BuildNarrative(prog, activeIce);
        int logBefore      = _session.SessionLog.Count;

        _session.RunProgram(slotIndex);
        _session.TickFrame(4.0f);

        var newEvents  = _session.SessionLog.Skip(logBefore).ToList();
        _resultMessage = BuildOutcome(prog, activeIce, newEvents);

        if (!_session.IsActive) _sessionEndedAfterRun = true;
    }

    private static string BuildNarrative(MatrixProgram prog, Ice? ice)
    {
        string n = prog.Spec.Name.ToString(), lv = $"L{prog.Spec.Level}";
        if (ice is null)
        {
            return prog.Spec.Name switch
            {
                ProgramName.Medic   => $"You fire up {n} {lv} to restore Persona integrity.",
                ProgramName.Analyze => $"You run {n} {lv} to scan the node and ICE.",
                ProgramName.Degrade => $"You run {n} {lv} to erode this node's security rating.",
                _                   => $"You activate {n} {lv}.",
            };
        }
        return prog.Spec.Name switch
        {
            ProgramName.Attack    => $"You unleash {n} {lv} against the {ice.Spec.Type} — a direct assault.",
            ProgramName.Deception => $"You run {n} {lv}, weaving false credentials past the {ice.Spec.Type}.",
            ProgramName.Slow      => $"You activate {n} {lv} to drag the {ice.Spec.Type}'s reaction time down.",
            ProgramName.Mirrors   => $"You throw {n} {lv} up to deflect the {ice.Spec.Type}'s targeting.",
            ProgramName.Shield    => $"You harden your Persona with {n} {lv} against the {ice.Spec.Type}.",
            ProgramName.Smoke     => $"You detonate {n} {lv}, flooding the dataspace with interference.",
            ProgramName.Sleaze    => $"You ghost past the {ice.Spec.Type} with {n} {lv}.",
            ProgramName.Relocate  => $"You trigger {n} {lv} to reroute the {ice.Spec.Type}'s trace vectors.",
            ProgramName.Medic     => $"You fire up {n} {lv} mid-combat to patch Persona damage.",
            ProgramName.Rebound   => $"You brace with {n} {lv} — next hit from the {ice.Spec.Type} gets reflected.",
            ProgramName.Degrade   => $"You run {n} {lv} to weaken the node and the {ice.Spec.Type} guarding it.",
            ProgramName.Analyze   => $"You scan the {ice.Spec.Type} with {n} {lv}.",
            _                     => $"You activate {n} {lv} against the {ice.Spec.Type}.",
        };
    }

    private static string BuildOutcome(MatrixProgram prog, Ice? ice, IReadOnlyList<SessionEvent> evts)
    {
        bool iceDefeated    = evts.Any(e => e.Type == SessionEventType.IceDefeated);
        bool nodeConquered  = evts.Any(e => e.Type == SessionEventType.NodeConquered);
        bool objMet         = evts.Any(e => e.Type == SessionEventType.RunObjectiveMet);
        bool dumped         = evts.Any(e => e.Type == SessionEventType.PersonaDumped);
        bool programRan     = evts.Any(e => e.Type == SessionEventType.ProgramRun);
        bool programFailed  = evts.Any(e => e.Type == SessionEventType.ProgramFailed);
        bool tarTriggered   = evts.Any(e => e.Type == SessionEventType.TarEffectTriggered);
        string n = prog.Spec.Name.ToString();

        string msg;
        if (dumped)
            msg = $"Critical failure — {n} backfired. Your Persona is dumped from the Matrix.";
        else if (tarTriggered)
        {
            // Tar ICE self-destructs after punishing the failed run. The "IceDefeated"
            // and "NodeConquered" events that may follow are a side-effect, not a victory.
            // Distinguish Tar Pit (permanent delete) from Tar Paper (memory flush) by
            // reading the log — the active-ice reference points at the primary, not the Tar.
            bool isPit = evts.Any(e =>
                e.Type == SessionEventType.TarEffectTriggered &&
                e.Description.Contains("permanently deleted", StringComparison.OrdinalIgnoreCase));
            string tarName    = isPit ? "Tar Pit" : "Tar Paper";
            string consequence = isPit
                ? $"{n} has been permanently deleted from your deck!"
                : $"{n} has been flushed from memory — it can be reloaded.";
            msg = $"{tarName} triggered — run failed. {consequence}";
        }
        else if (programFailed && ice is not null)
        {
            msg = prog.Spec.Name switch
            {
                ProgramName.Attack    => $"Attack failed — the {ice.Spec.Type} deflects your intrusion.",
                ProgramName.Deception => $"Deception failed — the {ice.Spec.Type} sees through your false credentials.",
                ProgramName.Sleaze    => $"Sleaze failed — the {ice.Spec.Type} detects you and engages.",
                ProgramName.Relocate  => $"Relocate failed — the trace vector could not be redirected.",
                ProgramName.Slow      => $"Slow failed — the {ice.Spec.Type} shrugs off the timing disruption.",
                ProgramName.Mirrors   => $"Mirrors failed — {ice.Spec.Type} targeting locks on regardless.",
                ProgramName.Degrade   => $"Degrade failed — the node's security held against your attack.",
                _                     => $"{n} had no effect on the {ice.Spec.Type}.",
            };
        }
        else if (iceDefeated)
            msg = nodeConquered
                ? $"{ice?.Spec.Type.ToString() ?? "ICE"} destroyed and node conquered — path ahead is clear."
                : $"{ice?.Spec.Type.ToString() ?? "ICE"} eliminated. The node is now undefended.";
        else if (programRan)
        {
            msg = prog.Spec.Name switch
            {
                ProgramName.Medic   => "Medic running — Persona integrity is being restored.",
                ProgramName.Analyze => "Scan complete — ICE profiles updated with success probabilities.",
                ProgramName.Degrade => "Security rating eroded — node and its ICE are now weaker.",
                ProgramName.Smoke   => "Dataspace flooded with interference — all actions penalised.",
                ProgramName.Mirrors => $"Mirrors active — {ice?.Spec.Type.ToString() ?? "ICE"} targeting accuracy reduced.",
                ProgramName.Shield  => "Shield reinforced — incoming damage per hit is reduced.",
                ProgramName.Rebound => $"Rebound primed — next hit from {ice?.Spec.Type.ToString() ?? "ICE"} is reflected.",
                ProgramName.Slow    => $"{ice?.Spec.Type.ToString() ?? "ICE"} slowed — reaction time degraded.",
                _                   => $"{n} executed successfully.",
            };
        }
        else
            msg = $"{n} had no effect this cycle.";

        if (objMet) msg += "\n\u2605 Run objective achieved — mission target met!";
        return msg;
    }

    private void RenderResult(int w)
    {
        int inner = w - 2;
        RenderHelper.DrawWindowOpen("[Program Result]", w);

        if (_actionNarrative is not null)
        {
            VC.Write("\u2551");
            VC.ForegroundColor = ConsoleColor.DarkGray;
            VC.Write(RenderHelper.Truncate(_actionNarrative, inner).PadRight(inner));
            VC.ResetColor();
            VC.WriteLine("\u2551");
            RenderHelper.DrawWindowDivider(w);
        }

        if (_resultMessage is not null)
        {
            var lines   = _resultMessage.Split('\n');
            bool failed = lines[0].Contains("failed") || lines[0].Contains("failure") ||
                          lines[0].Contains("empty") || lines[0].Contains("loading") ||
                          lines[0].Contains("cooling");
            VC.Write("\u2551");
            VC.ForegroundColor = failed ? ConsoleColor.Red : ConsoleColor.Green;
            VC.Write($"  {RenderHelper.Truncate(lines[0], inner - 2)}".PadRight(inner));
            VC.ResetColor();
            VC.WriteLine("\u2551");
            foreach (var extra in lines.Skip(1))
            {
                VC.Write("\u2551");
                VC.ForegroundColor = ConsoleColor.Yellow;
                VC.Write($"  {RenderHelper.Truncate(extra, inner - 2)}".PadRight(inner));
                VC.ResetColor();
                VC.WriteLine("\u2551");
            }
        }

        RenderHelper.DrawWindowDivider(w);

        var log   = _session.SessionLog;
        int count = Math.Min(5, log.Count);
        int start = Math.Max(0, log.Count - count);
        for (int i = start; i < log.Count; i++)
        {
            var    evt   = log[i];
            string entry = $"  {evt.Timestamp:HH:mm:ss}  {RenderHelper.Truncate(evt.Description, inner - 14)}";
            VC.Write("\u2551");
            VC.ForegroundColor = EventColor(evt.Type);
            VC.Write(entry.PadRight(inner));
            VC.ResetColor();
            VC.WriteLine("\u2551");
        }

        RenderHelper.DrawWindowClose(w);
        VC.WriteLine();
        VC.WriteLine("  [Any key] Continue".PadRight(w));
    }

    private void RenderSlot(int idx, MatrixProgram? prog, Ice? ice, int w)
    {
        int inner = w - 2;
        if (prog is null)
        {
            VC.Write("\u2551");
            VC.ForegroundColor = ConsoleColor.DarkGray;
            VC.Write($"  [{idx}]  (empty slot)".PadRight(inner));
            VC.ResetColor();
            VC.WriteLine("\u2551");
            return;
        }
        string status = !prog.IsLoaded        ? "Not loaded"
                      : prog.LoadProgress < 1f ? $"Loading {prog.LoadProgress:P0}"
                      :                           "Ready";
        string succ   = prog.IsReadyToRun && ice is not null
            ? $"  {_session.Persona.ComputeSuccessChance(prog, ice):P0}"
            : "  \u2014";
        string content = $"  [{idx}]  {prog.Spec.Name,-12}  L{prog.Spec.Level}  {status,-15}{succ}";
        if (content.Length > inner) content = RenderHelper.Truncate(content, inner);
        VC.Write("\u2551");
        if (!prog.IsReadyToRun) VC.ForegroundColor = ConsoleColor.DarkGray;
        else if (ice is not null) VC.ForegroundColor = ConsoleColor.Cyan;
        VC.Write(content.PadRight(inner));
        VC.ResetColor();
        VC.WriteLine("\u2551");
    }

    private static ConsoleColor EventColor(SessionEventType t) => t switch
    {
        SessionEventType.PersonaDamaged     => ConsoleColor.Red,
        SessionEventType.PersonaDumped      => ConsoleColor.Red,
        SessionEventType.DeckDamaged        => ConsoleColor.Red,
        SessionEventType.AlertEscalated     => ConsoleColor.Yellow,
        SessionEventType.TarEffectTriggered => ConsoleColor.Red,
        SessionEventType.IceDefeated        => ConsoleColor.Green,
        SessionEventType.NodeConquered      => ConsoleColor.Green,
        SessionEventType.RunObjectiveMet    => ConsoleColor.Green,
        SessionEventType.ProgramRun         => ConsoleColor.Cyan,
        SessionEventType.ProgramFailed      => ConsoleColor.Yellow,
        SessionEventType.CombatEngaged      => ConsoleColor.Magenta,
        _                                   => ConsoleColor.Gray
    };
}
