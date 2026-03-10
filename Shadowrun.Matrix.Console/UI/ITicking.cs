namespace Shadowrun.Matrix.UI;

/// <summary>
/// Implemented by screens that need real-time delta-time updates
/// (e.g. MatrixGameScreen during active combat).
/// </summary>
public interface ITicking
{
    /// <summary>Called by the game loop every frame with elapsed seconds since last call.</summary>
    void Tick(float dt);

    /// <summary>True when the screen needs to be redrawn on the next frame.</summary>
    bool NeedsRedraw { get; }

    /// <summary>
    /// Returns and clears a pending automatic screen transition (e.g. session ended
    /// mid-tick). The game loop checks this after every tick and navigates immediately
    /// without waiting for a key press. Returns null when no transition is pending.
    /// </summary>
    IScreen? PopAutoTransition();
}
