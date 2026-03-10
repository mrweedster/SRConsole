using Shadowrun.Matrix.Core;
using Shadowrun.Matrix.Enums;
using Shadowrun.Matrix.ValueObjects;

namespace Shadowrun.Matrix.Models;

/// <summary>
/// A single copy of a program residing on a Cyberdeck.
/// Tracks runtime state: whether it is loaded into one of the five active memory
/// slots, how far through loading it is, and the latest success-bar estimate.
///
/// The immutable definition (name, type, level, size, price) lives in
/// <see cref="Spec"/>. Multiple <see cref="Program"/> instances can share the
/// same <see cref="ProgramSpec"/> if the Decker somehow owns duplicates, though
/// that is not expected in normal gameplay.
/// </summary>
public class Program
{
    // ── Identity ──────────────────────────────────────────────────────────────

    /// <summary>Unique instance identifier — distinguishes two copies of the same program.</summary>
    public string Id { get; } = Guid.NewGuid().ToString("N");

    /// <summary>The immutable definition this instance is based on.</summary>
    public ProgramSpec Spec { get; }

    // ── Runtime state ─────────────────────────────────────────────────────────

    /// <summary>
    /// Whether this program currently occupies one of the Cyberdeck's five
    /// active memory slots. A program must be loaded before it can be run.
    /// </summary>
    public bool IsLoaded { get; private set; }

    /// <summary>
    /// Loading progress from 0.0 (not loaded) to 1.0 (fully loaded and ready).
    /// Programs loaded before jacking in start at 1.0.
    /// Programs loaded mid-session start at 0.0 and advance via
    /// <see cref="Cyberdeck.TickLoading"/> based on the deck's Load/IO Speed.
    /// </summary>
    public float LoadProgress { get; private set; }

    /// <summary>
    /// Estimated probability (0.0–1.0) that this program will succeed against
    /// the currently scanned Node. Only valid after a successful Analyze scan
    /// has fully identified the Node's ICE. Displayed as the vertical success
    /// bar next to each program icon on the cyberdeck screen.
    /// </summary>
    public float SuccessBar { get; private set; }

    /// <summary>
    /// Cooldown progress from 0.0 (on cooldown) to 1.0 (refreshed and ready).
    /// After a program is used it is set to 0.0 and advances automatically.
    /// Programs that <see cref="ProgramSpec.ReloadsAfterUse"/> reset
    /// <see cref="LoadProgress"/> instead of this value.
    /// </summary>
    public float RefreshProgress { get; private set; } = 1.0f;

    /// <summary>
    /// For Rebound: number of deflection charges remaining.
    /// Starts at <see cref="ReboundMaxCharges"/> when the program is run;
    /// decremented by <see cref="ConsumeReboundCharge"/>; program effect ends at 0.
    /// Irrelevant for all other program types.
    /// </summary>
    public int ReboundChargesRemaining { get; private set; }

    // ── Constants ─────────────────────────────────────────────────────────────

    /// <summary>Number of deflections Rebound can absorb before breaking.</summary>
    public const int ReboundMaxCharges = 3;

    /// <summary>
    /// A loaded program is ready to run when both loading and refresh are complete.
    /// </summary>
    public bool IsReadyToRun =>
        IsLoaded && LoadProgress >= 1.0f && RefreshProgress >= 1.0f;

    // ── Construction ─────────────────────────────────────────────────────────

    /// <param name="spec">The program definition to base this instance on.</param>
    /// <param name="preloaded">
    /// Pass <c>true</c> when equipping the program before jacking in — sets
    /// <see cref="LoadProgress"/> to 1.0 immediately. Pass <c>false</c> when
    /// loading mid-session; the deck will drive progress via
    /// <see cref="AdvanceLoading"/>.
    /// </param>
    public Program(ProgramSpec spec, bool preloaded = false)
    {
        ArgumentNullException.ThrowIfNull(spec);
        Spec         = spec;
        IsLoaded     = preloaded;
        LoadProgress = preloaded ? 1.0f : 0.0f;
    }

    // ── Loading lifecycle ─────────────────────────────────────────────────────

    /// <summary>
    /// Marks this program as occupying a memory slot, setting its state
    /// according to whether it was loaded before or after jacking in.
    /// Called exclusively by <see cref="Cyberdeck.LoadProgram"/>.
    /// </summary>
    /// <param name="midSession">
    /// True if the Decker is currently inside the Matrix; loading will be
    /// deferred and driven by <see cref="AdvanceLoading"/>.
    /// </param>
    internal void BeginLoad(bool midSession)
    {
        if (IsLoaded)
            throw new InvalidOperationException(
                $"Program '{Spec.Name}' is already loaded.");

        IsLoaded     = true;
        LoadProgress = midSession ? 0.0f : 1.0f;
    }

