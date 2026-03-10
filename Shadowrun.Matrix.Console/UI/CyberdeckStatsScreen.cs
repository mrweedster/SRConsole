using Shadowrun.Matrix.Models;

namespace Shadowrun.Matrix.UI.Screens;

/// <summary>
/// Screen — Cyberdeck Stats.
/// 50/50 split: left shows all stat values; right shows a context-sensitive
/// description of the stat currently under the cursor.
/// Arrow keys / D-pad navigate selectable rows; [Backspace] goes back.
/// </summary>
public sealed class CyberdeckStatsScreen : IScreen
{
    private readonly Cyberdeck _deck;
    private int _cursor = 0;

    private record StatRow(string Label, Func<string> GetValue, string Description, bool Separator = false);

    private readonly StatRow[] _rows;

    // Indices of selectable rows (non-separator)
    private readonly int[] _selectable;

    public CyberdeckStatsScreen(Cyberdeck deck)
    {
        _deck = deck;
        var s = deck.Stats;

        _rows = new StatRow[]
        {
            new("MPCP",         () => s.Mpcp.ToString(),
                "Sets the ceiling for ALL upgradeable stats — no attribute or " +
                "hardware stat can exceed this value. The single most important " +
                "number on any cyberdeck."),
            new("Hardening",    () => s.Hardening.ToString(),
                "Intrinsic defensive toughness hardwired into the deck. Fixed at " +
                "purchase and cannot be upgraded. Reduces ICE attack damage " +
                "alongside the Bod attribute."),
            new("", () => "", "", Separator: true),
            new("Memory",       () => $"{deck.UsedMemory()} / {s.Memory} Mp",
                "Loaded-program capacity in Megapulses. All programs in active " +
                "slots consume memory. Total in-use memory cannot exceed this " +
                "value during a run."),
            new("Storage",      () => $"{deck.UsedStorage()} / {s.Storage} Mp",
                "Total capacity for installed programs and downloaded data files. " +
                "Programs consume storage permanently; sell or delete data files " +
                "to reclaim space."),
            new("Load I/O Spd", () => $"{s.LoadIoSpeed} / {s.LoadIoSpeedMax}",
                "How quickly programs reload into memory during a run. Only " +
                "applies to mid-run reloading — programs loaded before jack-in " +
                "start fully loaded regardless of this value."),
            new("", () => "", "", Separator: true),
            new("Response",     () => s.Response.ToString(),
                "Reduces player cooldown between program runs. Hard capped at 3. " +
                "The single most impactful upgrade for active combat speed — " +
                "each point is a significant difference."),
            new("", () => "", "", Separator: true),
            new("Bod",          () => s.Bod.ToString(),
                "Effective armor rating in the Matrix. Added to Hardening for " +
                "total damage reduction per ICE hit. Balances well with Evasion " +
                "for overall survivability."),
            new("Evasion",      () => s.Evasion.ToString(),
                "Chance to completely dodge ICE attacks before damage is rolled. " +
                "Higher values cause more ICE attacks to miss entirely. " +
                "Pairs well with Bod."),
            new("Masking",      () => s.Masking.ToString(),
                "Most important upgradeable attribute. Slows system alert " +
                "escalation and directly multiplies Deception program success " +
                "chance. Upgrade Masking first."),
            new("Sensor",       () => s.Sensor.ToString(),
                "Improves Analyze program success chance. Limited applicability " +
                "compared to other attributes. Upgrade only after Masking " +
                "is maxed."),
        };

        _selectable = _rows
            .Select((r, i) => (r, i))
            .Where(x => !x.r.Separator)
            .Select(x => x.i)
            .ToArray();
    }

    public void Render(int w, int h)
    {
        int inner  = w - 2;
        int leftW  = (inner - 1) / 2;
        int rightW = inner - 1 - leftW;

        // Description for currently selected row
        int selRowIdx  = _selectable[Math.Clamp(_cursor, 0, _selectable.Length - 1)];
        var descLines  = Wrap(_rows[selRowIdx].Description, rightW - 2);
        int totalRows  = Math.Max(_rows.Length, descLines.Count);

        RenderHelper.DrawWindowOpen("[Main Menu -> Cyberdeck -> Stats]", w);
        RenderHelper.DrawWindowCentredLine(_deck.Name, w);
        RenderHelper.DrawWindowDivider(w);

        for (int i = 0; i < totalRows; i++)
        {
            bool isSel    = i < _rows.Length && !_rows[i].Separator && _selectable[Math.Clamp(_cursor, 0, _selectable.Length - 1)] == i;
            bool isSep    = i < _rows.Length && _rows[i].Separator;

            // Left content
            string leftContent;
            if (i >= _rows.Length || isSep)
            {
                leftContent = new string(' ', leftW);
            }
            else
            {
                var row   = _rows[i];
                string ptr = isSel ? "\u25ba " : "  ";
                string lbl = $"{ptr}{row.Label}";
                string val = row.GetValue();
                // Label left-aligned, value right-aligned within leftW
                int    gap = Math.Max(1, leftW - lbl.Length - val.Length);
                string left = lbl + new string(' ', gap) + val;
                leftContent = left.Length >= leftW ? left[..leftW] : left.PadRight(leftW);
            }

            // Right content — only show description lines in the selected row's vertical span
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

            // Render the split line
            VC.Write("\u2551");
            if (isSel)
            {
                VC.BackgroundColor = ConsoleColor.DarkYellow;
                VC.ForegroundColor = ConsoleColor.Black;
            }
            else
            {
                VC.ForegroundColor = isSep ? ConsoleColor.DarkGray : ConsoleColor.Gray;
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

        RenderHelper.DrawWindowClose(w);
        VC.WriteLine();
        VC.WriteLine(("  [\u2191\u2193] Select   [Backspace] Back").PadRight(w));
    }

    public IScreen? HandleInput(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.Escape)    return NavigationToken.Back;
        if (key.Key == ConsoleKey.Backspace) return NavigationToken.Back;

        if (key.Key == ConsoleKey.UpArrow)
            { _cursor = Math.Max(0, _cursor - 1); return null; }
        if (key.Key == ConsoleKey.DownArrow)
            { _cursor = Math.Min(_selectable.Length - 1, _cursor + 1); return null; }

        return null;
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
