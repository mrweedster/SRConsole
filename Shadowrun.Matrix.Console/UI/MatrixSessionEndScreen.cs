using Shadowrun.Matrix.Enums;
using Shadowrun.Matrix.Models;
using Shadowrun.Matrix.UI;

namespace Shadowrun.Matrix.UI.Screens;

/// <summary>
/// Screen 8 — Session End.
/// Displayed after jack-out, dump, or system crash.
/// Shows run stats, mission reward (if earned), and navigation options.
/// </summary>
public sealed class MatrixSessionEndScreen : IScreen
{
    private readonly MatrixSession _session;
    private readonly int           _systemNumber;
    private readonly string        _endReason;
    private readonly GameState     _gameState;

    public MatrixSessionEndScreen(MatrixSession session, int systemNumber, string endReason, GameState gameState)
    {
        _session      = session;
        _systemNumber = systemNumber;
        _endReason    = endReason;
        _gameState    = gameState;
    }

    /// <summary>Backwards-compatible overload used by callers without GameState.</summary>
    public MatrixSessionEndScreen(MatrixSession session, int systemNumber, string endReason)
        : this(session, systemNumber, endReason, new GameState()) { }

    public void Render(int w, int h)
    {
        MatrixSystem system  = _session.System;
        Persona      persona = _session.Persona;
        int          inner   = w - 2;

        int conqueredCount = system.Nodes.Values.Count(n => n.IsConquered);
        int totalCount     = system.Nodes.Count;

        string reasonLabel = _endReason switch
        {
            "jack_out"   => "Jack Out (clean)",
            "dumped"     => "PERSONA DUMPED",
            "deck_fried" => "DECK FRIED",
            "crashed"    => "System Crashed",
            _            => _endReason
        };
        bool isClean = _endReason is "jack_out" or "crashed";

        // ── Header ────────────────────────────────────────────────────────────
        RenderHelper.DrawWindowOpen("SESSION ENDED", w);

        VC.Write("\u2551");
        VC.ForegroundColor = isClean ? ConsoleColor.Green : ConsoleColor.Red;
        VC.Write(RenderHelper.Centre($"Reason: {reasonLabel}", inner));
        VC.ResetColor();
        VC.WriteLine("\u2551");

        RenderHelper.DrawWindowDivider(w);

        // ── Stats ─────────────────────────────────────────────────────────────
        RenderHelper.DrawWindowStatLine("System:",          $"{system.Name}  [{system.Difficulty.ToUpper()}]", w);
        RenderHelper.DrawWindowStatLine("Nodes conquered:", $"{conqueredCount} / {totalCount}", w);

        if (_session.ActiveRun is not null)
        {
            // Replicate DrawWindowStatLine column layout exactly:
            //   prefix = "  {label}".PadRight(labelW)   gap = "  "   then value
            int    labelW   = Math.Clamp(inner * 35 / 100, 16, 28);
            string prefix   = "  Run objective:".PadRight(labelW);
            string objStatus = _session.ActiveRun.ObjectiveAchieved ? "ACCOMPLISHED \u2713" : "NOT ACHIEVED \u2717";
            ConsoleColor objColor = _session.ActiveRun.ObjectiveAchieved ? ConsoleColor.Green : ConsoleColor.Red;
            VC.Write("\u2551");
            VC.ForegroundColor = ConsoleColor.Gray;
            VC.Write(prefix + "  ");
            VC.ForegroundColor = objColor;
            VC.Write(objStatus.PadRight(inner - labelW - 2));
            VC.ResetColor();
            VC.WriteLine("\u2551");
        }

        TimeSpan duration = DateTimeOffset.UtcNow - _session.StartTime;
        RenderHelper.DrawWindowStatLine("Time in system:", $"{duration:mm\\:ss}", w);

        // ── Mission reward (if run was completed) ─────────────────────────────
        var reward = _gameState.PendingReward;
        if (reward?.Success == true)
        {
            RenderHelper.DrawWindowDivider(w);
            VC.Write("\u2551");
            VC.ForegroundColor = ConsoleColor.Yellow;
            VC.Write(RenderHelper.Centre("\u2605  MISSION COMPLETE  \u2605", inner));
            VC.ResetColor();
            VC.WriteLine("\u2551");
            RenderHelper.DrawWindowStatLine("Payment:",    $"+{reward.NuyenEarned}\u00a5", w);
            RenderHelper.DrawWindowStatLine("Karma:",      $"+{reward.KarmaEarned}", w);
            RenderHelper.DrawWindowStatLine("New balance:", $"{_session.Decker.Nuyen}\u00a5", w);
            _gameState.PendingReward = null; // consume
            _gameState.ActiveRun     = null; // run over
        }
        else if (_session.ActiveRun is not null)
        {
            // Only clear the run if it was definitively ended (failed or crashed).
            // On a clean jack-out the run may still be active — preserve it so
            // the Decker can re-enter the system and finish the job.
            if (_session.ActiveRun.IsComplete)
                _gameState.ActiveRun = null;
        }

        RenderHelper.DrawWindowDivider(w);

        // ── Final log entries ─────────────────────────────────────────────────
        var log    = _session.SessionLog;
        int toShow = Math.Min(5, log.Count);
        int start  = Math.Max(0, log.Count - toShow);

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
        VC.WriteLine("  [Q / Esc / Circle] Back to Main Menu".PadRight(w));
    }

    public IScreen? HandleInput(ConsoleKeyInfo key)
    {
        if (key.KeyChar is 'q' or 'Q'
            || key.Key == ConsoleKey.Escape
            || key.Key == ConsoleKey.Backspace)
            return NavigationToken.Root;

        return null;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ConsoleColor EventColor(SessionEventType t) => t switch
    {
        SessionEventType.PersonaDamaged    => ConsoleColor.Red,
        SessionEventType.PersonaDumped     => ConsoleColor.Red,
        SessionEventType.DeckDamaged       => ConsoleColor.Red,
        SessionEventType.AlertEscalated    => ConsoleColor.Yellow,
        SessionEventType.IceDefeated       => ConsoleColor.Green,
        SessionEventType.NodeConquered     => ConsoleColor.Green,
        SessionEventType.RunObjectiveMet   => ConsoleColor.Green,
        SessionEventType.ProgramRun        => ConsoleColor.Cyan,
        SessionEventType.ProgramFailed     => ConsoleColor.Yellow,
        SessionEventType.CombatEngaged     => ConsoleColor.Magenta,
        SessionEventType.TarEffectTriggered=> ConsoleColor.Red,
        _                                  => ConsoleColor.Gray
    };
}
