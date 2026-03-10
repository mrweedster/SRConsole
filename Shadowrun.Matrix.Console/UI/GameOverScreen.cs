using Shadowrun.Matrix.Models;
using Shadowrun.Matrix.UI;

namespace Shadowrun.Matrix.UI.Screens;

/// <summary>
/// Game Over screen — displayed when the Decker's physical health is depleted
/// by BlackIce during a Matrix run.
/// Shows final stats; any key returns to the name-entry screen to start a new game.
/// </summary>
public sealed class GameOverScreen : IScreen
{
    private readonly MatrixSession _session;
    private readonly GameState     _gameState;

    public GameOverScreen(MatrixSession session, GameState gameState)
    {
        _session   = session;
        _gameState = gameState;

        // Clean up run state — the decker is dead, all contracts are void
        _gameState.ActiveRun    = null;
        _gameState.ActiveSession = null;
    }

    public void Render(int w, int h)
    {
        int inner = w - 2;

        RenderHelper.DrawWindowOpen("GAME OVER", w);

        // Big red death notice
        void RedCentre(string text)
        {
            VC.Write("\u2551");
            VC.ForegroundColor = ConsoleColor.Red;
            VC.Write(RenderHelper.Centre(text, inner));
            VC.ResetColor();
            VC.WriteLine("\u2551");
        }

        RedCentre("");
        RedCentre("\u2620  DECKER FLATLINED  \u2620");
        RedCentre("BlackIce penetrated the jack and killed you.");
        RedCentre("");

        RenderHelper.DrawWindowDivider(w);

        // Final stats
        RenderHelper.DrawWindowStatLine("Decker:",        _session.Decker.Name, w);
        RenderHelper.DrawWindowStatLine("System:",        $"{_session.System.Name}  [{_session.System.Difficulty.ToUpper()}]", w);

        int conquered = _session.System.Nodes.Values.Count(n => n.IsConquered);
        int total     = _session.System.Nodes.Count;
        RenderHelper.DrawWindowStatLine("Nodes conquered:", $"{conquered} / {total}", w);

        TimeSpan duration = DateTimeOffset.UtcNow - _session.StartTime;
        RenderHelper.DrawWindowStatLine("Time in system:",  $"{duration:mm\\:ss}", w);
        RenderHelper.DrawWindowStatLine("Nuyen:",           $"{_session.Decker.Nuyen}\u00a5", w);

        RenderHelper.DrawWindowDivider(w);

        // Last log entries
        var log    = _session.SessionLog;
        int toShow = Math.Min(4, log.Count);
        int start  = Math.Max(0, log.Count - toShow);
        for (int i = start; i < log.Count; i++)
        {
            var    evt   = log[i];
            string entry = $"  {evt.Timestamp:HH:mm:ss}  {RenderHelper.Truncate(evt.Description, inner - 14)}";
            VC.Write("\u2551");
            VC.ForegroundColor = ConsoleColor.DarkRed;
            VC.Write(entry.PadRight(inner));
            VC.ResetColor();
            VC.WriteLine("\u2551");
        }

        RenderHelper.DrawWindowClose(w);
        VC.WriteLine();
        VC.Write("\u2551");
        VC.ForegroundColor = ConsoleColor.DarkGray;
        VC.Write("  Press any key to start a new game...".PadRight(inner));
        VC.ResetColor();
        VC.WriteLine("\u2551");
    }

    public IScreen? HandleInput(ConsoleKeyInfo key)
    {
        // Any key starts a new game (returns to the name-entry screen)
        return NavigationToken.NewGame;
    }
}
