using Shadowrun.Matrix.Models;
using Shadowrun.Matrix.ValueObjects;

namespace Shadowrun.Matrix.UI.Screens;

/// <summary>Screen 13 — Cyberdeck → Datastore. Scrollable data-file list.</summary>
public sealed class DatastoreScreen : MenuScreen
{
    private readonly Cyberdeck _deck;
    public DatastoreScreen(Cyberdeck deck) { _deck = deck; }

    protected override int GetItemCount() => _deck.DataFiles.Count;

    protected override IScreen? OnItemConfirmed(int index) =>
        index >= 0 && index < _deck.DataFiles.Count
            ? new DatastoreDeleteConfirmScreen(_deck, _deck.DataFiles[index], index + 1)
            : null;

    public override void Render(int w, int h)
    {
        // Overhead: WindowOpen(3) + centred name(1) + divider(1)
        //           + scroll-up(1) + scroll-dn(1) + WindowClose(1) + blank(1) + prompt(1) = 10
        int  visibleRows  = Math.Max(1, h - 10);
        bool canScrollUp  = ScrollOffset > 0;
        bool canScrollDn  = ScrollOffset + visibleRows < _deck.DataFiles.Count;

        RenderHelper.DrawWindowOpen("[Main Menu -> Cyberdeck -> Datastore]", w);
        RenderHelper.DrawWindowCentredLine(_deck.Name, w);
        RenderHelper.DrawWindowDivider(w);

        if (_deck.DataFiles.Count == 0)
        {
            RenderHelper.DrawWindowCentredLine("Datastore empty.", w);
            RenderHelper.DrawWindowClose(w);
            VC.WriteLine();
            VC.WriteLine("  Selection:".PadRight(w));
            return;
        }

        RenderHelper.DrawWindowScrollUp(canScrollUp, w);
        for (int i = ScrollOffset; i < Math.Min(_deck.DataFiles.Count, ScrollOffset + visibleRows); i++)
        {
            DataFile f   = _deck.DataFiles[i];
            string   lbl = $"{f.Name} — {f.NuyenValue}\u00a5 ({f.SizeInMp}Mp)";
            RenderHelper.DrawWindowMenuItem(i + 1, lbl, "delete", SelectedIndex == i, w);
        }
        RenderHelper.DrawWindowScrollDown(canScrollDn, w);
        RenderHelper.DrawWindowClose(w);

        VC.WriteLine();
        VC.WriteLine("  Selection:".PadRight(w));
        if (PendingError is not null) { RenderHelper.DrawErrorLine(PendingError, w); PendingError = null; }
    }
}
