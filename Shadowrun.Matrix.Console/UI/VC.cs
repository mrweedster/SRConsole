namespace Shadowrun.Matrix.UI;

/// <summary>
/// Virtual console frame buffer.
/// Screens write to VC instead of directly to System.Console.
/// EndFrame() diffs the new buffer against the previous frame and only
/// writes changed cells — eliminating flicker from Console.Clear().
/// </summary>
public static class VC
{
    private readonly record struct Cell(char Ch, ConsoleColor Fg, ConsoleColor Bg)
    {
        public static readonly Cell Blank = new(' ', ConsoleColor.Gray, ConsoleColor.Black);
    }

    private static Cell[,] _cur  = new Cell[1, 1];
    private static Cell[,] _prev = new Cell[1, 1];
    private static int _bW, _bH;
    private static int _cx, _cy;
    private static ConsoleColor _fg = ConsoleColor.Gray;
    private static ConsoleColor _bg = ConsoleColor.Black;
    private static bool _firstFrame = true;

    // ── Frame lifecycle ───────────────────────────────────────────────────────

    public static void BeginFrame(int w, int h)
    {
        if (w != _bW || h != _bH)
        {
            _bW = w; _bH = h;
            _cur  = new Cell[h, w];
            _prev = new Cell[h, w];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                _prev[y, x] = new Cell('\0', ConsoleColor.Gray, ConsoleColor.Black);
        }
        for (int y = 0; y < _bH; y++)
        for (int x = 0; x < _bW; x++)
            _cur[y, x] = Cell.Blank;
        _cx = 0; _cy = 0;
        _fg = ConsoleColor.Gray;
        _bg = ConsoleColor.Black;
    }

    public static void EndFrame()
    {
        if (_firstFrame)
        {
            Console.BackgroundColor = ConsoleColor.Black;
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Clear();
            _firstFrame = false;
        }

        Console.CursorVisible = false;

        ConsoleColor activeFg = ConsoleColor.Gray;
        ConsoleColor activeBg = ConsoleColor.Black;

        // Seed the real console to our known baseline before writing any cells,
        // so the terminal theme colour never bleeds through.
        Console.ForegroundColor = activeFg;
        Console.BackgroundColor = activeBg;

        for (int y = 0; y < _bH; y++)
        {
            int first = -1, last = -1;
            for (int x = 0; x < _bW; x++)
            {
                // Unchanged shadow pairs are never dirty.
                if (_cur[y, x].Ch == '\0' && _prev[y, x].Ch == '\0') continue;
                if (_cur[y, x] != _prev[y, x])
                {
                    // If the dirty cell is a shadow ('\0'), pull the range left to
                    // include its parent wide char so the wide char gets redrawn —
                    // its second display column will then naturally overwrite any ghost.
                    int markX = (_cur[y, x].Ch == '\0' && x > 0) ? x - 1 : x;
                    if (first < 0) first = markX;
                    else           first = Math.Min(first, markX);
                    last = Math.Max(last, x);
                }
            }
            if (first < 0) continue;

            Console.SetCursorPosition(first, y);

            int consoleX = first; // tracks where the real console cursor is
            for (int x = first; x <= last; x++)
            {
                var cell = _cur[y, x];
                // Shadow cell ('\0') — the parent wide char already covered this
                // display column.  Skip without writing or repositioning.
                if (cell.Ch == '\0') continue;

                // Reposition if the console cursor drifted (e.g. after a wide char
                // advanced by 2 but we skipped the shadow cell above).
                if (consoleX != x) Console.SetCursorPosition(x, y);

                if (cell.Fg != activeFg) { Console.ForegroundColor = cell.Fg; activeFg = cell.Fg; }
                if (cell.Bg != activeBg) { Console.BackgroundColor = cell.Bg; activeBg = cell.Bg; }
                Console.Write(cell.Ch);
                consoleX = x + CharDisplayWidth(cell.Ch);
            }
        }

        // Always restore to our fixed baseline — never let the terminal theme bleed in.
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.BackgroundColor = ConsoleColor.Black;
        (_prev, _cur) = (_cur, _prev);
    }

    // ── Write API (called by screens) ─────────────────────────────────────────

    public static ConsoleColor ForegroundColor
    {
        get => _fg;
        set => _fg = value;
    }

    public static ConsoleColor BackgroundColor
    {
        get => _bg;
        set => _bg = value;
    }

    public static void ResetColor()
    {
        _fg = ConsoleColor.Gray;
        _bg = ConsoleColor.Black;
    }

    public static void SetCursorPosition(int x, int y)
    {
        _cx = Math.Max(0, x);
        _cy = Math.Max(0, y);
    }

    public static void Write(string? s)
    {
        if (s is null) return;
        foreach (char c in s) PutChar(c);
    }

    public static void Write(char c) => PutChar(c);

    public static void WriteLine(string? s = null)
    {
        if (s is not null) Write(s);
        PutChar('\n');
    }

    public static void Write(int v)     => Write(v.ToString());
    public static void Write(float v)   => Write(v.ToString());
    public static void Write(double v)  => Write(v.ToString());
    public static void Write(object? v) => Write(v?.ToString());

    // ── Pass-throughs (non-render, always go to real Console) ─────────────────

    public static bool CursorVisible
    {
        get => Console.CursorVisible;
        set => Console.CursorVisible = value;
    }

    public static int  WindowWidth   => Console.WindowWidth;
    public static int  WindowHeight  => Console.WindowHeight;
    public static bool KeyAvailable  => Console.KeyAvailable;

    public static ConsoleKeyInfo ReadKey(bool intercept = false)
        => Console.ReadKey(intercept);

    // No-op: BeginFrame handles buffer clearing
    public static void Clear() { }

    // ── Private ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the number of terminal columns a character occupies.
    /// Most printable ASCII = 1.  Unicode "Ambiguous/Wide" symbols used in this
    /// game (Geometric Shapes, Misc Symbols, Dingbats, Emoji) = 2.
    /// Box-drawing and Block-element ranges are always 1.
    /// </summary>
    private static int CharDisplayWidth(char ch)
    {
        if (ch < 0x80)  return 1;                   // plain ASCII
        if (ch >= 0x2500 && ch <= 0x259F) return 1; // Box-drawing + Block elements (always 1-wide)
        if (ch >= 0x25A0 && ch <= 0x25FF) return 1; // Geometric Shapes (△□○◇▶◦) — 1-wide in Western terminals
        if (ch >= 0x2600 && ch <= 0x27BF) return 2; // Misc Symbols & Dingbats ⚡☠⚔⚠★⛁ — 2-wide
        if (ch >= 0xFF01 && ch <= 0xFF60) return 2; // Fullwidth forms
        if (ch >= 0xD800)                 return 2; // Surrogates / Emoji
        return 1;
    }

    private static void PutChar(char c)
    {
        if (c == '\n') { _cy++; _cx = 0; return; }
        if (c == '\r') return;

        int dw = CharDisplayWidth(c);

        if (_cx >= 0 && _cx < _bW && _cy >= 0 && _cy < _bH)
        {
            _cur[_cy, _cx] = new Cell(c, _fg, _bg);
            // For 2-column-wide chars mark the shadow cell as '\0' so EndFrame
            // knows to skip it (the wide char already covers that display column).
            if (dw == 2 && _cx + 1 < _bW)
                _cur[_cy, _cx + 1] = new Cell('\0', _fg, _bg);
        }
        _cx += dw;
    }
}
