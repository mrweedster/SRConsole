using Shadowrun.Matrix.Core;
using Shadowrun.Matrix.Models;
using Shadowrun.Matrix.ValueObjects;

namespace Shadowrun.Matrix.UI.Screens;

/// <summary>
/// Screen — Decker Skills.
/// 50/50 split: left shows skill list with karma cost; right shows a
/// context-sensitive description of the currently selected skill.
/// Arrow keys / D-pad select; [Enter] or [+] spends karma.
/// </summary>
public sealed class DeckerSkillsScreen : IScreen
{
    private readonly Decker    _decker;
    private readonly GameState _gameState;

    private record SkillEntry(string Label, Func<int> Get, Func<Result> Upgrade, string Description);

    private readonly SkillEntry[] _skills;
    private int    _selectedIndex  = 0;
    private string _message        = "";
    private bool   _messageIsError = false;

    public DeckerSkillsScreen(Decker decker, GameState gameState)
    {
        _decker    = decker;
        _gameState = gameState;

        _skills =
        [
            new("Computer",    () => decker.Skills.Computer,
                () => decker.UpgradeComputerSkill(decker.Skills.Computer + 1),
                "Core Matrix skill. Improves success rate of ALL programs, " +
                "boosts data-find rate on DS nodes, and reduces system detection " +
                "frequency. Minimum skill 5-6 for reliable data runs. " +
                "The most important skill to raise first."),

            new("Combat",      () => decker.Skills.Combat,
                () => decker.UpgradeCombatSkill(decker.Skills.Combat + 1),
                "Attack accuracy and damage against ICE. Most valuable when " +
                "fighting Barrier or BlackIce with the Attack program. Less " +
                "critical if relying on instant-kill programs like Deception."),

            new("Negotiation", () => decker.Skills.Negotiation,
                () => decker.UpgradeNegotiationSkill(decker.Skills.Negotiation + 1),
                "Reduces purchase prices for programs, decks, and upgrades. " +
                "Each point above 2 cuts cost by ~3% of base price. " +
                "Low priority — raise only after Computer and Combat are solid."),
        ];
    }

    public void Render(int w, int h)
    {
        int inner  = w - 2;
        int leftW  = (inner - 1) / 2;
        int rightW = inner - 1 - leftW;

        RenderHelper.DrawWindowOpen("[Main Menu -> Decker -> Skills]", w);

        // Karma balance — full-width header
        VC.Write("\u2551");
        VC.ForegroundColor = ConsoleColor.Yellow;
        VC.Write($"  Available Karma: {_gameState.Karma}".PadRight(inner));
        VC.ResetColor();
        VC.WriteLine("\u2551");

        RenderHelper.DrawWindowDivider(w);

        // Wrap the selected skill's description into right-column lines
        var descLines = Wrap(_skills[_selectedIndex].Description, rightW - 2);
        int rows = Math.Max(_skills.Length, descLines.Count);

        for (int i = 0; i < rows; i++)
        {
            // Left side
            string leftContent;
            if (i < _skills.Length)
            {
                var  entry   = _skills[i];
                int  current = entry.Get();
                bool isMax   = current >= DeckerSkills.MaxSkill;
                int  cost    = Math.Max(1, current);
                string ptr   = i == _selectedIndex ? "\u25ba " : "  ";
                string val   = $"{current,2}/{DeckerSkills.MaxSkill}";
                string costStr = isMax ? " MAX" : $" {cost}k";
                string left  = $"{ptr}[{i+1}] {entry.Label,-14} {val}{costStr}";
                leftContent  = left.Length >= leftW ? left[..leftW] : left.PadRight(leftW);
            }
            else
            {
                leftContent = new string(' ', leftW);
            }

            // Right side
            string rightContent;
            if (i < descLines.Count)
            {
                string d = " " + descLines[i];
                rightContent = d.Length >= rightW ? d[..rightW] : d.PadRight(rightW);
            }
            else
            {
                rightContent = new string(' ', rightW);
            }

            bool highlighted = i == _selectedIndex && i < _skills.Length;
            VC.Write("\u2551");
            if (highlighted)
            {
                VC.BackgroundColor = ConsoleColor.DarkYellow;
                VC.ForegroundColor = ConsoleColor.Black;
            }
            else
            {
                bool mx = i < _skills.Length && _skills[i].Get() >= DeckerSkills.MaxSkill;
                VC.ForegroundColor = mx ? ConsoleColor.DarkGreen : ConsoleColor.Gray;
            }
            VC.Write(leftContent);
            VC.ResetColor();
            VC.ForegroundColor = ConsoleColor.DarkGray;
            VC.Write("\u2502"); // │
            VC.ForegroundColor = ConsoleColor.Gray;
            VC.Write(rightContent);
            VC.ResetColor();
            VC.WriteLine("\u2551");
        }

        RenderHelper.DrawWindowDivider(w);

        // Feedback / hint line
        VC.Write("\u2551");
        if (_message.Length > 0)
        {
            VC.ForegroundColor = _messageIsError ? ConsoleColor.Red : ConsoleColor.Green;
            string msg = $"  {_message}";
            VC.Write((msg.Length > inner ? msg[..inner] : msg).PadRight(inner));
        }
        else
        {
            VC.ForegroundColor = ConsoleColor.DarkGray;
            var  sel   = _skills[_selectedIndex];
            int  cur   = sel.Get();
            bool maxed = cur >= DeckerSkills.MaxSkill;
            int  cost  = Math.Max(1, cur);
            string hint = maxed
                ? $"  {sel.Label} is at maximum."
                : $"  [Enter /+] raise {sel.Label} {cur} \u2192 {cur+1}  (costs {cost} karma)";
            VC.Write((hint.Length > inner ? hint[..inner] : hint).PadRight(inner));
        }
        VC.ResetColor();
        VC.WriteLine("\u2551");

        RenderHelper.DrawWindowClose(w);
        VC.WriteLine();
        VC.WriteLine(("  [\u2191\u2193] Select   [Enter /+] Invest karma   [Backspace] Back").PadRight(w));
    }

