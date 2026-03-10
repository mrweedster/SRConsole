using Shadowrun.Matrix.Data;
using Shadowrun.Matrix.Models;
using Shadowrun.Matrix.UI;

namespace Shadowrun.Matrix.UI.Screens;

/// <summary>
/// Screen — Matrix System Select.
/// Lists all 25 available systems. Supports arrow-key navigation.
/// When an active run is set, the target system is pre-selected and highlighted.
/// </summary>
public sealed class MatrixSystemSelectScreen : IScreen
{
    private readonly Decker    _decker;
    private readonly GameState _gameState;
    private int     _cursorIdx;
    private string? _errorMessage;
    private readonly IReadOnlyList<SystemDefinition> _allDefs;

    public MatrixSystemSelectScreen(Decker decker, GameState gameState)
    {
        _decker    = decker;
        _gameState = gameState;

        _allDefs = SystemCatalog.All.Values
            .OrderBy(d => d.SystemNumber)
            .ToList();

        // Pre-select active run's target system
        if (gameState.ActiveRun is not null &&
            int.TryParse(gameState.ActiveRun.TargetSystemId, out int targetNum))
        {
            int idx = _allDefs
                .Select((d, i) => (d, i))
                .FirstOrDefault(x => x.d.SystemNumber == targetNum).i;
            _cursorIdx = idx >= 0 ? idx : 0;
        }
    }

    public void Render(int w, int h)
    {
        int visibleRows = Math.Max(1, h - 8);
        MatrixRun? activeRun = _gameState.ActiveRun;

        string title = activeRun is not null
            ? "[Enter the Matrix  \u2014  \u2605 Mission system pre-selected]"
            : "[Enter the Matrix  \u2014  Select Target System]";

        RenderHelper.DrawWindowOpen(title, w);
        RenderHelper.DrawWindowDivider(w);

        int count  = _allDefs.Count;
        int shown  = Math.Min(count, visibleRows);
        int inner  = w - 2;

        int scrollOffset = Math.Clamp(_cursorIdx - visibleRows / 2, 0, Math.Max(0, count - visibleRows));

        int? missionNum = null;
        if (activeRun is not null && int.TryParse(activeRun.TargetSystemId, out int tn))
            missionNum = tn;

        for (int i = scrollOffset; i < scrollOffset + shown && i < count; i++)
        {
            var    def       = _allDefs[i];
            bool   isMission = missionNum.HasValue && def.SystemNumber == missionNum.Value;
            bool   isCursor  = i == _cursorIdx;

            string corp   = def.CorporationName is not null ? $" [{def.CorporationName}]" : "";
            string prefix = isCursor ? "\u25ba" : isMission ? "\u2605" : " ";
            string label  = $" {prefix} [{def.SystemNumber,2}]  {def.Name}{corp}";
            string diff   = $"({def.Difficulty})";

            int col2  = Math.Min(inner - diff.Length - 1, inner * 72 / 100);
            int gap   = Math.Max(1, col2 - label.Length);
            string line = (label + new string(' ', gap) + diff).PadRight(inner);
            if (line.Length > inner) line = line[..inner];

            VC.Write("\u2551");
            if (isCursor)
            {
                VC.BackgroundColor = ConsoleColor.DarkBlue;
                VC.ForegroundColor = ConsoleColor.White;
            }
            else if (isMission)
                VC.ForegroundColor = ConsoleColor.Yellow;
            else
            {
                VC.ForegroundColor = def.Difficulty switch
                {
                    "simple"   => ConsoleColor.Green,
                    "moderate" => ConsoleColor.Yellow,
                    "expert"   => ConsoleColor.Red,
                    _          => ConsoleColor.Gray
                };
            }
            VC.Write(line);
            VC.ResetColor();
            VC.WriteLine("\u2551");
        }

        RenderHelper.DrawWindowClose(w);
        VC.WriteLine();

        if (activeRun is not null)
        {
            VC.ForegroundColor = ConsoleColor.Yellow;
            string hint = $"  \u2605 Mission: {activeRun.TargetNodeTitle}  |  Press [Enter] to jack in";
            if (hint.Length > w - 1) hint = hint[..(w - 1)];
            VC.WriteLine(hint.PadRight(w));
            VC.ResetColor();
        }

        VC.WriteLine("  [\u2191\u2193] Navigate   [Enter] Jack In   [Esc] Back".PadRight(w));

        if (_errorMessage is not null)
        {
            RenderHelper.DrawErrorLine(_errorMessage, w);
            _errorMessage = null;
        }
    }

    public IScreen? HandleInput(ConsoleKeyInfo key)
    {
        int count = _allDefs.Count;

        switch (key.Key)
        {
            case ConsoleKey.Escape:
            case ConsoleKey.Backspace when _errorMessage is null:
                return NavigationToken.Back;

            case ConsoleKey.UpArrow:
                _cursorIdx = (_cursorIdx - 1 + count) % count;
                return null;

            case ConsoleKey.DownArrow:
                _cursorIdx = (_cursorIdx + 1) % count;
                return null;

            case ConsoleKey.Enter:
                return TryJackIn(_allDefs[_cursorIdx].SystemNumber);

            default:
                return null;
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private IScreen? TryJackIn(int num)
    {
        if (_decker.Deck.IsBroken)
        {
            _errorMessage = "Deck MPCP is fried \u2014 repair before jacking in.";
            return null;
        }

        if (_decker.IsJackedIn)
        {
            _errorMessage = "Already jacked in \u2014 jack out first.";
            return null;
        }

        MatrixSystem system = SystemCatalog.BuildSystem(num);

        // Attach active run only if it targets this system
        MatrixRun? activeRun = _gameState.ActiveRun;
        if (activeRun is not null &&
            (!int.TryParse(activeRun.TargetSystemId, out int targetNum) || targetNum != num))
        {
            activeRun = null;
        }

        var jackResult = _decker.JackIn(system.SanNodeId);
        if (jackResult.IsFailure)
        {
            _errorMessage = jackResult.Error;
            return null;
        }

        var session = new MatrixSession(_decker, system, jackResult.Value, activeRun);
        return new MatrixGameScreen(session, num, _gameState);
    }
}