    /// <summary>
    /// Advances loading progress by <paramref name="delta"/> (0.0–1.0).
    /// Only moves the needle when <see cref="LoadProgress"/> &lt; 1.0.
    /// Called by <see cref="Cyberdeck.TickLoading"/> for the front-most
    /// incomplete slot.
    /// </summary>
    internal void AdvanceLoading(float delta)
    {
        if (delta < 0f)
            throw new ArgumentOutOfRangeException(nameof(delta), "Delta must be non-negative.");

        if (LoadProgress < 1.0f)
            LoadProgress = Math.Min(1.0f, LoadProgress + delta);
    }

    /// <summary>
    /// Marks the program as unloaded and frees its memory slot.
    /// Called exclusively by <see cref="Cyberdeck.UnloadProgram"/>.
    /// </summary>
    internal void Unload()
    {
        if (!IsLoaded)
            throw new InvalidOperationException(
                $"Program '{Spec.Name}' is not currently loaded.");

        IsLoaded     = false;
        LoadProgress = 0.0f;
    }

    // ── Post-use lifecycle ────────────────────────────────────────────────────

    /// <summary>
    /// Called after a successful or failed program run when the program only
    /// needs a brief cooldown before it can be used again (the common case).
    /// Resets <see cref="RefreshProgress"/> to 0.0; the session tick loop
    /// advances it back to 1.0 over time based on deck Response.
    /// </summary>
    public void Refresh()
    {
        if (!IsLoaded)
            throw new InvalidOperationException(
                $"Cannot refresh '{Spec.Name}' — program is not loaded.");

        RefreshProgress = 0.0f;
    }

    /// <summary>
    /// Advances the refresh cooldown by <paramref name="delta"/>.
    /// Called by the session tick loop.
    /// </summary>
    internal void AdvanceRefresh(float delta)
    {
        if (delta < 0f)
            throw new ArgumentOutOfRangeException(nameof(delta), "Delta must be non-negative.");

        if (RefreshProgress < 1.0f)
            RefreshProgress = Math.Min(1.0f, RefreshProgress + delta);
    }

    /// <summary>
    /// Called after a program run for programs whose
    /// <see cref="ProgramSpec.ReloadsAfterUse"/> is <c>true</c>
    /// (Medic, Mirrors, Smoke). Resets <see cref="LoadProgress"/> to 0.0,
    /// requiring a full reload before the program can be used again.
    /// </summary>
    public void BeginReload()
    {
        if (!IsLoaded)
            throw new InvalidOperationException(
                $"Cannot reload '{Spec.Name}' — program is not loaded.");

        LoadProgress    = 0.0f;
        RefreshProgress = 1.0f; // Refresh is not the blocker; loading is.
    }

    // ── Success bar ───────────────────────────────────────────────────────────

    /// <summary>
    /// Updates the estimated success probability for the current Node encounter.
    /// Only meaningful after a full Analyze scan. Clamped to [0.0, 1.0].
    /// </summary>
    public void UpdateSuccessBar(float estimate)
    {
        if (estimate is < 0f or > 1f)
            throw new ArgumentOutOfRangeException(nameof(estimate),
                "Success bar estimate must be 0.0–1.0.");

        SuccessBar = estimate;
    }

    /// <summary>
    /// Clears the success bar when leaving a Node or between encounters.
    /// </summary>
    public void ClearSuccessBar() => SuccessBar = 0.0f;

    // ── Rebound charges ───────────────────────────────────────────────────────

    /// <summary>
    /// Initialises Rebound charges when the program is successfully activated.
    /// Only valid for the Rebound program.
    /// </summary>
    public void InitialiseReboundCharges()
    {
        if (Spec.Name != ProgramName.Rebound)
            throw new InvalidOperationException("Only the Rebound program has charges.");

        ReboundChargesRemaining = ReboundMaxCharges;
    }

    /// <summary>
    /// Consumes one Rebound deflection charge.
    /// Returns <c>true</c> when the program still has charges remaining;
    /// <c>false</c> when it has broken (0 charges left).
    /// </summary>
    public bool ConsumeReboundCharge()
    {
        if (Spec.Name != ProgramName.Rebound)
            throw new InvalidOperationException("Only the Rebound program has charges.");

        if (ReboundChargesRemaining <= 0)
            return false;

        ReboundChargesRemaining--;
        return ReboundChargesRemaining > 0;
    }

    // ── Display ───────────────────────────────────────────────────────────────

    public override string ToString()
    {
        string status = IsLoaded
            ? LoadProgress < 1.0f
                ? $"Loading {LoadProgress:P0}"
                : RefreshProgress < 1.0f
                    ? $"Refreshing {RefreshProgress:P0}"
                    : "Ready"
            : "Unloaded";

        return $"[Program] {Spec.Name} L{Spec.Level} ({Spec.SizeInMp}Mp) — {status}";
    }
}
