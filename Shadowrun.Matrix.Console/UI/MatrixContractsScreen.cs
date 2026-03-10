using Shadowrun.Matrix.Models;

namespace Shadowrun.Matrix.UI.Screens;

/// <summary>Screen 2 — Matrix Contracts. Scrollable list of available runs.</summary>
public sealed class MatrixContractsScreen : MenuScreen
{
    private readonly IReadOnlyList<MatrixRunEntry> _entries;
    private readonly Func<MatrixRun, IScreen?>?    _onAccepted;

    public MatrixContractsScreen(
        IReadOnlyList<MatrixRunEntry> entries,
        Func<MatrixRun, IScreen?>?    onAccepted = null)
    {
        _entries    = entries;
        _onAccepted = onAccepted;
    }

    protected override int GetItemCount() => _entries.Count;

    protected override IScreen? OnItemConfirmed(int index) =>
        index >= 0 && index < _entries.Count
            ? new MatrixContractSubmenuScreen(_entries[index], index + 1, _onAccepted)
            : null;

    public override void Render(int w, int h)
    {
        // Overhead: WindowOpen(3) + divider(1) + scroll-up(1)
        //           + scroll-dn(1) + WindowClose(1) + blank(1) + prompt(1) = 9
        int  visibleRows  = Math.Max(1, h - 9);
        bool canScrollUp  = ScrollOffset > 0;
        bool canScrollDn  = ScrollOffset + visibleRows < _entries.Count;

        RenderHelper.DrawWindowOpen("[Main Menu -> Matrix Contracts]", w);
        RenderHelper.DrawWindowDivider(w);

        if (_entries.Count == 0)
        {
            RenderHelper.DrawWindowCentredLine("No contracts available.", w);
            RenderHelper.DrawWindowClose(w);
            VC.WriteLine();
            VC.WriteLine("  Enter contract:".PadRight(w));
            return;
        }

        RenderHelper.DrawWindowScrollUp(canScrollUp, w);
        for (int i = ScrollOffset; i < Math.Min(_entries.Count, ScrollOffset + visibleRows); i++)
        {
            var    e        = _entries[i];
            string overview = $"{e.SystemName} — {e.Run.TargetNodeTitle}";
            RenderHelper.DrawWindowContractItem(i + 1, e.Run.Difficulty, overview, SelectedIndex == i, w);
        }
        RenderHelper.DrawWindowScrollDown(canScrollDn, w);
        RenderHelper.DrawWindowClose(w);

        VC.WriteLine();
        VC.WriteLine("  Enter contract:".PadRight(w));
        if (PendingError is not null) { RenderHelper.DrawErrorLine(PendingError, w); PendingError = null; }
    }
}
