using Shadowrun.Matrix.Data;
using Shadowrun.Matrix.Models;
using Shadowrun.Matrix.UI;
using Shadowrun.Matrix.ValueObjects;

namespace Shadowrun.Matrix.UI.Screens;

/// <summary>
/// Shown once on a fresh save-less start.
/// Prompts the player to enter a hacker handle (default: Ghost).
/// On confirm, bootstraps the starter decker and pushes the Main Menu.
/// </summary>
public sealed class NewGameNameScreen : IScreen
{
    // ── State ─────────────────────────────────────────────────────────────────

    private readonly IReadOnlyList<MatrixRunEntry> _runs;
    /// <summary>Called with the newly created MainMenuScreen so the navigator root can be updated.</summary>
    private readonly Action<IScreen>? _onRootCreated;

    /// <summary>Current text in the input field.</summary>
    private string _name = "Ghost";

    /// <summary>Cursor position within _name (0 = before first char).</summary>
    private int _cursor;

    private string? _error;

    // ── Constants ─────────────────────────────────────────────────────────────

    private const int MaxNameLength = 20;

    // ── Construction ─────────────────────────────────────────────────────────

    public NewGameNameScreen(IReadOnlyList<MatrixRunEntry> runs, Action<IScreen>? onRootCreated = null)
    {
        _runs          = runs;
        _onRootCreated = onRootCreated;
        _cursor        = _name.Length; // start cursor at end of default name
    }

    // ── IScreen ───────────────────────────────────────────────────────────────

    public IScreen? HandleInput(ConsoleKeyInfo key)
    {
        _error = null;

        switch (key.Key)
        {
            // ── Confirm ───────────────────────────────────────────────────────
            case ConsoleKey.Enter:
            {
                string trimmed = _name.Trim();
                if (trimmed.Length == 0)
                {
                    _error = "Please enter a name.";
                    return null;
                }
                return BuildGame(trimmed);
            }

            // ── Delete / backspace ────────────────────────────────────────────
            case ConsoleKey.Backspace:
                if (_cursor > 0)
                {
                    _name   = _name.Remove(_cursor - 1, 1);
                    _cursor = Math.Max(0, _cursor - 1);
                }
                return null;

            case ConsoleKey.Delete:
                if (_cursor < _name.Length)
                    _name = _name.Remove(_cursor, 1);
                return null;

            // ── Cursor movement ───────────────────────────────────────────────
            case ConsoleKey.LeftArrow:
                _cursor = Math.Max(0, _cursor - 1);
                return null;

            case ConsoleKey.RightArrow:
                _cursor = Math.Min(_name.Length, _cursor + 1);
                return null;

            case ConsoleKey.Home:
                _cursor = 0;
                return null;

            case ConsoleKey.End:
                _cursor = _name.Length;
                return null;

            // ── Printable characters ──────────────────────────────────────────
            default:
                if (!char.IsControl(key.KeyChar) && _name.Length < MaxNameLength)
                {
                    _name   = _name.Insert(_cursor, key.KeyChar.ToString());
                    _cursor = Math.Min(_name.Length, _cursor + 1);
                }
                return null;
        }
    }

    public void Render(int w, int h)
    {
        // ── Centre a small dialog box ─────────────────────────────────────────
        const int BoxW = 44;
        int left = Math.Max(0, (w - BoxW) / 2);
        int row  = Math.Max(0, (h - 12) / 2);

        // Title art
        VC.ForegroundColor = ConsoleColor.Cyan;
        DrawLine("╔══════════════════════════════════════════╗", left, BoxW, ref row);
        DrawLine("║        S H A D O W R U N                 ║", left, BoxW, ref row);
        DrawLine("║         Matrix Console Game              ║", left, BoxW, ref row);
        DrawLine("╠══════════════════════════════════════════╣", left, BoxW, ref row);
        VC.ResetColor();

        // Prompt
        DrawLine("║                                          ║", left, BoxW, ref row);
        DrawColorLine("  Enter your hacker handle:", left, BoxW, ConsoleColor.White, ref row);
        DrawLine("║                                          ║", left, BoxW, ref row);

        // Input field
        string display = _name.Length > 0 ? _name : "";
        string field   = BuildFieldDisplay(display, BoxW - 8);
        DrawInputLine(field, _cursor, left, BoxW, ref row);

        DrawLine("║                                          ║", left, BoxW, ref row);

        // Hint
        DrawColorLine("  Press Enter to begin", left, BoxW, ConsoleColor.DarkGray, ref row);
        DrawLine("║                                          ║", left, BoxW, ref row);

        // Error line
        if (_error is not null)
            DrawColorLine($"  ! {_error}", left, BoxW, ConsoleColor.Red, ref row);
        else
            DrawLine("║                                          ║", left, BoxW, ref row);

        VC.ForegroundColor = ConsoleColor.Cyan;
        DrawLine("╚══════════════════════════════════════════╝", left, BoxW, ref row);
        VC.ResetColor();
    }

