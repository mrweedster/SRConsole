using Shadowrun.Matrix.Models;
using Shadowrun.Matrix.Persistence;
using Shadowrun.Matrix.UI;

namespace Shadowrun.Matrix.UI.Screens;

public sealed class QuitConfirmScreen : MenuScreen
{
    private readonly Decker    _decker;
    private readonly GameState _gameState;

    public QuitConfirmScreen(Decker decker, GameState gameState)
    {
        _decker    = decker;
        _gameState = gameState;
        SetSelectedIndex(1);
    }

    protected override int GetItemCount() => 2;

    protected override IScreen? OnItemConfirmed(int index)
    {
        if (index == 0)
        {
            SaveGameManager.Save(_decker, _gameState);
            VC.CursorVisible = true;
            Environment.Exit(0);
        }
        return NavigationToken.Back;
    }

    public override IScreen? HandleInput(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.Escape || key.Key == ConsoleKey.Backspace) return NavigationToken.Back;
        if (key.KeyChar is 'y' or 'Y') return OnItemConfirmed(0);
        if (key.KeyChar is 'n' or 'N') return NavigationToken.Back;
        return base.HandleInput(key);
    }

    public override void Render(int w, int h)
    {
        RenderHelper.DrawWindowOpen("[Exit Game]", w);
        RenderHelper.DrawWindowCentredLine("Are you sure you want to quit?", w);
        RenderHelper.DrawWindowBlankLine(w);
        VC.Write("\u2551");
        VC.ForegroundColor = ConsoleColor.DarkGray;
        VC.Write("  Game will be saved automatically.".PadRight(w - 2));
        VC.ResetColor();
        VC.WriteLine("\u2551");
        RenderHelper.DrawWindowDivider(w);
        RenderHelper.DrawWindowMenuItem(1, "Yes — save and exit",  null, SelectedIndex == 0, w);
        RenderHelper.DrawWindowMenuItem(2, "No  — return to menu", null, SelectedIndex == 1, w);
        RenderHelper.DrawWindowClose(w);
        VC.WriteLine();
        VC.WriteLine("  Selection:".PadRight(w));
    }
}
