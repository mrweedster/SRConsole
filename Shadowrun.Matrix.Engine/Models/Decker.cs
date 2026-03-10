using Shadowrun.Matrix.Core;
using Shadowrun.Matrix.ValueObjects;

namespace Shadowrun.Matrix.Models;

/// <summary>
/// The real-world character who operates a Cyberdeck and jacks into the Matrix.
///
/// Tracks two parallel health pools:
/// <list type="bullet">
///   <item><b>Physical health</b> — the body. Reduced by BlackIce. Reaching zero
///         causes unconsciousness.</item>
///   <item><b>Mental health</b> — morale / psychological state. Reduced by some
///         magical attacks in the real world. Also reaching zero causes
///         unconsciousness.</item>
/// </list>
///
/// Key Matrix-relevant skill properties are surfaced as named shortcuts
/// (<see cref="ComputerSkill"/>, <see cref="CombatSkill"/>,
/// <see cref="NegotiationSkill"/>) for convenience in formula code.
///
/// A Decker may own one active deck at a time. When purchasing a new deck,
/// call <see cref="SwapDeck"/> — programs and data files transfer automatically.
/// </summary>
public class Decker
{
    // ── Identity ──────────────────────────────────────────────────────────────

    public string Id   { get; } = Guid.NewGuid().ToString("N");
    public string Name { get; }

    // ── Health ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Current physical health. Depleted by BlackIce during Matrix combat.
    /// If this reaches zero the Decker falls unconscious.
    /// </summary>
    public float PhysicalHealth    { get; private set; }

    public float PhysicalHealthMax { get; private set; }

    /// <summary>
    /// Whether the Decker is currently unconscious (physical or mental health depleted).
    /// Unconscious Deckers cannot jack in.
    /// </summary>
    public bool IsUnconscious => PhysicalHealth <= 0f || MentalHealth <= 0f;

    /// <summary>
    /// Current mental health. Reduced by some real-world magical effects.
    /// </summary>
    public float MentalHealth    { get; private set; }

    public float MentalHealthMax { get; private set; }

    // ── Economy ───────────────────────────────────────────────────────────────

    /// <summary>Available funds in nuyen (¥).</summary>
    public int Nuyen { get; private set; }

    // ── Skills ────────────────────────────────────────────────────────────────

    public DeckerSkills Skills { get; }

    /// <summary>Shortcut: the Computer skill, the most important Matrix stat.</summary>
    public int ComputerSkill    => Skills.Computer;

    /// <summary>Shortcut: Combat skill, affects attack accuracy and damage inside the Matrix.</summary>
    public int CombatSkill      => Skills.Combat;

    /// <summary>Shortcut: Negotiation skill, reduces purchase prices.</summary>
    public int NegotiationSkill => Skills.Negotiation;

    // ── Equipment ─────────────────────────────────────────────────────────────

    /// <summary>
    /// The Cyberdeck the Decker currently owns.
    /// Never null after construction — the Decker always has at least a starter deck.
    /// </summary>
    public Cyberdeck Deck { get; private set; }

    // ── Notebook ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Plot-relevant data files accumulated during Matrix runs.
    /// Files are moved here automatically from the deck on jack-out when
    /// <see cref="DataFile.IsPlotRelevant"/> is true.
    /// </summary>
    public IReadOnlyList<DataFile> Notebook => _notebook.AsReadOnly();

    private readonly List<DataFile> _notebook = [];

    // ── Session state ─────────────────────────────────────────────────────────

    /// <summary>The Persona the Decker is currently controlling. Null when not jacked in.</summary>
    public Persona? ActivePersona { get; private set; }

    /// <summary>Whether the Decker is currently jacked into the Matrix.</summary>
    public bool IsJackedIn => ActivePersona is not null;

    // ── Construction ─────────────────────────────────────────────────────────

