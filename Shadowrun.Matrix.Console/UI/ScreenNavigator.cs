namespace Shadowrun.Matrix.UI;

/// <summary>
/// Owns the screen navigation stack and drives the render/input loop.
///
/// <para>Callers push an initial screen, then call <see cref="RenderCurrent"/>
/// and <see cref="HandleKey"/> in a tight loop.</para>
/// </summary>
public sealed class ScreenNavigator
{
    // ── State ─────────────────────────────────────────────────────────────────

    private readonly Stack<IScreen> _stack = new();

    /// <summary>Factory that creates a fresh Main Menu screen when the stack empties.</summary>
    private readonly Func<IScreen> _rootFactory;

    /// <summary>Factory that creates a fresh name-entry screen for a new game.</summary>
    private readonly Func<IScreen> _newGameFactory;

    // ── Construction ─────────────────────────────────────────────────────────

    /// <param name="rootFactory">Called whenever the stack must be reset to the main menu.</param>
    /// <param name="newGameFactory">Called when a new game should start (e.g. after Game Over).</param>
    public ScreenNavigator(Func<IScreen> rootFactory, Func<IScreen>? newGameFactory = null)
    {
        _rootFactory    = rootFactory;
        _newGameFactory = newGameFactory ?? rootFactory;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>The screen currently on top of the stack, or null if empty.</summary>
    public IScreen? Current => _stack.Count > 0 ? _stack.Peek() : null;

    /// <summary>Push a screen onto the stack (does not render it).</summary>
    public void Push(IScreen screen) => _stack.Push(screen);

    /// <summary>
    /// Replaces the top screen with <paramref name="screen"/> without calling
    /// HandleInput on the outgoing screen. Used for programmatic transitions
    /// (e.g. auto-transition when a session ends mid-tick).
    /// </summary>
    public void ReplaceTop(IScreen screen)
    {
        if (_stack.Count > 0) _stack.Pop();
        _stack.Push(screen);
    }

    /// <summary>
    /// Render the top screen using the current terminal dimensions.
    /// Clears the console before drawing.
    /// </summary>
    public void RenderCurrent()
    {
        if (_stack.Count == 0)
            Reset();

        int w = Math.Max(36, Console.WindowWidth);
        int h = Math.Max(10, Console.WindowHeight);

        VC.BeginFrame(w, h);
        _stack.Peek().Render(w, h);
        VC.EndFrame();
    }

    /// <summary>
    /// Route a keypress to the top screen, then act on its return value.
    /// </summary>
    public void HandleKey(ConsoleKeyInfo key)
    {
        if (_stack.Count == 0)
        {
            Reset();
            return;
        }

        IScreen? result = _stack.Peek().HandleInput(key);

        if (result is null)
            return; // stay on current screen

        if (ReferenceEquals(result, NavigationToken.Root))
        {
            Reset();
            return;
        }

        if (ReferenceEquals(result, NavigationToken.Back))
        {
            _stack.Pop();
            if (_stack.Count == 0)
                Reset();
            return;
        }

        if (ReferenceEquals(result, NavigationToken.BackTwo))
        {
            if (_stack.Count > 0) _stack.Pop();
            if (_stack.Count > 0) _stack.Pop();
            if (_stack.Count == 0) Reset();
            return;
        }

        if (ReferenceEquals(result, NavigationToken.NewGame))
        {
            _stack.Clear();
            _stack.Push(_newGameFactory());
            return;
        }

        // Push new screen
        _stack.Push(result);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>Clear the stack and push a fresh root screen.</summary>
    private void Reset()
    {
        _stack.Clear();
        _stack.Push(_rootFactory());
    }
}
