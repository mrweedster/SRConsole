using Shadowrun.Matrix.Models;

namespace Shadowrun.Matrix.UI.Screens;

public sealed class ProgramsInstalledSubmenuScreen : MenuScreen
{
    private readonly Cyberdeck _deck;
    private readonly int       _parentDisplayIndex;

    public ProgramsInstalledSubmenuScreen(Cyberdeck deck, int parentDisplayIndex)
    {
        _deck               = deck;
        _parentDisplayIndex = parentDisplayIndex;
    }

    protected override int GetItemCount() => _deck.Programs.Count;
    protected override IScreen? OnItemConfirmed(int index) =>
        index >= 0 && index < _deck.Programs.Count
            ? new ProgramDeleteConfirmScreen(_deck, _deck.Programs[index]) : null;

    public override void Render(int w, int h)
    {
        int visibleRows  = Math.Max(1, h - 10);
        bool canScrollUp = ScrollOffset > 0;
        bool canScrollDn = ScrollOffset + visibleRows < _deck.Programs.Count;

        RenderHelper.DrawWindowOpen(
            $"[Installed -> [{_parentDisplayIndex}] — Select program to delete]", w);
        RenderHelper.DrawWindowCentredLine(_deck.Name, w);
        RenderHelper.DrawWindowDivider(w);

        if (_deck.Programs.Count == 0)
        {
            RenderHelper.DrawWindowCentredLine("No programs installed.", w);
            RenderHelper.DrawWindowClose(w);
            VC.WriteLine();
            VC.WriteLine("  Delete Program Selection:".PadRight(w));
            return;
        }

        RenderHelper.DrawWindowScrollUp(canScrollUp, w);
        for (int i = ScrollOffset; i < Math.Min(_deck.Programs.Count, ScrollOffset + visibleRows); i++)
        {
            var p = _deck.Programs[i];
            RenderHelper.DrawWindowMenuItem(i + 1, $"{p.Spec.Name} (Lvl {p.Spec.Level})",
                "delete", SelectedIndex == i, w);
        }
        RenderHelper.DrawWindowScrollDown(canScrollDn, w);
        RenderHelper.DrawWindowClose(w);
        VC.WriteLine();
        VC.WriteLine("  Delete Program Selection:".PadRight(w));
        if (PendingError is not null) { RenderHelper.DrawErrorLine(PendingError, w); PendingError = null; }
    }
}