    /// <param name="name">Character name.</param>
    /// <param name="deck">Starting Cyberdeck.</param>
    /// <param name="skills">Initial skill ratings.</param>
    /// <param name="physicalHealthMax">Starting and maximum physical health.</param>
    /// <param name="mentalHealthMax">Starting and maximum mental health.</param>
    /// <param name="startingNuyen">Starting funds.</param>
    public Decker(
        string        name,
        Cyberdeck     deck,
        DeckerSkills  skills,
        float         physicalHealthMax = 100f,
        float         mentalHealthMax   = 100f,
        int           startingNuyen     = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(deck);
        ArgumentNullException.ThrowIfNull(skills);

        if (physicalHealthMax <= 0f)
            throw new ArgumentException("Physical health max must be positive.", nameof(physicalHealthMax));
        if (mentalHealthMax <= 0f)
            throw new ArgumentException("Mental health max must be positive.",   nameof(mentalHealthMax));
        if (startingNuyen < 0)
            throw new ArgumentException("Starting nuyen cannot be negative.",    nameof(startingNuyen));

        Name              = name;
        Deck              = deck;
        Skills            = skills;
        PhysicalHealthMax = physicalHealthMax;
        PhysicalHealth    = physicalHealthMax;
        MentalHealthMax   = mentalHealthMax;
        MentalHealth      = mentalHealthMax;
        Nuyen             = startingNuyen;
    }

    // ── Jack-in / jack-out ────────────────────────────────────────────────────

    /// <summary>
    /// Creates and activates a new <see cref="Persona"/> for a Matrix session.
    /// Returns a failed <see cref="Core.Result"/> if the Decker cannot jack in.
    /// </summary>
    /// <param name="entryNodeId">The Node ID the Persona will start at (SAN or IOP).</param>
    /// <param name="rng">Optional seeded random for the Persona's rolls.</param>
    public Result<Persona> JackIn(string entryNodeId, Random? rng = null)
    {
        if (IsJackedIn)
            return Result.Fail<Persona>("Decker is already jacked in.");

        if (IsUnconscious)
            return Result.Fail<Persona>("Decker is unconscious and cannot jack in.");

        if (Deck.IsBroken)
            return Result.Fail<Persona>(
                "Cyberdeck MPCP is damaged. Take it to a computer shop before jacking in.");

        var persona    = new Persona(this, Deck, entryNodeId, rng);
        ActivePersona  = persona;
        return Result.Ok(persona);
    }

    /// <summary>
    /// Deactivates the current session's Persona. Moves any plot-relevant data
    /// files from the deck to the <see cref="Notebook"/>.
    /// </summary>
    /// <param name="jackOutResult">The result of the persona's final jack-out call.</param>
    public void JackOut(JackOutResult jackOutResult)
    {
        if (!IsJackedIn)
            throw new InvalidOperationException("Decker is not currently jacked in.");

        ActivePersona = null;

        // Move plot files to notebook
        foreach (DataFile file in Deck.DataFiles.Where(f => f.IsPlotRelevant).ToList())
        {
            _notebook.Add(file);
            Deck.RemoveDataFile(file);
        }
    }

    // ── Health management ─────────────────────────────────────────────────────

    /// <summary>
    /// Applies physical damage. Called by the Persona when struck by BlackIce,
    /// or by jack-out penalty from BlackIce blocking.
    /// </summary>
    public void ReceivePhysicalDamage(float amount)
    {
        if (amount < 0f)
            throw new ArgumentOutOfRangeException(nameof(amount), "Damage must be non-negative.");

        PhysicalHealth = Math.Max(0f, PhysicalHealth - amount);
    }

    /// <summary>
    /// Applies mental damage.
    /// </summary>
    public void ReceiveMentalDamage(float amount)
    {
        if (amount < 0f)
            throw new ArgumentOutOfRangeException(nameof(amount), "Damage must be non-negative.");

        MentalHealth = Math.Max(0f, MentalHealth - amount);
    }

    /// <summary>
    /// Heals physical health up to the maximum. Called after rest or medkit use.
    /// </summary>
    public void HealPhysical(float amount)
    {
        if (amount < 0f)
            throw new ArgumentOutOfRangeException(nameof(amount), "Heal amount must be non-negative.");

        PhysicalHealth = Math.Min(PhysicalHealthMax, PhysicalHealth + amount);
    }

    /// <summary>
    /// Heals mental health up to the maximum.
    /// </summary>
    public void HealMental(float amount)
    {
        if (amount < 0f)
            throw new ArgumentOutOfRangeException(nameof(amount), "Heal amount must be non-negative.");

        MentalHealth = Math.Min(MentalHealthMax, MentalHealth + amount);
    }

    // ── Skill upgrades ────────────────────────────────────────────────────────

