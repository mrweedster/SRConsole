using Shadowrun.Matrix.Enums;

namespace Shadowrun.Matrix.ValueObjects;

/// <summary>
/// Immutable definition of a program at a specific level.
/// Shared between all <c>Program</c> instances of the same name + level.
/// Does not represent a copy on a deck — see <c>Program</c> for that.
/// </summary>
public class ProgramSpec
{
    // ── Identity ──────────────────────────────────────────────────────────────

    public ProgramName Name        { get; }
    public ProgramType Type        { get; }

    /// <summary>Program level 1–8.</summary>
    public int         Level       { get; }

    // ── Derived / static properties ───────────────────────────────────────────

    /// <summary>
    /// Storage and memory footprint in Megapulses.
    /// Formula: Small = level² × 2; Medium = level² × 3; Large = level² × 4.
    /// Special programs (Degrade, Rebound) use hardcoded sizes.
    /// </summary>
    public int    SizeInMp         { get; }

    /// <summary>Base price at 0–2 Negotiation skill. Discounted by Cyberdeck.ComputePrice().</summary>
    public int    BasePrice        { get; }

    public string Description      { get; }

    /// <summary>Walkthrough usefulness rating 1–10. 10 = most useful.</summary>
    public int    UsefulnessRating { get; }

    /// <summary>
    /// If true, this program must fully reload (loadProgress reset to 0) after each use,
    /// not just refresh. Applies to Medic, Mirrors, and Smoke.
    /// </summary>
    public bool   ReloadsAfterUse  { get; }

    /// <summary>
    /// True for programs that target the Persona (or the dataspace globally) rather
    /// than ICE. These must never be passed an ICE target — doing so would route them
    /// through the combat success roll, cause spurious combat engagement, and incorrectly
    /// trigger Tar secondary ICE on a "failed" heal/shield.
    ///
    /// Medic  — heals Persona energy.
    /// Shield — absorbs incoming damage for the Persona.
    /// </summary>
    public bool TargetsPersonaOnly =>
        Name is ProgramName.Medic or ProgramName.Shield;

    // ── Construction ─────────────────────────────────────────────────────────

    public ProgramSpec(
        ProgramName name,
        ProgramType type,
        int         level,
        string      description,
        int         usefulnessRating,
        bool        reloadsAfterUse,
        int?        overrideSizeInMp  = null,
        int?        overrideBasePrice = null)
    {
        if (level is < 1 or > 8)
            throw new ArgumentOutOfRangeException(nameof(level), "Program level must be 1–8.");

        if (usefulnessRating is < 1 or > 10)
            throw new ArgumentOutOfRangeException(nameof(usefulnessRating),
                "Usefulness rating must be 1–10.");

        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        Name             = name;
        Type             = type;
        Level            = level;
        Description      = description;
        UsefulnessRating = usefulnessRating;
        ReloadsAfterUse  = reloadsAfterUse;

        SizeInMp  = overrideSizeInMp  ?? ComputeSize(type,  level);
        BasePrice = overrideBasePrice ?? ComputeBasePrice(type, level);
    }

    // ── Static factory: build the full catalog ────────────────────────────────

