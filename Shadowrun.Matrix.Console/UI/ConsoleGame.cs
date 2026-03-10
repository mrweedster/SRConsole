using Shadowrun.Matrix.Models;
using Shadowrun.Matrix.UI;
using Shadowrun.Matrix.UI.Screens;

/// <summary>
/// Top-level game runner. Owns the navigator and drives the render/input loop.
/// </summary>
public sealed class ConsoleGame
{
    private readonly Decker?                        _decker;
    private readonly IReadOnlyList<MatrixRunEntry>  _availableRuns;
    private readonly GameState?                     _gameState;

    /// <summary>Initial screen to push — overrides default Main Menu when set.</summary>
    private readonly IScreen?                       _initialScreen;

    /// <summary>Standard constructor: existing save, go straight to Main Menu.</summary>
    public ConsoleGame(Decker decker, IReadOnlyList<MatrixRunEntry> availableRuns, GameState? gameState = null)
    {
        _decker        = decker;
        _availableRuns = availableRuns;
        _gameState     = gameState ?? new GameState();
        _initialScreen = null;
    }

    /// <summary>
    /// New-game constructor: push a custom initial screen (e.g. name entry)
    /// instead of going straight to the Main Menu.
    /// </summary>
    public ConsoleGame(IReadOnlyList<MatrixRunEntry> availableRuns, IScreen initialScreen)
    {
        _availableRuns = availableRuns;
        _initialScreen = initialScreen;
        _decker        = null;
        _gameState     = null;
    }

    public void Run()
    {
        VC.CursorVisible = false;

        // If we have a decker (loaded save), root = Main Menu.
        // If we don't (new game), the name screen will push a Main Menu once the
        // player confirms, so root just falls back to a placeholder that goes nowhere —
        // in practice the stack is never empty during normal play.
        // For a loaded save, the root is always the Main Menu.
        // For a new game, the root starts as NewGameNameScreen, but once the player
        // confirms their name and BuildGame fires, we update _currentRoot to the
        // newly created MainMenuScreen so Root tokens navigate there from then on.
        IScreen? _currentRoot = _decker is not null
            ? new MainMenuScreen(_decker, _availableRuns, _gameState!)
            : null;

        IScreen rootScreen = _currentRoot
            ?? new NewGameNameScreen(_availableRuns, screen => _currentRoot = screen);

        var navigator = new ScreenNavigator(
            () => _currentRoot ?? new NewGameNameScreen(_availableRuns, screen => _currentRoot = screen),
            () => new NewGameNameScreen(_availableRuns, screen => _currentRoot = screen));

        navigator.Push(rootScreen);

        var  sw        = System.Diagnostics.Stopwatch.StartNew();
        bool lastWasTicking = false;

        while (true)
        {
            float dt     = (float)sw.Elapsed.TotalSeconds;
            sw.Restart();

            bool needsDraw = false;

            // Tick real-time screens (MatrixGameScreen during combat)
            if (navigator.Current is ITicking ticker)
            {
                ticker.Tick(dt);

                // Check for session-end transitions that need to fire immediately
                // (e.g. decker killed by BlackIce, persona dumped mid-tick)
                var autoTransition = ticker.PopAutoTransition();
                if (autoTransition is not null)
                {
                    // Replace the MatrixGameScreen with the end screen directly —
                    // do NOT call HandleKey here, it would re-enter HandleInput on
                    // the dead session and push a second screen.
                    navigator.ReplaceTop(autoTransition);
                    navigator.RenderCurrent();
                    lastWasTicking = false;
                    continue;
                }

                if (ticker.NeedsRedraw) needsDraw = true;
                lastWasTicking = true;
            }
            else
            {
                lastWasTicking = false;
            }

            // Handle input — non-blocking when ticking, blocking otherwise
            if (lastWasTicking)
            {
                if (VC.KeyAvailable)
                {
                    var key = Console.ReadKey(intercept: true);
                    navigator.HandleKey(key);
                    needsDraw = true;
                }
                // Controller (edge-triggered, one press per poll)
                var ctrlKey = ControllerInput.Poll();
                if (ctrlKey.HasValue)
                {
                    navigator.HandleKey(ctrlKey.Value);
                    needsDraw = true;
                }
                if (needsDraw)
                    navigator.RenderCurrent();
                System.Threading.Thread.Sleep(50);
            }
            else
            {
                // Non-combat: render then wait for any input (keyboard or controller)
                navigator.RenderCurrent();
                ConsoleKeyInfo? key = null;
                while (key is null)
                {
                    if (VC.KeyAvailable)
                    {
                        key = Console.ReadKey(intercept: true);
                        break;
                    }
                    var ctrl = ControllerInput.Poll();
                    if (ctrl.HasValue) { key = ctrl; break; }
                    System.Threading.Thread.Sleep(16);   // ~60 Hz polling
                }
                navigator.HandleKey(key!.Value);
            }
        }
    }
}
