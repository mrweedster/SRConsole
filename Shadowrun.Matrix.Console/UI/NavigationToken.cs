namespace Shadowrun.Matrix.UI;

/// <summary>
/// Sentinel <see cref="IScreen"/> values used to signal navigation instructions
/// that are not real screens.
///
/// <para>The navigator checks incoming <c>HandleInput</c> return values by
/// reference identity against these singletons and routes accordingly.
/// Their <c>Render</c> and <c>HandleInput</c> implementations must never be
/// called — they throw to expose accidental misuse.</para>
/// </summary>
public sealed class NavigationToken : IScreen
{
    // ── Singletons ────────────────────────────────────────────────────────────

    /// <summary>Pop the current screen and return to the previous one.</summary>
    public static readonly NavigationToken Back = new("BACK");

    /// <summary>Clear the entire stack and push a fresh Main Menu screen.</summary>
    public static readonly NavigationToken Root = new("ROOT");

    /// <summary>
    /// Pop the two top-most screens in one step.
    /// Used by confirm screens that sit two levels above the list they return to,
    /// e.g. ProgramDeleteConfirmScreen → ProgramsInstalledSubmenuScreen → ProgramsInstalledScreen.
    /// </summary>
    public static readonly NavigationToken BackTwo = new("BACKTWO");

    /// <summary>
    /// Clear the entire stack and push a fresh name-entry (new game) screen.
    /// Used exclusively by GameOverScreen so the player can start over.
    /// </summary>
    public static readonly NavigationToken NewGame = new("NEWGAME");

    // ── Implementation ────────────────────────────────────────────────────────

    private readonly string _name;
    private NavigationToken(string name) => _name = name;

    public void     Render(int w, int h)         => throw new InvalidOperationException($"NavigationToken.{_name} must not be rendered.");
    public IScreen? HandleInput(ConsoleKeyInfo k) => throw new InvalidOperationException($"NavigationToken.{_name} must not handle input.");

    public override string ToString() => $"NavigationToken.{_name}";
}
