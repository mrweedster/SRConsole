namespace Shadowrun.Matrix.UI;

/// <summary>
/// Static helpers for console rendering.
///
/// <para><b>Window model:</b> all sub-screens are drawn as a single unified
/// window using ╔/╠/╚/═/║/╗/╝ characters.
/// <see cref="DrawWindowOpen"/> draws the title bar and leaves the window open;
/// <see cref="DrawWindowClose"/> closes it. Content lines, stat lines, menu
/// items, and scroll indicators are drawn with matching Window* helpers so
/// everything stays inside the same bordered frame.</para>
///
/// <para><b>Column layout</b> (inside the window):
/// label starts at a 2-space indent; label field is capped at ~35 % of the
/// inner width (max 28 chars); value follows 2 spaces later.
/// At 80-char terminal the value starts around column 30 — compact and
/// centered rather than spread edge-to-edge.</para>
/// </summary>
public static class RenderHelper
{
    // ── Column helpers ────────────────────────────────────────────────────────

    /// <summary>Label field width inside a window (indent included).</summary>
    private static int LabelW(int inner) =>
        Math.Clamp(inner * 35 / 100, 16, 28);

    // ── Window drawing ────────────────────────────────────────────────────────

    /// <summary>
    /// Opens a window: draws the top border, centred title, then the first
    /// ╠══╣ divider. The window remains open until <see cref="DrawWindowClose"/>.
    /// <code>
    /// ╔══════════════╗
    /// ║    Title     ║
    /// ╠══════════════╣
    /// </code>
    /// </summary>
    public static void DrawWindowOpen(string title, int width)
    {
        int    inner  = width - 2;
        string border = new('═', inner);
        VC.WriteLine($"╔{border}╗");
        VC.WriteLine($"║{Centre(Truncate(title, inner), inner)}║");
        VC.WriteLine($"╠{border}╣");
    }

    /// <summary>Internal horizontal divider: ╠══╣</summary>
    public static void DrawWindowDivider(int width)
    {
        int inner = width - 2;
        VC.WriteLine($"╠{new string('═', inner)}╣");
    }

    /// <summary>Closes the window: ╚══╝</summary>
    public static void DrawWindowClose(int width)
    {
        int inner = width - 2;
        VC.WriteLine($"╚{new string('═', inner)}╝");
    }

    /// <summary>A blank line inside the window: ║     ║</summary>
    public static void DrawWindowBlankLine(int width)
    {
        WriteWindowLine(new string(' ', width - 2), highlighted: false);
    }

    /// <summary>
    /// A plain centred text line inside the window, e.g. the deck name.
    /// </summary>
    public static void DrawWindowCentredLine(string text, int width)
    {
        int    inner   = width - 2;
        string content = Centre(Truncate(text, inner), inner);
        WriteWindowLine(content, highlighted: false);
    }

    /// <summary>
    /// A two-column stat line inside the window.
    /// Label is left-aligned; value follows after the label field.
    /// </summary>
    public static void DrawWindowStatLine(string label, string value, int width)
    {
        int inner  = width - 2;
        int labelW = LabelW(inner);

        // "  Label" padded to labelW, then 2-space gap, then value
        string prefix = Truncate($"  {label}", labelW).PadRight(labelW);
        string gap    = "  ";
        string line   = prefix + gap + value;

        if (line.Length > inner) line = Truncate(line, inner);
        WriteWindowLine(line.PadRight(inner), highlighted: false);
    }

    /// <summary>
    /// A word-wrapped text block inside the window. Each wrapped line is
    /// output as a full window row (║ ... ║). Uses a 2-space indent.
    /// </summary>
    public static void DrawWindowWrappedText(string text, int width, int indent = 2)
    {
        int    inner      = width - 2;
        int    maxLine    = Math.Max(4, inner - indent);
        string indentStr  = new(' ', indent);
        string[] words    = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var sb = new System.Text.StringBuilder();

        foreach (string word in words)
        {
            string candidate = sb.Length == 0 ? word : sb + " " + word;
            if (candidate.Length <= maxLine)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(word);
            }
            else
            {
                string lineContent = (indentStr + sb).PadRight(inner);
                WriteWindowLine(lineContent.Length > inner ? lineContent[..inner] : lineContent, false);
                sb.Clear();
                sb.Append(word);
            }
        }