    public IScreen? HandleInput(ConsoleKeyInfo key)
    {
        _message = "";

        if (key.Key == ConsoleKey.Escape)    return NavigationToken.Back;
        if (key.Key == ConsoleKey.Backspace) return NavigationToken.Back;

        if (key.Key == ConsoleKey.UpArrow)
            { _selectedIndex = (_selectedIndex - 1 + _skills.Length) % _skills.Length; return null; }
        if (key.Key == ConsoleKey.DownArrow)
            { _selectedIndex = (_selectedIndex + 1) % _skills.Length; return null; }

        if (key.KeyChar >= '1' && key.KeyChar <= (char)('0' + _skills.Length))
            { _selectedIndex = key.KeyChar - '1'; return null; }

        if (key.Key == ConsoleKey.Enter || key.KeyChar == '+')
            { TryInvest(_selectedIndex); return null; }

        return null;
    }

    private void TryInvest(int index)
    {
        var  entry   = _skills[index];
        int  current = entry.Get();
        bool isMax   = current >= DeckerSkills.MaxSkill;

        if (isMax)
        {
            _message        = $"{entry.Label} is already at maximum ({current}).";
            _messageIsError = true;
            return;
        }

        int cost = Math.Max(1, current);
        if (_gameState.Karma < cost)
        {
            _message        = $"Not enough karma \u2014 need {cost}, have {_gameState.Karma}.";
            _messageIsError = true;
            return;
        }

        var result = entry.Upgrade();
        if (result.IsFailure)
        {
            _message        = result.Error ?? "Upgrade failed.";
            _messageIsError = true;
            return;
        }

        _gameState.Karma -= cost;
        _message         = $"{entry.Label} raised to {entry.Get()}!  ({_gameState.Karma} karma remaining)";
        _messageIsError  = false;
    }

    private static List<string> Wrap(string text, int maxWidth)
    {
        var lines = new List<string>();
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var sb    = new System.Text.StringBuilder();
        foreach (string word in words)
        {
            int needed = sb.Length == 0 ? word.Length : sb.Length + 1 + word.Length;
            if (needed > maxWidth && sb.Length > 0) { lines.Add(sb.ToString()); sb.Clear(); }
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(word);
        }
        if (sb.Length > 0) lines.Add(sb.ToString());
        return lines;
    }
}
