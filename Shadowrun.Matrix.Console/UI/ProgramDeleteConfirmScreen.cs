using Shadowrun.Matrix.Models;
using MatrixProgram = Shadowrun.Matrix.Models.Program;

namespace Shadowrun.Matrix.UI.Screens;

public sealed class ProgramDeleteConfirmScreen : MenuScreen
{
    private readonly Cyberdeck     _deck;
    private readonly MatrixProgram _program;

    public ProgramDeleteConfirmScreen(Cyberdeck deck, MatrixProgram program)
    {
        _deck    = deck;
        _program = program;
        SetSelectedIndex(1);
    }

    protected override int GetItemCount() => 2;
    protected override IScreen? OnItemConfirmed(int index)
    {
        if (index == 0)
        {
            try { _deck.DeleteProgram(_program); return NavigationToken.BackTwo; }
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
        int inner   = w - 2;
        int nameMax = Math.Max(1, inner - 34); // "  Delete program: " = ~20 + some margin
        string name = RenderHelper.Truncate(_program.Spec.Name.ToString(), nameMax);

        RenderHelper.DrawWindowOpen("[Delete Program]", w);
        RenderHelper.DrawWindowCentredLine($"Delete  '{name}'  from the cyberdeck?", w);
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
