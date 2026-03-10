using Shadowrun.Matrix.Core;

namespace Shadowrun.Matrix.ValueObjects;

/// <summary>
/// Holds all numeric statistics for a Cyberdeck.
///
/// The stats are divided into three groups:
/// <list type="bullet">
///   <item><b>Base values</b> — set at manufacture; can never be changed (MPCP, Hardening).</item>
///   <item><b>Hardware stats</b> — upgradeable up to the deck's MPCP rating (Memory, Storage, Load/IO Speed, Response).</item>
///   <item><b>Attributes</b> — upgradeable up to the deck's MPCP rating (Bod, Evasion, Masking, Sensor).</item>
/// </list>
///
/// All upgrade attempts must go through <c>Cyberdeck.UpgradeStat()</c>, which
/// validates against the MPCP cap and deck-specific maximums before mutating this object.
/// Direct property mutation is intentionally restricted to the <c>Shadowrun.Matrix</c> assembly.
/// </summary>
public class DeckStats
{
    // ── Base values (immutable after construction) ────────────────────────────

    /// <summary>
    /// Master Persona Control Program rating. Immutable. Sets the ceiling for
    /// all upgradeable stats — no stat may be raised above this value.
    /// The most important single number on a deck.
    /// </summary>
    public int Mpcp { get; }

    /// <summary>
    /// Intrinsic defensive power of the deck hardware. Immutable.
    /// Think of it as natural toughness before armor (Bod) is factored in.
    /// </summary>
    public int Hardening { get; }

    // ── Hardware stats (upgradeable) ─────────────────────────────────────────

    /// <summary>
    /// Broadly affects how quickly the Persona perceives and interacts with
    /// cyberspace: running speed, program loading, utility refresh rate.
    /// Hard capped at 3 across all decks regardless of MPCP.
    /// </summary>
    public int Response { get; internal set; }

    /// <summary>Current loaded-program capacity in Megapulses.</summary>
    public int Memory    { get; internal set; }

    /// <summary>Maximum memory capacity this deck can be upgraded to.</summary>
    public int MemoryMax { get; internal set; }

    /// <summary>
    /// Total storage in Megapulses. Consumed by both installed programs
    /// and downloaded data files.
    /// </summary>
    public int Storage    { get; internal set; }

    /// <summary>Maximum storage capacity this deck can be upgraded to.</summary>
    public int StorageMax { get; internal set; }

    /// <summary>
    /// Controls how quickly programs load into memory while inside the Matrix.
    /// Programs loaded before jacking in start fully loaded regardless of this value.
    /// </summary>
    public int LoadIoSpeed    { get; internal set; }

    /// <summary>Maximum Load/IO Speed this deck can be upgraded to.</summary>
    public int LoadIoSpeedMax { get; internal set; }

    // ── Attributes (upgradeable, capped at MPCP) ─────────────────────────────

    /// <summary>
    /// Effective armor rating while in the Matrix.
    /// Distinct from Hardening: Hardening is the deck's natural toughness;
    /// Bod is the equivalent of equipped armor on top of that.
    /// </summary>
    public int Bod { get; internal set; }

    /// <summary>
    /// Controls the Persona's chance to dodge incoming ICE attacks.
    /// Higher values cause ICE attacks to miss more often.
    /// </summary>
    public int Evasion { get; internal set; }

    /// <summary>
    /// The most important upgradeable attribute.
    /// (a) Slows system detection of the Persona's actions.
    /// (b) Directly multiplies the success chance of the Deception program.
    /// Invest here first.
    /// </summary>
    public int Masking { get; internal set; }

    /// <summary>
    /// Improves the success chance of the Analyze program.
    /// Limited single-use applicability — upgrade last if at all.
    /// </summary>
    public int Sensor { get; internal set; }

