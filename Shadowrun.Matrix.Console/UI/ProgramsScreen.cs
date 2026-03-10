using Shadowrun.Matrix.Models;

namespace Shadowrun.Matrix.UI.Screens;

public sealed class ProgramsScreen : MenuScreen
{
    private readonly Cyberdeck _deck;
    private readonly bool      _midSession;

    public ProgramsScreen(Cyberdeck deck, bool midSession = false)
    {
        _deck       = deck;
        _midSession = midSession;
    }

    protected override int GetItemCount() => 2;
    protected override IScreen? OnItemConfirmed(int index) => index switch
    {
        0 => new ProgramsInstalledScreen(_deck, _midSession),
        1 => new ProgramsLoadedScreen(_deck),
        _ => null
    };

    public override void Render(int w, int h)
    {
        RenderHelper.DrawWindowOpen("[Main Menu -> Cyberdeck -> Programs]", w);
        RenderHelper.DrawWindowCentredLine(_deck.Name, w);
        RenderHelper.DrawWindowDivider(w);
        RenderHelper.DrawWindowMenuItem(1, "INSTALLED", "sub menu", SelectedIndex == 0, w);
        RenderHelper.DrawWindowMenuItem(2, "LOADED",    "sub menu", SelectedIndex == 1, w);
        RenderHelper.DrawWindowClose(w);
        VC.WriteLine();
        VC.WriteLine("  Selection:".PadRight(w));
        if (PendingError is not null) { RenderHelper.DrawErrorLine(PendingError, w); PendingError = null; }
    }
}