    /// <summary>
    /// Builds a <see cref="ProgramSpec"/> for every (name, level) combination
    /// recognised by the game. Returns an empty list for names/levels not
    /// available in the base game (e.g. Degrade/Rebound at most levels).
    /// </summary>
    public static IReadOnlyList<ProgramSpec> BuildCatalog()
    {
        var catalog = new List<ProgramSpec>();

        // Standard programs — all 8 levels
        foreach (int level in Enumerable.Range(1, 8))
        {
            catalog.Add(new ProgramSpec(ProgramName.Attack,    ProgramType.Small,  level,
                "Used to destroy (crash) IC. Mandatory when Deception fails.",         9, false));

            catalog.Add(new ProgramSpec(ProgramName.Slow,      ProgramType.Large,  level,
                "Reduces ICE reaction speed. No effect on Trace ICE.",                 3, false));

            catalog.Add(new ProgramSpec(ProgramName.Medic,     ProgramType.Large,  level,
                "Repairs Persona energy. Slower when below 25% health.",               4, true));

            catalog.Add(new ProgramSpec(ProgramName.Shield,    ProgramType.Large,  level,
                "Reduces incoming damage per hit. Useless against BlackIce.",          3, false));

            catalog.Add(new ProgramSpec(ProgramName.Smoke,     ProgramType.Small,  level,
                "Adds difficulty to ALL actions for 4s. Useful for Tar Pit bait.",     1, true));

            catalog.Add(new ProgramSpec(ProgramName.Mirrors,   ProgramType.Medium, level,
                "Reduces ICE attack accuracy for 4s.",                                 2, true));

            catalog.Add(new ProgramSpec(ProgramName.Sleaze,    ProgramType.Medium, level,
                "Bypasses a Node without defeating its ICE. Works on all ICE types.",  7, false));

            catalog.Add(new ProgramSpec(ProgramName.Deception, ProgramType.Small,  level,
                "Instantly defeats Access, Blaster, Killer, Trace ICE. Pre-combat only.", 10, false));

            catalog.Add(new ProgramSpec(ProgramName.Relocate,  ProgramType.Small,  level,
                "Instantly defeats any Trace ICE. Redundant with pre-combat Deception.", 4, false));

            catalog.Add(new ProgramSpec(ProgramName.Analyze,   ProgramType.Medium, level,
                "Scans Node and ICE. Enables success bars. May need multiple runs.",   3, false));
        }

        // Special programs — only levels 3 and 6 are available in-game
        // Sizes and prices are hardcoded; they do not follow the standard formula.
        catalog.Add(new ProgramSpec(ProgramName.Degrade, ProgramType.Special, 3,
            "Lowers Node security rating by 1. Weakens both Node and ICE.",
            6, false, overrideSizeInMp: 36,  overrideBasePrice: 3_000));

        catalog.Add(new ProgramSpec(ProgramName.Degrade, ProgramType.Special, 6,
            "Lowers Node security rating by 1. Weakens both Node and ICE.",
            6, false, overrideSizeInMp: 144, overrideBasePrice: 30_000));

        catalog.Add(new ProgramSpec(ProgramName.Rebound, ProgramType.Special, 3,
            "Deflects attacks back at ICE. Breaks after 2–3 deflections.",
            2, false, overrideSizeInMp: 27,  overrideBasePrice: 3_000));

        catalog.Add(new ProgramSpec(ProgramName.Rebound, ProgramType.Special, 6,
            "Deflects attacks back at ICE. Breaks after 2–3 deflections.",
            2, false, overrideSizeInMp: 108, overrideBasePrice: 30_000));

        return catalog.AsReadOnly();
    }

    // ── Price computation ─────────────────────────────────────────────────────

    /// <summary>
    /// Applies the standard negotiation discount to this program's base price.
    /// Each negotiation point above 2 reduces cost by ~3.125% of base (floor division).
    /// Negotiation 0–2 returns the full base price.
    /// </summary>
    public int ComputeDiscountedPrice(int negotiationRating)
    {
        if (negotiationRating < 0 || negotiationRating > 12)
            throw new ArgumentOutOfRangeException(nameof(negotiationRating),
                "Negotiation rating must be 0–12.");

        if (negotiationRating <= 2)
            return BasePrice;

        // Each point above 2 knocks off ~3.125% (1/32) of base price
        int discountSteps = negotiationRating - 2;
        int discount       = (BasePrice * discountSteps) / 32;
        return BasePrice - discount;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Standard size formula.
    /// Small: level² × 2 | Medium: level² × 3 | Large: level² × 4.
    /// Special programs must pass an overrideSizeInMp.
    /// </summary>
    private static int ComputeSize(ProgramType type, int level)
    {
        if (type == ProgramType.Special)
            throw new InvalidOperationException(
                "Special programs must supply an explicit SizeInMp override.");

        int multiplier = (int)type + 1; // Small=1→×2, Medium=2→×3, Large=3→×4
        return level * level * multiplier * 2;
    }

    /// <summary>
    /// Standard base price formula at 0–2 Negotiation.
    /// Small: 60 × level² | Medium: 90 × level² | Large: 120 × level².
    /// </summary>
    private static int ComputeBasePrice(ProgramType type, int level)
    {
        if (type == ProgramType.Special)
            throw new InvalidOperationException(
                "Special programs must supply an explicit BasePrice override.");

        int baseUnit = (int)type * 60; // Small=60, Medium=120... wait — see note below
        // Actual game values: Small base = 60×level², Medium = 90×level², Large = 120×level²
        // The multiplier sequence is 60/90/120, i.e. 60 × (typeIndex×0.5 + 1)
        // Simplest correct expression:
        return type switch
        {
            ProgramType.Small  => 60  * level * level,
            ProgramType.Medium => 90  * level * level,
            ProgramType.Large  => 120 * level * level,
            _                  => throw new InvalidOperationException("Unreachable.")
        };
    }

    // ── Display ───────────────────────────────────────────────────────────────

    public override string ToString() =>
        $"[{Name} L{Level}] Type:{Type} Size:{SizeInMp}Mp Price:{BasePrice}¥ " +
        $"Useful:{UsefulnessRating}/10";
}
