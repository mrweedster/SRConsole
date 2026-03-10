namespace Shadowrun.Matrix.UI.Screens;

/// <summary>Placeholder for the Enter the Matrix game flow.</summary>
public sealed class EnterMatrixStubScreen : IScreen
{
    public void Render(int w, int h)
    {
        RenderHelper.DrawWindowOpen("[Enter the Matrix]", w);
        RenderHelper.DrawWindowBlankLine(w);
        RenderHelper.DrawWindowCentredLine("NOT YET IMPLEMENTED", w);
        RenderHelper.DrawWindowBlankLine(w);
        RenderHelper.DrawWindowCentredLine("The Matrix awaits. Implementation pending.", w);
        RenderHelper.DrawWindowBlankLine(w);
        RenderHelper.DrawWindowClose(w);
        VC.WriteLine();
        VC.WriteLine("  [Backspace] Back".PadRight(w));
    }

    public IScreen? HandleInput(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.Escape)    return NavigationToken.Back;
        if (key.Key == ConsoleKey.Backspace) return NavigationToken.Back;
        return null;
    }
}
