using Shadowrun.Matrix.Models;

namespace Shadowrun.Matrix.UI;

/// <summary>
/// Mutable container for game-wide state shared across screens.
/// Passed by reference so all screens always see the current value.
/// </summary>
public sealed class GameState
{
    /// <summary>The currently active contracted Matrix run, or null for a free run.</summary>
    public MatrixRun? ActiveRun { get; set; }

    /// <summary>Accumulated karma points available to spend on skill upgrades.</summary>
    public int Karma { get; set; }

    /// <summary>
    /// The live MatrixSession while the decker is jacked in.
    /// Remains set even when the player navigates to the main menu mid-run.
    /// Check <see cref="MatrixSession.IsActive"/> before using.
    /// </summary>
    public MatrixSession? ActiveSession { get; set; }

    /// <summary>The system number of the current/last session (used for Return to Matrix).</summary>
    public int ActiveSystemNumber { get; set; }

    /// <summary>
    /// Set when a run is auto-completed during jack-out/crash.
    /// Consumed by <see cref="Screens.MatrixSessionEndScreen"/> to display the reward.
    /// </summary>
    public RunCompletionResult? PendingReward { get; set; }
}
