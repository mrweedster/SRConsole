using Shadowrun.Matrix.Models;
using MatrixProgram = Shadowrun.Matrix.Models.Program;

namespace Shadowrun.Matrix.UI.Screens;

/// <summary>Screen 11 — Programs → Loaded.</summary>
public sealed class ProgramsLoadedScreen : MenuScreen
{
    private readonly Cyberdeck _deck;
    public ProgramsLoadedScreen(Cyberdeck deck) { _deck = deck; }

    private List<(MatrixProgram Program, int SlotIndex)> GetLoaded() =>
        _deck.LoadedSlots
             .Select((p, i) => (Program: p, SlotIndex: i))
             .Where(t => t.Program is not null)
             .Select(t => (t.Program!, t.SlotIndex))
             .ToList();

    protected override int GetItemCount() => GetLoaded().Count;

    protected override IScreen? OnItemConfirmed(int index)
    {
        var loaded = GetLoaded();
        if (index < 0 || index >= loaded.Count) return null;
        var (program, slotIndex) = loaded[index];
        return new ProgramsLoadedSubmenuScreen(_deck, program, slotIndex, index + 1);
    }

    public override void Render(int w, int h)
    {
        var loaded = GetLoaded();
        // Overhead: WindowOpen(3) + centred name(1) + divider(1)
        //           + scroll-up(1) + scroll-dn(1) + WindowClose(1) + blank(1) + prompt(1) = 10
        int  visibleRows  = Math.Max(1, h - 10);
        bool canScrollUp  = ScrollOffset > 0;
        bool canScrollDn  = ScrollOffset + visibleRows < loaded.Count;

        RenderHelper.DrawWindowOpen("[Main Menu -> Cyberdeck -> Programs -> Loaded]", w);
        RenderHelper.DrawWindowCentredLine(_deck.Name, w);
        RenderHelper.DrawWindowDivider(w);

        if (loaded.Count == 0)
        {
            RenderHelper.DrawWindowCentredLine("No programs loaded.", w);
            RenderHelper.DrawWindowClose(w);
            VC.WriteLine();
            VC.WriteLine("  Selection:".PadRight(w));
            return;
        }

        RenderHelper.DrawWindowScrollUp(canScrollUp, w);
        for (int i = ScrollOffset; i < Math.Min(loaded.Count, ScrollOffset + visibleRows); i++)
        {
            var p = loaded[i].Program;
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
