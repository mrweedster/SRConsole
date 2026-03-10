using Shadowrun.Matrix.Enums;

namespace Shadowrun.Matrix.ValueObjects;

/// <summary>
/// Immutable definition (blueprint) of an ICE instance.
/// Describes what a particular ICE type at a given base rating looks like and
/// how it behaves — shared across all nodes that use the same type and rating.
///
/// The runtime mutable state (current health, probe position, isAlive) lives
/// in <c>Ice</c>, not here.
/// </summary>
public class IceSpec
{
    // ── Identity ──────────────────────────────────────────────────────────────

    public IceType Type { get; }

    /// <summary>
    /// Base security rating 1–7. Effective rating in play =
    /// <c>BaseRating + (int)system.AlertState</c>, capped at 9.
    /// All combat stats (attack power, hit frequency, probe speed) scale with this.
    /// </summary>
    public int BaseRating { get; }

    // ── Encounter ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Relative weight used when procedurally assigning ICE to nodes.
    /// Higher values appear more frequently.
    /// Based on observed occurrence percentages from the game:
    /// Access=20%, Barrier=15.9%, BlackIce=13.3%, etc.
    /// </summary>
    public float OccurrenceWeight { get; }

    // ── Combat weaknesses ─────────────────────────────────────────────────────

    /// <summary>
    /// Programs that instantly destroy this ICE on a successful run.
    /// Empty for ICE types that must be defeated via Attack only (Barrier, BlackIce).
    /// </summary>
    public IReadOnlyList<ProgramName> WeakAgainst { get; }

    // ── Positioning ───────────────────────────────────────────────────────────

    /// <summary>
    /// True for Tar Paper and Tar Pit — these ICE types always hide
    /// behind a primary non-Trace, non-Tar ICE and are only revealed
    /// when the primary is dealt with.
    /// </summary>
    public bool IsHidden { get; }

    /// <summary>
    /// For hidden ICE: the type of ICE in front of this one that must be
    /// cleared first. Null for non-hidden ICE.
    /// </summary>
    public IceType? PrimaryIceType { get; }

    // ── Visual ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Description of the ICE's in-game graphic — useful for UI rendering cues.
    /// </summary>
    public string GraphicDescription { get; }

    // ── Construction ─────────────────────────────────────────────────────────

    public IceSpec(
        IceType              type,
        int                  baseRating,
        float                occurrenceWeight,
        IEnumerable<ProgramName> weakAgainst,
        bool                 isHidden,
        IceType?             primaryIceType,
        string               graphicDescription)
    {
        if (baseRating is < 1 or > 7)
            throw new ArgumentOutOfRangeException(nameof(baseRating),
                "ICE base rating must be 1–7.");

        if (occurrenceWeight < 0f)
            throw new ArgumentOutOfRangeException(nameof(occurrenceWeight),
                "Occurrence weight cannot be negative.");

        if (isHidden && primaryIceType is null)
            throw new ArgumentException(
                "Hidden ICE must specify a PrimaryIceType.", nameof(primaryIceType));

        if (!isHidden && primaryIceType is not null)
            throw new ArgumentException(
                "Non-hidden ICE must not specify a PrimaryIceType.", nameof(primaryIceType));

        ArgumentException.ThrowIfNullOrWhiteSpace(graphicDescription);

        Type               = type;
        BaseRating         = baseRating;
        OccurrenceWeight   = occurrenceWeight;
        WeakAgainst        = weakAgainst.ToList().AsReadOnly();
        IsHidden           = isHidden;
        PrimaryIceType     = primaryIceType;
        GraphicDescription = graphicDescription;
    }

    // ── Computed ──────────────────────────────────────────────────────────────

    /// <summary>
    /// True for Trace &amp; Burn and Trace &amp; Dump — ICE types that send a tracking
    /// probe across the screen rather than attacking directly.
    /// </summary>
    public bool IsTraceType =>
        Type is IceType.TraceAndBurn or IceType.TraceAndDump;

    /// <summary>
    /// True for Tar Paper and Tar Pit — ICE that reacts to a failed program run
    /// rather than attacking the Persona directly.
    /// </summary>
    public bool IsTarType =>
        Type is IceType.TarPaper or IceType.TarPit;

    /// <summary>
    /// True for BlackIce — damage from this ICE bypasses Persona energy and
    /// hits the Decker's physical health directly.
    /// </summary>
    public bool DealsPhysicalDamage => Type is IceType.BlackIce;

    /// <summary>
    /// Whether this ICE type can be defeated by a program other than Attack.
    /// </summary>
    public bool HasProgramWeakness => WeakAgainst.Count > 0;

    // ── Static catalog ────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a representative set of IceSpec definitions at a given base rating.
    /// In a full implementation, call this for ratings 1–7 to populate the game world.
    /// </summary>
    public static IReadOnlyList<IceSpec> BuildCatalogAtRating(int baseRating)
    {
        if (baseRating is < 1 or > 7)
            throw new ArgumentOutOfRangeException(nameof(baseRating));

        return new List<IceSpec>
        {
            new(IceType.Access,       baseRating, 20.0f,
                [ProgramName.Deception],
                false, null,
                "A square hatch with doors that repeatedly slide open and shut."),

            new(IceType.Barrier,      baseRating, 15.9f,
                [],
                false, null,
                "A rotating three-spoked circular spark."),

            new(IceType.BlackIce,     baseRating, 13.3f,
                [],
                false, null,
                "A dark form that morphs between a circle and a four-point star, shifting colour."),

            new(IceType.Blaster,      baseRating, 12.6f,
                [ProgramName.Deception],
                false, null,
                "An orange and black explosion."),

            new(IceType.Killer,       baseRating, 10.2f,
                [ProgramName.Deception],
                false, null,
                "A blue-grey sphere with electrical current circling around it."),

            new(IceType.TarPaper,     baseRating,  6.7f,
                [],
                true, IceType.Access,   // Tar types always hide; Access is the default front
                "Brownish, bubbling tar."),

            new(IceType.TarPit,       baseRating,  8.9f,
                [],
                true, IceType.Access,
                "An orange circle with tar bubbling inside."),

            new(IceType.TraceAndBurn, baseRating,  6.4f,
                [ProgramName.Deception, ProgramName.Relocate],
                false, null,
                "A dark cylindrical base with a spherical probe topped with a flame."),

            new(IceType.TraceAndDump, baseRating,  5.9f,
                [ProgramName.Deception, ProgramName.Relocate],
                false, null,
                "A dark cylindrical base with a spherical probe topped with a plume of smoke."),
        }.AsReadOnly();
    }

    // ── Display ───────────────────────────────────────────────────────────────

    public override string ToString() =>
        $"[IceSpec] {Type} Rating:{BaseRating} Hidden:{IsHidden} " +
        $"Weak:[{string.Join(", ", WeakAgainst)}]";
}
