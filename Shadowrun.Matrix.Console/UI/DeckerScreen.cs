using Shadowrun.Matrix.Models;
using Shadowrun.Matrix.UI;

namespace Shadowrun.Matrix.UI.Screens;

public sealed class DeckerScreen : MenuScreen
{
    private readonly Decker    _decker;
    private readonly GameState _gameState;

    public DeckerScreen(Decker decker, GameState gameState)
    {
        _decker    = decker;
        _gameState = gameState;
    }

    // Items: 0=Skills, 1=Cyberdeck, [2=Run Info if active], [3=Cancel Run if active]
    protected override int GetItemCount() => _gameState.ActiveRun is not null ? 4 : 2;

    protected override IScreen? OnItemConfirmed(int index) => index switch
    {
        0 => new DeckerSkillsScreen(_decker, _gameState),
        1 => new CyberdeckScreen(_decker.Deck, _decker.IsJackedIn),
        2 when _gameState.ActiveRun is not null => new RunInfoScreen(_gameState.ActiveRun, _gameState, _decker),
        3 when _gameState.ActiveRun is not null => new CancelRunConfirmScreen(_gameState),
        _ => null
    };

    public override void Render(int w, int h)
    {
        RenderHelper.DrawWindowOpen("[Main Menu -> Decker]", w);
        RenderHelper.DrawWindowStatLine("Name:",         _decker.Name,                                                    w);
        RenderHelper.DrawWindowStatLine("Money:",        $"{_decker.Nuyen}\u00a5",                                       w);
        RenderHelper.DrawWindowStatLine("Physical Hlt:", $"{_decker.PhysicalHealth:F0} / {_decker.PhysicalHealthMax:F0}", w);
        RenderHelper.DrawWindowDivider(w);
        RenderHelper.DrawWindowMenuItem(1, "SKILLS",    "sub menu", SelectedIndex == 0, w);
        RenderHelper.DrawWindowMenuItem(2, "CYBERDECK", "sub menu", SelectedIndex == 1, w);

        if (_gameState.ActiveRun is not null)
        {
            string runLabel = $"RUN INFO — {_gameState.ActiveRun.TargetNodeTitle}";
            RenderHelper.DrawWindowMenuItem(3, runLabel, "active", SelectedIndex == 2, w);
            RenderHelper.DrawWindowMenuItem(4, "CANCEL ACTIVE RUN", "abandon contract", SelectedIndex == 3, w);
        }

        RenderHelper.DrawWindowClose(w);
        VC.WriteLine();
        VC.WriteLine("  Selection:".PadRight(w));
        if (PendingError is not null) { RenderHelper.DrawErrorLine(PendingError, w); PendingError = null; }
    }
}
