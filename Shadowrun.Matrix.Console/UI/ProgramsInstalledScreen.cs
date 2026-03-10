using Shadowrun.Matrix.Models;

namespace Shadowrun.Matrix.UI.Screens;

public sealed class ProgramsInstalledScreen : MenuScreen
{
    private readonly Cyberdeck _deck;
    private readonly bool      _midSession;

    public ProgramsInstalledScreen(Cyberdeck deck, bool midSession = false)
    {
        _deck       = deck;
        _midSession = midSession;
    }

    protected override int GetItemCount() => _deck.Programs.Count;
    protected override IScreen? OnItemConfirmed(int index) =>
        index >= 0 && index < _deck.Programs.Count
            ? new ProgramInstalledActionScreen(_deck, _deck.Programs[index], _midSession) : null;

    public override void Render(int w, int h)
    {
        // Fixed overhead: WindowOpen(3) + centred name(1) + divider(1)
        //                 + scroll-up(1) + scroll-dn(1) + WindowClose(1)
        //                 + blank(1) + prompt(1) = 10
        int visibleRows  = Math.Max(1, h - 10);
        bool canScrollUp = ScrollOffset > 0;
        bool canScrollDn = ScrollOffset + visibleRows < _deck.Programs.Count;

        RenderHelper.DrawWindowOpen("[Main Menu -> Cyberdeck -> Programs -> Installed]", w);
        RenderHelper.DrawWindowCentredLine(_deck.Name, w);
        RenderHelper.DrawWindowDivider(w);

        if (_deck.Programs.Count == 0)
        {
            RenderHelper.DrawWindowCentredLine("No programs installed.", w);
            RenderHelper.DrawWindowClose(w);
            VC.WriteLine();
            VC.WriteLine("  Selection:".PadRight(w));
            return;
        }

        RenderHelper.DrawWindowScrollUp(canScrollUp, w);
        for (int i = ScrollOffset; i < Math.Min(_deck.Programs.Count, ScrollOffset + visibleRows); i++)
        {
            var p = _deck.Programs[i];
            RenderHelper.DrawWindowMenuItem(i + 1, $"{p.Spec.Name} (Lvl {p.Spec.Level})",
                "sub menu", SelectedIndex == i, w);
        }
        RenderHelper.DrawWindowScrollDown(canScrollDn, w);
        RenderHelper.DrawWindowClose(w);
        VC.WriteLine();
        VC.WriteLine("  Selection:".PadRight(w));
        if (PendingError is not null) { RenderHelper.DrawErrorLine(PendingError, w); PendingError = null; }
    }
}
