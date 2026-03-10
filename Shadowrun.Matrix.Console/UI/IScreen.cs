namespace Shadowrun.Matrix.UI;

/// <summary>
/// Contract for every screen in the menu system.
///
/// <para><b>Render</b> is called once after every keypress (and on initial display).
/// It receives the current terminal dimensions so that all drawing scales
/// to the live window size.</para>
///
/// <para><b>HandleInput</b> processes a single keypress and returns a navigation
/// instruction:</para>
/// <list type="bullet">
///   <item><c>null</c>               — stay on this screen (re-render).</item>
///   <item>A new <see cref="IScreen"/> instance — push it onto the stack.</item>
///   <item><see cref="NavigationToken.Back"/> — pop this screen (go up one level).</item>
///   <item><see cref="NavigationToken.Root"/> — clear the stack, return to Main Menu.</item>
/// </list>
/// </summary>
public interface IScreen
{
    void    Render(int consoleWidth, int consoleHeight);
    IScreen? HandleInput(ConsoleKeyInfo key);
}
