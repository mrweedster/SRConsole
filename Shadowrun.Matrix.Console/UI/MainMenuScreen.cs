using Shadowrun.Matrix.Models;
using Shadowrun.Matrix.UI;

namespace Shadowrun.Matrix.UI.Screens;

/// <summary>
/// Screen 1 — Main Menu.
/// Items: [1] Decker  [2] Matrix Contracts  [3] Enter the Matrix  [4] Black Market  [0] Exit
/// </summary>
public sealed class MainMenuScreen : MenuScreen
{
    private readonly Decker                        _decker;
    private readonly IReadOnlyList<MatrixRunEntry> _runs;
    private readonly GameState                     _gameState;

    public MainMenuScreen(Decker decker, IReadOnlyList<MatrixRunEntry> runs, GameState gameState)
    {
        _decker    = decker;
        _runs      = runs;
        _gameState = gameState;
    }

    private bool InActiveSession => _gameState.ActiveSession?.IsActive == true;

    protected override int GetItemCount() => InActiveSession ? 6 : 5;

    protected override IScreen? OnItemConfirmed(int index) => index switch
    {
        0 => new DeckerScreen(_decker, _gameState),
        1 => new MatrixContractsScreen(
                _runs.Where(e => !e.Run.RewardClaimed).ToList().AsReadOnly(),
                run => { _gameState.ActiveRun = run; return new MatrixSystemSelectScreen(_decker, _gameState); }),
        2 => new MatrixSystemSelectScreen(_decker, _gameState),
        3 => new BlackMarketScreen(_decker),
        4 when InActiveSession => new MatrixGameScreen(
                _gameState.ActiveSession!, _gameState.ActiveSystemNumber, _gameState),
        4 when !InActiveSession => new QuitConfirmScreen(_decker, _gameState),
        5 when InActiveSession  => new QuitConfirmScreen(_decker, _gameState),
        _ => null
    };

    public override IScreen? HandleInput(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.Escape || key.Key == ConsoleKey.Backspace)
            return new QuitConfirmScreen(_decker, _gameState);
        return base.HandleInput(key);
    }

    public override void Render(int w, int h)
    {
        RenderHelper.DrawBox("[Main Menu]", w);
        VC.WriteLine();

        // Active run banner
        if (_gameState.ActiveRun is not null)
        {
            var run = _gameState.ActiveRun;
            VC.ForegroundColor = ConsoleColor.Yellow;
            string runTag = $"  ★ ACTIVE RUN: {run.TargetNodeTitle} | {run.Difficulty.ToUpper()} | {(run.ObjectiveAchieved ? "DONE ✓" : "pending")}";
            if (runTag.Length > w - 1) runTag = runTag[..(w - 1)];
            VC.WriteLine(runTag.PadRight(w));
            VC.ResetColor();
            VC.WriteLine();
        }

        RenderHelper.DrawPlainMenuItem(1, "Decker",                     SelectedIndex == 0, w);
        RenderHelper.DrawPlainMenuItem(2, "Available Matrix contracts",  SelectedIndex == 1, w);
        RenderHelper.DrawPlainMenuItem(3, "Enter the Matrix",            SelectedIndex == 2, w);
        RenderHelper.DrawPlainMenuItem(4, "Black Market",                SelectedIndex == 3, w);

        if (InActiveSession)
        {
            VC.ForegroundColor = ConsoleColor.Cyan;
            RenderHelper.DrawPlainMenuItem(5, "\u21aa RETURN TO MATRIX", SelectedIndex == 4, w);
            VC.ResetColor();
        }

        int exitIdx = InActiveSession ? 5 : 4;
        RenderHelper.DrawPlainMenuItem(0, "Exit", SelectedIndex == exitIdx, w);

        VC.WriteLine();
        VC.WriteLine("  Selection:".PadRight(w));

        if (PendingError is not null)
        {
            RenderHelper.DrawErrorLine(PendingError, w);
            PendingError = null;
        }
    }
}
