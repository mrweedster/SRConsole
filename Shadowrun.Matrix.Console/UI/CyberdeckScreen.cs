using Shadowrun.Matrix.Models;

namespace Shadowrun.Matrix.UI.Screens;

public sealed class CyberdeckScreen : MenuScreen
{
    private readonly Cyberdeck _deck;
    private readonly bool      _midSession;

    public CyberdeckScreen(Cyberdeck deck, bool midSession = false)
    {
        _deck       = deck;
        _midSession = midSession;
    }

    protected override int GetItemCount() => 3;
    protected override IScreen? OnItemConfirmed(int index) => index switch
    {
        0 => new CyberdeckStatsScreen(_deck),
        1 => new ProgramsScreen(_deck, _midSession),
        2 => new DatastoreScreen(_deck),
        _ => null
    };

    public override void Render(int w, int h)
    {
        RenderHelper.DrawWindowOpen("[Main Menu -> Cyberdeck]", w);
        RenderHelper.DrawWindowCentredLine(_deck.Name, w);
        RenderHelper.DrawWindowDivider(w);
        RenderHelper.DrawWindowMenuItem(1, "STATS",     "sub menu", SelectedIndex == 0, w);
        RenderHelper.DrawWindowMenuItem(2, "PROGRAMS",  "sub menu", SelectedIndex == 1, w);
        RenderHelper.DrawWindowMenuItem(3, "DATASTORE", "sub menu", SelectedIndex == 2, w);
        RenderHelper.DrawWindowClose(w);
        VC.WriteLine();
        VC.WriteLine("  Selection:".PadRight(w));
        if (PendingError is not null) { RenderHelper.DrawErrorLine(PendingError, w); PendingError = null; }
    }
}
