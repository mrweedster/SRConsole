using Shadowrun.Matrix.Enums;
using Shadowrun.Matrix.ValueObjects;

namespace Shadowrun.Matrix.Models;

/// <summary>
/// Configuration bag for procedural Matrix system generation.
/// Controls node counts, ICE density, and rating ranges per difficulty tier.
/// </summary>
public class ProceduralSystemConfig
{
    // ── Identity ──────────────────────────────────────────────────────────────

    public string Difficulty { get; }

    // ── Node counts ───────────────────────────────────────────────────────────

    public int MinNodes { get; }
    public int MaxNodes { get; }

    // ── Security ──────────────────────────────────────────────────────────────

    /// <summary>Minimum ICE base rating on this tier.</summary>
    public int MinIceRating { get; }

    /// <summary>Maximum ICE base rating on this tier.</summary>
    public int MaxIceRating { get; }

    /// <summary>Fraction of nodes that will have ICE (0.0–1.0).</summary>
    public float IceDensity { get; }

    /// <summary>
    /// Fraction of ICE'd nodes that will have a secondary hidden Tar-type ICE.
    /// </summary>
    public float TarIceProbability { get; }

    /// <summary>
    /// Whether BlackIce may appear in this tier.
    /// False for simple runs (almost no BlackIce), true for moderate/expert.
    /// </summary>
    public bool AllowBlackIce { get; }

    /// <summary>Color range allowed for nodes in this tier.</summary>
    public IReadOnlyList<NodeColor> AllowedColors { get; }

    // ── Predefined tiers ─────────────────────────────────────────────────────

    public static readonly ProceduralSystemConfig Simple = new(
        difficulty:        "simple",
        minNodes:          8,
        maxNodes:          12,
        minIceRating:      1,
        maxIceRating:      3,
        iceDensity:        0.5f,
        tarIceProbability: 0.05f,
        allowBlackIce:     false,
        allowedColors:     [NodeColor.Blue, NodeColor.Green]);

    public static readonly ProceduralSystemConfig Moderate = new(
        difficulty:        "moderate",
        minNodes:          13,
        maxNodes:          18,
        minIceRating:      2,
        maxIceRating:      5,
        iceDensity:        0.65f,
        tarIceProbability: 0.15f,
        allowBlackIce:     true,
        allowedColors:     [NodeColor.Blue, NodeColor.Green, NodeColor.Orange]);

    public static readonly ProceduralSystemConfig Expert = new(
        difficulty:        "expert",
        minNodes:          20,
        maxNodes:          28,
        minIceRating:      4,
        maxIceRating:      7,
        iceDensity:        0.85f,
        tarIceProbability: 0.30f,
        allowBlackIce:     true,
        allowedColors:     [NodeColor.Green, NodeColor.Orange, NodeColor.Red]);

    // ── Construction ─────────────────────────────────────────────────────────

    public ProceduralSystemConfig(
        string              difficulty,
        int                 minNodes,
        int                 maxNodes,
        int                 minIceRating,
        int                 maxIceRating,
        float               iceDensity,
        float               tarIceProbability,
        bool                allowBlackIce,
        IEnumerable<NodeColor> allowedColors)
    {
        if (!MatrixRun.ValidDifficulties.Contains(difficulty))
            throw new ArgumentException("Invalid difficulty.", nameof(difficulty));
        if (minNodes < 1 || minNodes > maxNodes)
            throw new ArgumentException("minNodes must be >= 1 and <= maxNodes.", nameof(minNodes));
        if (minIceRating < 1 || minIceRating > maxIceRating || maxIceRating > 7)
            throw new ArgumentException("ICE rating range must be within 1–7.", nameof(minIceRating));

        Difficulty        = difficulty;
        MinNodes          = minNodes;
        MaxNodes          = maxNodes;
        MinIceRating      = minIceRating;
        MaxIceRating      = maxIceRating;
        IceDensity        = Math.Clamp(iceDensity, 0f, 1f);
        TarIceProbability = Math.Clamp(tarIceProbability, 0f, 1f);
        AllowBlackIce     = allowBlackIce;
        AllowedColors     = allowedColors.ToList().AsReadOnly();
    }

    /// <summary>Returns the preset config for the given difficulty string.</summary>
    public static ProceduralSystemConfig ForDifficulty(string difficulty) => difficulty switch
    {
        "simple"   => Simple,
        "moderate" => Moderate,
        "expert"   => Expert,
        _          => throw new ArgumentException($"Unknown difficulty: '{difficulty}'.")
    };
}