    /// <summary>
    /// Raises the Computer skill. Each point above the current value costs Karma.
    /// Validates the new value is strictly higher and within 1–12.
    /// </summary>
    public Result UpgradeComputerSkill(int newValue)
    {
        if (newValue <= Skills.Computer)
            return Result.Fail($"New Computer skill ({newValue}) must exceed current ({Skills.Computer}).");
        if (newValue > DeckerSkills.MaxSkill)
            return Result.Fail($"Computer skill cannot exceed {DeckerSkills.MaxSkill}.");

        Skills.Computer = newValue;
        return Result.Ok();
    }

    /// <summary>
    /// Raises the Combat skill.
    /// </summary>
    public Result UpgradeCombatSkill(int newValue)
    {
        if (newValue <= Skills.Combat)
            return Result.Fail($"New Combat skill ({newValue}) must exceed current ({Skills.Combat}).");
        if (newValue > DeckerSkills.MaxSkill)
            return Result.Fail($"Combat skill cannot exceed {DeckerSkills.MaxSkill}.");

        Skills.Combat = newValue;
        return Result.Ok();
    }

    /// <summary>
    /// Raises the Negotiation skill.
    /// </summary>
    public Result UpgradeNegotiationSkill(int newValue)
    {
        if (newValue < Skills.Negotiation)
            return Result.Fail($"New Negotiation skill ({newValue}) must exceed current ({Skills.Negotiation}).");
        if (newValue > DeckerSkills.MaxSkill)
            return Result.Fail($"Negotiation skill cannot exceed {DeckerSkills.MaxSkill}.");

        Skills.Negotiation = newValue;
        return Result.Ok();
    }

    // ── Economy ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Adds nuyen to the Decker's funds (payment, data sale proceeds).
    /// </summary>
    public void AddNuyen(int amount)
    {
        if (amount < 0)
            throw new ArgumentException("Amount must be non-negative.", nameof(amount));
        Nuyen += amount;
    }

    /// <summary>
    /// Deducts nuyen from the Decker's funds.
    /// Returns a failed <see cref="Result"/> if insufficient funds.
    /// </summary>
    public Result SpendNuyen(int amount)
    {
        if (amount < 0)
            throw new ArgumentException("Amount must be non-negative.", nameof(amount));
        if (amount > Nuyen)
            return Result.Fail($"Insufficient funds: need {amount}¥, have {Nuyen}¥.");

        Nuyen -= amount;
        return Result.Ok();
    }

    // ── Deck management ───────────────────────────────────────────────────────

    /// <summary>
    /// Replaces the active deck with <paramref name="newDeck"/>, transferring
    /// all programs and data files from the old deck automatically.
    /// Returns a failed <see cref="Result"/> if the Decker is currently jacked in
    /// or if the transfer is only partially successful.
    /// </summary>
    public Result SwapDeck(Cyberdeck newDeck)
    {
        ArgumentNullException.ThrowIfNull(newDeck);

        if (IsJackedIn)
            return Result.Fail("Cannot swap deck while jacked in.");

        Result transferResult = Deck.TransferContentsTo(newDeck);

        Deck = newDeck;

        return transferResult.IsFailure
            ? Result.Fail($"Deck swapped but transfer was incomplete: {transferResult.Error}")
            : Result.Ok();
    }

    // ── Data files ────────────────────────────────────────────────────────────

    /// <summary>
    /// Adds a downloaded data file to the deck. If the file is plot-relevant
    /// it is moved immediately to the <see cref="Notebook"/> instead.
    /// </summary>
    public Result ReceiveDataFile(DataFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (file.IsPlotRelevant)
        {
            _notebook.Add(file);
            return Result.Ok();
        }

        return Deck.AddDataFile(file);
    }

    // ── Notebook ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the notebook entry with the given ID, or null if not found.
    /// </summary>
    public DataFile? FindNotebookEntry(string fileId) =>
        _notebook.FirstOrDefault(f => f.Id == fileId);

    // ── Display ───────────────────────────────────────────────────────────────

    public override string ToString()
    {
        string status = IsJackedIn    ? " [JACKED IN]"
                      : IsUnconscious ? " [UNCONSCIOUS]"
                      : "";

        return $"[Decker] {Name}{status} " +
               $"HP:{PhysicalHealth:F0}/{PhysicalHealthMax:F0} " +
               $"MP:{MentalHealth:F0}/{MentalHealthMax:F0} " +
               $"¥{Nuyen} | {Skills} | Deck:{Deck.Name}";
    }
}
