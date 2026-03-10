using Shadowrun.Matrix.UI;

namespace Shadowrun.Matrix.UI.Screens;

/// <summary>
/// Confirmation screen for cancelling the currently active Matrix run contract.
/// The run is removed from GameState on confirmation; it cannot be recovered.
/// </summary>
public sealed class CancelRunConfirmScreen : MenuScreen
{
    private readonly GameState _gameState;

    public CancelRunConfirmScreen(GameState gameState)
    {
        _gameState = gameState;
        SetSelectedIndex(0); // default to No
    }

    protected override int GetItemCount() => 2;

    protected override IScreen? OnItemConfirmed(int index)
    {
        if (index == 1) // Yes — cancel the run
        {
            _gameState.ActiveRun = null;
            return NavigationToken.Back; // back to DeckerScreen
        }
        return NavigationToken.Back; // No — go back unchanged
    }

    public override void Render(int w, int h)
    {
        int inner = w - 2;
        RenderHelper.DrawWindowOpen("[Decker -> Cancel Run]", w);

        VC.Write("\u2551");
        VC.ForegroundColor = ConsoleColor.Yellow;
        VC.Write("  WARNING: This will permanently abandon the active run contract.".PadRight(inner)[..inner]);
        VC.ResetColor();
        VC.WriteLine("\u2551");

        if (_gameState.ActiveRun is not null)
        {
            RenderHelper.DrawWindowStatLine("Run target:", _gameState.ActiveRun.TargetNodeTitle, w);
            RenderHelper.DrawWindowStatLine("Difficulty:", _gameState.ActiveRun.Difficulty.ToUpper(), w);
            RenderHelper.DrawWindowStatLine("Reward:",     $"{_gameState.ActiveRun.BasePayNuyen}\u00a5 +{_gameState.ActiveRun.KarmaReward}K (forfeited)", w);
        }

        RenderHelper.DrawWindowDivider(w);
        RenderHelper.DrawWindowMenuItem(1, "NO  — Keep the run active", "",   SelectedIndex == 0, w);
        RenderHelper.DrawWindowMenuItem(2, "YES — Abandon the contract", "!", SelectedIndex == 1, w);
        RenderHelper.DrawWindowClose(w);
        VC.WriteLine();
        VC.WriteLine("  Selection:".PadRight(w));
    }
}
