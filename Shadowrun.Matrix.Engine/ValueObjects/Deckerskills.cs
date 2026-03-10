namespace Shadowrun.Matrix.ValueObjects;

/// <summary>
/// Groups all six skill ratings for a Decker.
/// Each skill is an integer 1–12.
///
/// The three Matrix-relevant skills:
/// <list type="bullet">
///   <item><b>Computer</b> — most important; affects program success, data find rate,
///         detection frequency.</item>
///   <item><b>Combat</b>   — attack accuracy and damage inside the Matrix.</item>
///   <item><b>Negotiation</b> — reduces program and deck purchase prices.</item>
/// </list>
/// </summary>
public class DeckerSkills
{
    // ── Skills ────────────────────────────────────────────────────────────────

    /// <summary>
    /// The single most important Matrix skill.
    /// Effects:
    /// <list type="number">
    ///   <item>Makes all programs more effective (fewer failures, more damage).</item>
    ///   <item>Increases data-find rate when searching DS nodes.</item>
    ///   <item>Reduces system detection frequency — critical for data runs.</item>
    /// </list>
    /// Minimum recommended for consistent data runs: 5–6.
    /// </summary>
    public int Computer     { get; internal set; }

    /// <summary>
    /// Improves attack accuracy and damage dealt to ICE.
    /// Important when fighting high-rated Barrier or BlackIce with the Attack program.
    /// </summary>
    public int Combat       { get; internal set; }

    /// <summary>
    /// Reduces prices when purchasing programs, decks, and upgrades.
    /// Each point above 2 reduces cost by ~3.125% of base price.
    /// </summary>
    public int Negotiation  { get; internal set; }

    /// <summary>General street-wise skill — not Matrix-specific.</summary>
    public int Charisma     { get; internal set; }

    /// <summary>Controls magic spell use — not applicable to a Decker archetype.</summary>
    public int Magic        { get; internal set; }

    /// <summary>Raw physical capability — not applicable to Matrix operations.</summary>
    public int Strength     { get; internal set; }

    // ── Constants ─────────────────────────────────────────────────────────────

    public const int MinSkill = 1;
    public const int MaxSkill = 12;

    // ── Construction ─────────────────────────────────────────────────────────

    public DeckerSkills(
        int computer    = 1,
        int combat      = 1,
        int negotiation = 0,
        int charisma    = 1,
        int magic       = 0,
        int strength    = 1)
    {
        Validate(computer,    nameof(computer),    allowZero: false);
        Validate(combat,      nameof(combat),      allowZero: false);
        Validate(negotiation, nameof(negotiation), allowZero: true);
        Validate(charisma,    nameof(charisma),    allowZero: false);
        Validate(magic,       nameof(magic),       allowZero: true);
        Validate(strength,    nameof(strength),    allowZero: false);

        Computer    = computer;
        Combat      = combat;
        Negotiation = negotiation;
        Charisma    = charisma;
        Magic       = magic;
        Strength    = strength;
    }

    // ── Validation ────────────────────────────────────────────────────────────

    private static void Validate(int value, string name, bool allowZero)
    {
        int min = allowZero ? 0 : MinSkill;
        if (value < min || value > MaxSkill)
            throw new ArgumentOutOfRangeException(name,
                $"{name} must be {min}–{MaxSkill}.");
    }

    // ── Display ───────────────────────────────────────────────────────────────

    public override string ToString() =>
        $"[Skills] Comp:{Computer} Combat:{Combat} Negot:{Negotiation} " +
        $"Cha:{Charisma} Mag:{Magic} Str:{Strength}";
}