    // ── Helpers — all use VC.SetCursorPosition to stay inside the VC buffer ──

    /// <summary>Write a pre-built box line at (left, row) in cyan then advance row.</summary>
    private static void DrawLine(string line, int left, int boxW, ref int row)
    {
        VC.SetCursorPosition(left, row++);
        VC.ForegroundColor = ConsoleColor.Cyan;
        VC.WriteLine(line.PadRight(boxW)[..Math.Min(line.Length, boxW)]);
        VC.ResetColor();
    }

    /// <summary>Write a coloured text line inside the box borders at (left, row).</summary>
    private static void DrawColorLine(string text, int left, int boxW, ConsoleColor color, ref int row)
    {
        int    inner     = boxW - 2;
        string innerText = (" " + text).PadRight(inner)[..inner];
        VC.SetCursorPosition(left, row++);
        VC.ForegroundColor = ConsoleColor.Cyan;
        VC.Write("║");
        VC.ForegroundColor = color;
        VC.Write(innerText);
        VC.ForegroundColor = ConsoleColor.Cyan;
        VC.WriteLine("║");
        VC.ResetColor();
    }

    private static string BuildFieldDisplay(string name, int fieldWidth)
    {
        if (name.Length > fieldWidth) name = name[..fieldWidth];
        return name.PadRight(fieldWidth);
    }

    /// <summary>Write the name input line with an inline cursor highlight.</summary>
    private static void DrawInputLine(string field, int cursor, int left, int boxW, ref int row)
    {
        int inner  = boxW - 2;
        int indent = 4; // "  > " prefix

        VC.SetCursorPosition(left, row++);
        VC.ForegroundColor = ConsoleColor.Cyan;
        VC.Write("║");
        VC.ResetColor();
        VC.Write("  ");
        VC.ForegroundColor = ConsoleColor.Yellow;
        VC.Write("> ");
        VC.ResetColor();

        int maxVisible = inner - indent - 2;
        string display = field.Length > maxVisible ? field[..maxVisible] : field.PadRight(maxVisible);

        for (int i = 0; i < display.Length; i++)
        {
            if (i == cursor)
            {
                VC.ForegroundColor = ConsoleColor.Black;
                VC.BackgroundColor = ConsoleColor.White;
                VC.Write(i < field.TrimEnd().Length ? display[i] : '_');
                VC.ResetColor();
            }
            else
            {
                VC.ForegroundColor = ConsoleColor.White;
                VC.Write(display[i]);
                VC.ResetColor();
            }
        }

        bool endCursorWritten = cursor >= display.Length;
        if (endCursorWritten)
        {
            VC.ForegroundColor = ConsoleColor.Black;
            VC.BackgroundColor = ConsoleColor.White;
            VC.Write('_');
            VC.ResetColor();
        }

        // +1 only when the end cursor char was actually written; otherwise it
        // was already accounted for inside the display loop and adding 1 here
        // would over-count, pushing the closing ║ one position to the left.
        int written = indent + Math.Min(display.Length, maxVisible) + (endCursorWritten ? 1 : 0);
        VC.Write(new string(' ', Math.Max(0, inner - written)));
        VC.ForegroundColor = ConsoleColor.Cyan;
        VC.WriteLine("║");
        VC.ResetColor();
    }

    private IScreen BuildGame(string hackerName)
    {
        var skills    = new DeckerSkills(computer: 4, combat: 3, negotiation: 0);
        var starter   = BlackMarketCatalog.StarterDeck;
        var deck      = new Cyberdeck(starter.Name, starter.ToDeckStats());
        var decker    = new Decker(hackerName, deck, skills, startingNuyen: 1_000);
        var gameState = new GameState();
        var mainMenu  = new MainMenuScreen(decker, _runs, gameState);
        _onRootCreated?.Invoke(mainMenu);
        return mainMenu;
    }
}