    // ── Construction ─────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a fully specified DeckStats snapshot.
    /// Throws <see cref="ArgumentException"/> if any value violates known constraints
    /// (e.g. Response > 3, or an attribute > MPCP).
    /// </summary>
    public DeckStats(
        int mpcp,
        int hardening,
        int response,
        int memory,      int memoryMax,
        int storage,     int storageMax,
        int loadIoSpeed, int loadIoSpeedMax,
        int bod,
        int evasion,
        int masking,
        int sensor)
    {
        if (mpcp       < 1)  throw new ArgumentException("MPCP must be at least 1.",         nameof(mpcp));
        if (hardening  < 0)  throw new ArgumentException("Hardening cannot be negative.",     nameof(hardening));
        if (response   < 0 || response   > MaxResponse)
            throw new ArgumentException($"Response must be 0–{MaxResponse}.",                 nameof(response));
        if (memory     < 0)  throw new ArgumentException("Memory cannot be negative.",        nameof(memory));
        if (memoryMax  < memory)
            throw new ArgumentException("MemoryMax cannot be less than Memory.",              nameof(memoryMax));
        if (storage    < 0)  throw new ArgumentException("Storage cannot be negative.",       nameof(storage));
        if (storageMax < storage)
            throw new ArgumentException("StorageMax cannot be less than Storage.",            nameof(storageMax));
        if (loadIoSpeed    < 0) throw new ArgumentException("LoadIoSpeed cannot be negative.", nameof(loadIoSpeed));
        if (loadIoSpeedMax < loadIoSpeed)
            throw new ArgumentException("LoadIoSpeedMax cannot be less than LoadIoSpeed.",    nameof(loadIoSpeedMax));

        ValidateAttribute(bod,      nameof(bod),      mpcp);
        ValidateAttribute(evasion,  nameof(evasion),  mpcp);
        ValidateAttribute(masking,  nameof(masking),  mpcp);
        ValidateAttribute(sensor,   nameof(sensor),   mpcp);

        Mpcp          = mpcp;
        Hardening     = hardening;
        Response      = response;
        Memory        = memory;
        MemoryMax     = memoryMax;
        Storage       = storage;
        StorageMax    = storageMax;
        LoadIoSpeed    = loadIoSpeed;
        LoadIoSpeedMax = loadIoSpeedMax;
        Bod           = bod;
        Evasion       = evasion;
        Masking       = masking;
        Sensor        = sensor;
    }

    // ── Constants ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Global hard cap on Response. All decks share this ceiling regardless of MPCP.
    /// </summary>
    public const int MaxResponse = 3;

    // ── Computed helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Combined effective defense: Hardening (base) + Bod (attribute).
    /// This is the value checked against ICE attack rolls.
    /// </summary>
    public int EffectiveDefense => Hardening + Bod;

    /// <summary>
    /// Returns the current free memory in Megapulses.
    /// Callers should not load programs when this reaches zero.
    /// </summary>
    /// <param name="usedMemory">Sum of sizeInMp for all currently loaded programs.</param>
    public int FreeMemory(int usedMemory) => Math.Max(0, Memory - usedMemory);

    /// <summary>
    /// Returns the current free storage in Megapulses.
    /// </summary>
    /// <param name="usedStorage">Sum of sizeInMp for all programs + data files on the deck.</param>
    public int FreeStorage(int usedStorage) => Math.Max(0, Storage - usedStorage);

    // ── Validation helper ─────────────────────────────────────────────────────

    private static void ValidateAttribute(int value, string name, int mpcp)
    {
        if (value < 0)
            throw new ArgumentException($"{name} cannot be negative.", name);
        if (value > mpcp)
            throw new ArgumentException(
                $"{name} ({value}) cannot exceed MPCP ({mpcp}).", name);
    }

    // ── Display ───────────────────────────────────────────────────────────────

    public override string ToString() =>
        $"[DeckStats] MPCP:{Mpcp} Hard:{Hardening} Resp:{Response} " +
        $"Mem:{Memory}/{MemoryMax} Stor:{Storage}/{StorageMax} " +
        $"IO:{LoadIoSpeed}/{LoadIoSpeedMax} " +
        $"Bod:{Bod} Eva:{Evasion} Mask:{Masking} Sen:{Sensor}";
}