        if (sb.Length > 0)
        {
            string lineContent = (indentStr + sb).PadRight(inner);
            WriteWindowLine(lineContent.Length > inner ? lineContent[..inner] : lineContent, false);
        }
    }

    /// <summary>
    /// A selectable menu item inside the window.
    /// Left: "  [N][Label]"; right: "[suffix]" anchored to 75% of inner width.
    /// </summary>
    public static void DrawWindowMenuItem(
        int index, string label, string? suffix, bool highlighted, int width)
    {
        int    inner      = width - 2;
        string left       = $"  [{index}][{label}]";
        string right      = suffix is not null ? $"[{suffix}]" : string.Empty;

        string content;
        if (right.Length == 0)
        {
            content = left.Length >= inner ? left[..inner] : left.PadRight(inner);
        }
        else
        {
            int col2   = Math.Min(inner - right.Length - 1, inner * 75 / 100);
            int gapLen = Math.Max(1, col2 - left.Length);
            string combined = left + new string(' ', gapLen) + right;
            if (combined.Length > inner) combined = Truncate(combined, inner);
            content = combined.PadRight(inner);
            if (content.Length > inner) content = content[..inner];
        }

        WriteWindowLine(content, highlighted);
    }

    /// <summary>Contract list item inside the window.</summary>
    public static void DrawWindowContractItem(
        int index, string difficulty, string overview, bool highlighted, int width)
    {
        int    inner = width - 2;
        string left  = $"  [{index}]  ({difficulty})  ";
        int    avail = Math.Max(0, inner - left.Length);
        string desc  = avail > 0 ? Truncate(overview, avail) : string.Empty;
        string content = (left + desc).PadRight(inner);
        if (content.Length > inner) content = content[..inner];
        WriteWindowLine(content, highlighted);
    }

    /// <summary>▲ scroll indicator inside the window (blank line if not shown).</summary>
    public static void DrawWindowScrollUp(bool show, int width)
    {
        int inner = width - 2;
        WriteWindowLine(show ? Centre("▲", inner) : new string(' ', inner), false);
    }

    /// <summary>▼ scroll indicator inside the window (blank line if not shown).</summary>
    public static void DrawWindowScrollDown(bool show, int width)
    {
        int inner = width - 2;
        WriteWindowLine(show ? Centre("▼", inner) : new string(' ', inner), false);
    }

    // ── Legacy / standalone helpers (kept for confirm boxes etc.) ─────────────

    /// <summary>
    /// Standalone three-line header box (used only where a full window is
    /// not appropriate, e.g. the main menu).
    /// </summary>
    public static void DrawBox(string title, int width)
    {
        int    inner  = width - 2;
        string border = new('═', inner);
        VC.WriteLine($"╔{border}╗");
        VC.WriteLine($"║{Centre(Truncate(title, inner), inner)}║");
        VC.WriteLine($"╚{border}╝");
    }

    // ── Outside-window helpers ────────────────────────────────────────────────

    /// <summary>Plain selectable item below a window: "  [N]  Label"</summary>
    public static void DrawPlainMenuItem(int index, string label, bool highlighted, int width)
    {
        string content = $"  [{index}]  {label}";
        string line    = content.Length >= width ? content[..width] : content.PadRight(width);
        WriteHighlighted(line, highlighted);
    }

    /// <summary>Error line below a window (red text).</summary>
    public static void DrawErrorLine(string message, int width)
    {
        var prev = VC.ForegroundColor;
        VC.ForegroundColor = ConsoleColor.Red;
        VC.WriteLine(Truncate($"  ! {message}", width).PadRight(width));
        VC.ForegroundColor = prev;
    }

    // ── Inline accept/decline helpers (MatrixContractSubmenuScreen) ───────────

    /// <summary>
    /// Writes a highlighted/normal choice token inline (no newline).
    /// Used by the contract Accept/Decline prompt which lives outside the window.
    /// </summary>
    public static void WriteInlineChoice(string text, bool highlighted)
    {
        if (highlighted)
        {
            var prevBg = VC.BackgroundColor;
            var prevFg = VC.ForegroundColor;
            VC.BackgroundColor = ConsoleColor.Green;
            VC.ForegroundColor = ConsoleColor.Black;
            VC.Write(text);
            VC.BackgroundColor = prevBg;
            VC.ForegroundColor = prevFg;
        }
        else
        {
            VC.Write(text);
        }
    }

    // ── String utilities (public — used by screens directly) ──────────────────

    public static string Truncate(string s, int maxLen)
    {
        if (maxLen <= 0)        return string.Empty;
        if (s.Length <= maxLen) return s;
        if (maxLen == 1)        return "…";
        return s[..(maxLen - 1)] + "…";
    }

    public static string Centre(string s, int totalWidth)
    {
        if (s.Length >= totalWidth) return Truncate(s, totalWidth);
        int padding = totalWidth - s.Length;
        int left    = padding / 2;
        int right   = padding - left;
        return new string(' ', left) + s + new string(' ', right);
    }

    // ── Visual (display) width helpers ──────────────────────────────────────────

    /// <summary>
    /// Returns the number of terminal columns a character occupies.
    /// Unicode "Ambiguous" and "Wide" characters (e.g. Misc Symbols ⚡☠⚔⚠★, some
    /// Geometric Shapes ▶□) render as 2 columns in most terminals.
    /// </summary>
    public static int VisualCharWidth(char ch)
    {
        // Must match VC.CharDisplayWidth exactly.
        if (ch >= 0x2500 && ch <= 0x259F) return 1; // Box-drawing + Block elements
        if (ch >= 0x25A0 && ch <= 0x25FF) return 1; // Geometric Shapes (△□○◇▶◦) — 1-wide
        if (ch >= 0x2600 && ch <= 0x27BF) return 2; // Misc Symbols & Dingbats ⚡☠⚔⚠★⛁
        if (ch >= 0xFF01 && ch <= 0xFF60) return 2; // Fullwidth forms
        if (ch >= 0x1F000)                return 2; // Emoji / supplementary
        return 1;
    }

    /// <summary>Returns the total display-column width of a string.</summary>
    public static int VisualWidth(string s) => s.Sum(VisualCharWidth);

    /// <summary>
    /// Pads <paramref name="s"/> with spaces on the right so its display width
    /// equals <paramref name="totalCols"/>.  Truncates if already wider.
    /// </summary>
    public static string VisualPadRight(string s, int totalCols)
    {
        int w = VisualWidth(s);
        if (w >= totalCols) return VisualTruncate(s, totalCols);
        return s + new string(' ', totalCols - w);
    }

    /// <summary>
    /// Truncates <paramref name="s"/> so its display width does not exceed
    /// <paramref name="maxCols"/>, appending "…" if truncation occurred.
    /// </summary>
    public static string VisualTruncate(string s, int maxCols)
    {
        if (maxCols <= 0) return string.Empty;
        int cols = 0;
        for (int i = 0; i < s.Length; i++)
        {
            int cw = VisualCharWidth(s[i]);
            if (cols + cw > maxCols)
                return i > 1 ? s[..(i - 1)] + "…" : "…";
            cols += cw;
        }
        return s; // already fits
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>Writes ║{content}║ with optional green highlight on the inner content.</summary>
    private static void WriteWindowLine(string inner, bool highlighted)
    {
        VC.Write("║");
        if (highlighted)
        {
            var prevBg = VC.BackgroundColor;
            var prevFg = VC.ForegroundColor;
            VC.BackgroundColor = ConsoleColor.Green;
            VC.ForegroundColor = ConsoleColor.Black;
            VC.Write(inner);
            VC.BackgroundColor = prevBg;
            VC.ForegroundColor = prevFg;
        }
        else
        {
            VC.Write(inner);
        }
        VC.WriteLine("║");
    }

    /// <summary>Writes a full-width line with optional green highlight (outside-window use).</summary>
    private static void WriteHighlighted(string line, bool highlighted)
    {
        if (highlighted)
        {
            var prevBg = VC.BackgroundColor;
            var prevFg = VC.ForegroundColor;
            VC.BackgroundColor = ConsoleColor.Green;
            VC.ForegroundColor = ConsoleColor.Black;
            VC.Write(line);
            VC.BackgroundColor = prevBg;
            VC.ForegroundColor = prevFg;
            VC.WriteLine();
        }
        else
        {
            VC.WriteLine(line);
        }
    }
}
