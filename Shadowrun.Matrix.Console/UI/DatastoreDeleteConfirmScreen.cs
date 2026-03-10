using Shadowrun.Matrix.Models;
using Shadowrun.Matrix.ValueObjects;

namespace Shadowrun.Matrix.UI.Screens;

/// <summary>Screen 14 — Datastore delete confirmation.</summary>
public sealed class DatastoreDeleteConfirmScreen : MenuScreen
{
    private readonly Cyberdeck _deck;
    private readonly DataFile  _file;
    private readonly int       _displayIndex;

    public DatastoreDeleteConfirmScreen(Cyberdeck deck, DataFile file, int displayIndex)
    {
        _deck         = deck;
        _file         = file;
        _displayIndex = displayIndex;
        SetSelectedIndex(1); // default to No
    }

    protected override int GetItemCount() => 2;

    protected override IScreen? OnItemConfirmed(int index)
    {
        if (index == 0)
        {
            try { _deck.RemoveDataFile(_file); return NavigationToken.BackTwo; }
            catch (Exception ex) { PendingError = ex.Message; return null; }
        }
        return NavigationToken.Back;
    }

    public override IScreen? HandleInput(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.Escape)    return NavigationToken.Back;
        if (key.Key == ConsoleKey.Backspace) return NavigationToken.Back;
        if (key.KeyChar is 'y' or 'Y') return OnItemConfirmed(0);
        if (key.KeyChar is 'n' or 'N') return NavigationToken.Back;
        return base.HandleInput(key);
    }

    public override void Render(int w, int h)
    {
        int    nameMax = Math.Max(1, (w - 2) - 14); // leave room for "Delete  '' ?"
        string name    = RenderHelper.Truncate(_file.Name, nameMax);

        RenderHelper.DrawWindowOpen("[Delete Data File]", w);
        RenderHelper.DrawWindowCentredLine($"Delete  '{name}'  from the datastore?", w);
        RenderHelper.DrawWindowCentredLine("This action cannot be undone!", w);
        RenderHelper.DrawWindowDivider(w);
        RenderHelper.DrawWindowMenuItem(1, "Yes — delete permanently", null, SelectedIndex == 0, w);
        RenderHelper.DrawWindowMenuItem(2, "No  — cancel",             null, SelectedIndex == 1, w);
        RenderHelper.DrawWindowClose(w);

        VC.WriteLine();
        VC.WriteLine("  Selection:".PadRight(w));
        if (PendingError is not null) { RenderHelper.DrawErrorLine(PendingError, w); PendingError = null; }
    }
}
