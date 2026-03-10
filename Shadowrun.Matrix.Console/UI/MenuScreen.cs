namespace Shadowrun.Matrix.UI;

/// <summary>
/// Abstract base class for all screens that present a numbered, selectable list
/// of items.
///
/// <para>Handles:</para>
/// <list type="bullet">
///   <item>Arrow Up / Down cursor movement with wrap-around.</item>
///   <item>Alphanumeric shortcut keys (1-based) for direct jump + confirm.</item>
///   <item>Enter to confirm the currently highlighted item.</item>
///   <item>Backspace → <see cref="NavigationToken.Back"/>.</item>
///   <item>Escape   → <see cref="NavigationToken.Back"/>.</item>
///   <item>Vertical scroll offset tracking when the list is taller than the viewport.</item>
/// </list>
///
/// <para>Subclasses must implement <see cref="GetItemCount"/>,
/// <see cref="OnItemConfirmed"/>, and <see cref="Render"/>.</para>
/// </summary>
public abstract class MenuScreen : IScreen
{
    // ── Cursor / scroll state ─────────────────────────────────────────────────

    /// <summary>Zero-based index of the currently highlighted item.</summary>
    protected int SelectedIndex { get; private set; }

    /// <summary>Zero-based index of the first visible item when the list is scrolled.</summary>
    protected int ScrollOffset  { get; private set; }

    // ── Pending error message ─────────────────────────────────────────────────

    /// <summary>
    /// Set this to a non-null string to display a one-line error at the bottom
    /// of the screen on the next render cycle. Automatically cleared after one render.
    /// </summary>
    protected string? PendingError { get; set; }

    // ── Abstract contract ─────────────────────────────────────────────────────

    /// <summary>Total number of selectable items in this screen's list.</summary>
    protected abstract int GetItemCount();

    /// <summary>
    /// Called when the user confirms the item at <paramref name="index"/> (zero-based).
    /// Return the same values as <see cref="IScreen.HandleInput"/>:
    /// null = stay, a new screen = push it, NavigationToken.Back = pop, NavigationToken.Root = reset.
    /// </summary>
    protected abstract IScreen? OnItemConfirmed(int index);

    /// <inheritdoc/>
    public abstract void Render(int consoleWidth, int consoleHeight);

    // ── Input handling ────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public virtual IScreen? HandleInput(ConsoleKeyInfo key)
    {
        int count = GetItemCount();

        // ── Global navigation keys ────────────────────────────────────────────
        if (key.Key == ConsoleKey.Escape)
            return NavigationToken.Back;

        if (key.Key == ConsoleKey.Backspace)
            return NavigationToken.Back;

        if (count == 0)
            return null;

        // ── Cursor movement ───────────────────────────────────────────────────
        if (key.Key == ConsoleKey.UpArrow)
        {
            SelectedIndex = (SelectedIndex - 1 + count) % count;
            ClampScroll(count);
            return null;
        }

        if (key.Key == ConsoleKey.DownArrow)
        {
            SelectedIndex = (SelectedIndex + 1) % count;
            ClampScroll(count);
            return null;
        }

        // ── Enter confirms current selection ──────────────────────────────────
        if (key.Key == ConsoleKey.Enter)
            return OnItemConfirmed(SelectedIndex);

        // ── Alphanumeric shortcut (1-based display index) ─────────────────────
        if (char.IsLetterOrDigit(key.KeyChar))
        {
            // Support single-digit 1–9
            if (int.TryParse(key.KeyChar.ToString(), out int displayIndex))
            {
                int zeroIndex = displayIndex - 1;
                if (zeroIndex >= 0 && zeroIndex < count)
                {
                    SelectedIndex = zeroIndex;
                    ClampScroll(count);
                    return OnItemConfirmed(zeroIndex);
                }
            }
        }

        return null;
    }

    // ── Scroll helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Call this after changing <see cref="SelectedIndex"/> to keep the scroll
    /// window centred around the cursor.
    /// </summary>
    /// <param name="count">Total item count.</param>
    /// <param name="visibleRows">
    /// How many rows are visible. Pass 0 to use the current terminal height minus a
    /// reasonable header overhead (8 lines).
    /// </param>
    protected void ClampScroll(int count, int visibleRows = 0)
    {
        if (visibleRows <= 0)
            visibleRows = Math.Max(1, VC.WindowHeight - 10);

        // Scroll down to keep selected item in view
        if (SelectedIndex >= ScrollOffset + visibleRows)
            ScrollOffset = SelectedIndex - visibleRows + 1;

        // Scroll up to keep selected item in view
        if (SelectedIndex < ScrollOffset)
            ScrollOffset = SelectedIndex;

        // Clamp offset
        ScrollOffset = Math.Max(0, Math.Min(ScrollOffset, count - visibleRows));
        ScrollOffset = Math.Max(0, ScrollOffset);
    }

    /// <summary>
    /// Resets cursor and scroll to the top (useful when the list data changes).
    /// </summary>
    protected void ResetCursor()
    {
        SelectedIndex = 0;
        ScrollOffset  = 0;
    }

    /// <summary>
    /// Sets the initial cursor position (zero-based). Use in constructors to
    /// override the default of 0, e.g. to default a confirm screen to "No".
    /// </summary>
    protected void SetSelectedIndex(int index)
    {
        SelectedIndex = index;
    }
}
