using Shadowrun.Matrix.ValueObjects;

namespace Shadowrun.Matrix.Data;

// ── Purchaseable item models ──────────────────────────────────────────────────

/// <summary>A cyberdeck available for purchase in the Black Market.</summary>
public sealed record DeckListing(
    string   Name,
    int      Price,
    /// <summary>
    /// Fixed nuyen subtracted per Negotiation skill level above 2.
    /// Formula: effectivePrice = Price - DiscountPerNegLevel * max(0, neg - 2).
    /// Derived directly from the Shadowrun Genesis source tables.
    /// </summary>
    int      DiscountPerNegLevel,
    int      Mpcp,
    int      Hardening,
    int      Response,
    int      Memory,
    int      MemoryMax,
    int      Storage,
    int      StorageMax,
    int      LoadIoSpeed,
    int      LoadIoSpeedMax,
    int      Bod,
    int      Evasion,
    int      Masking,
    int      Sensor,
    string   Description)
{
    public DeckStats ToDeckStats() => new(
        mpcp:            Mpcp,
        hardening:       Hardening,
        response:        Response,
        memory:          Memory,
        memoryMax:       MemoryMax,
        storage:         Storage,
        storageMax:      StorageMax,
        loadIoSpeed:     LoadIoSpeed,
        loadIoSpeedMax:  LoadIoSpeedMax,
        bod:             Bod,
        evasion:         Evasion,
        masking:         Masking,
        sensor:          Sensor);
}

/// <summary>A program available for purchase in the Black Market.</summary>
public sealed record ProgramListing(
    string ProgramName,
    int    Level,
    int    Price,
    int    DiscountPerNegLevel,
    int    SizeInMp,
    string Category,     // "Combat", "Defense", "Stealth", "Utility"
    string Description);

/// <summary>A cyberdeck upgrade (single stat boost) available for purchase.</summary>
public sealed record UpgradeListing(
    string StatName,
    string Description,
    int    CostPerPoint,
    int    MaxPoints);

// ── Catalog ───────────────────────────────────────────────────────────────────

/// <summary>
/// Black Market catalog — all purchaseable cyberdecks, programs, and upgrades.
/// Data sourced from Shadowrun Genesis game reference (walkthrough data).
/// </summary>
public static class BlackMarketCatalog
{
    // ── Cyberdecks ────────────────────────────────────────────────────────────

    /// <summary>
    /// The Allegiance Alpha — every new decker's starting deck.
    /// Not purchaseable; stats sourced directly from the Shadowrun Genesis table.
    /// Memory/MemMax 30/120, Storage/StorMax 100/250, LoadIO/Max 10/30,
    /// Hardening 0, Response 0, MPCP 3.
    /// </summary>
    public static DeckListing StarterDeck { get; } = new(
        Name:                "Allegiance Alpha",
        Price:               0,
        DiscountPerNegLevel: 0,
        Mpcp:                3,
        Hardening:           0,
        Response:            0,
        Memory:              30,
        MemoryMax:           120,
        Storage:             100,
        StorageMax:          250,
        LoadIoSpeed:         10,
        LoadIoSpeedMax:      30,
        Bod:                 0,
        Evasion:             0,
        Masking:             2,
        Sensor:              0,
        Description:         "Entry-level street deck issued to new runners. Reliable but limited ceiling.");

    /// <summary>
    /// All cyberdecks available for purchase, sorted by price.
    /// Stats: Memory/MemoryMax, Storage/StorageMax, Load/IO Speed, H=Hardening,
    ///        R=Response, MP=MPCP, Bod/Evasion/Masking/Sensor.
    /// The Allegiance Alpha (starting deck) is not listed here — see <see cref="StarterDeck"/>.
    /// </summary>
    public static IReadOnlyList<DeckListing> Decks { get; } = new List<DeckListing>
    {
        new(
            Name:                "Cyber Shack PCD-500",
            Price:               5_000,
            DiscountPerNegLevel: 156,        // 5,000 → 3,440 over neg 3-12
            Mpcp:                4,
            Hardening:           1,
            Response:            0,
            Memory:              50,
            MemoryMax:           160,
            Storage:             100,
            StorageMax:          325,
            LoadIoSpeed:         20,
            LoadIoSpeedMax:      40,
            Bod:                 1,
            Evasion:             0,
            Masking:             3,
            Sensor:              0,
            Description:         "Entry-level street deck. Reliable, upgradeable, limited ceiling."),

        new(
            Name:                "Fuchi Cyber-5",
            Price:               25_000,
            DiscountPerNegLevel: 781,        // 25,000 → 17,190 over neg 3-12
            Mpcp:                6,
            Hardening:           2,
            Response:            1,
            Memory:              100,
            MemoryMax:           240,
            Storage:             500,
            StorageMax:          500,
            LoadIoSpeed:         20,
            LoadIoSpeedMax:      60,
            Bod:                 2,
            Evasion:             1,
            Masking:             5,
            Sensor:              1,
            Description:         "Mid-tier corp deck. Good balance of speed and capacity."),

        new(
            Name:                "SEGA CTY-360",
            Price:               60_000,
            DiscountPerNegLevel: 1_875,      // 60,000 → 41,250 over neg 3-12
            Mpcp:                8,
            Hardening:           3,
            Response:            1,
            Memory:              200,
            MemoryMax:           320,
            Storage:             500,
            StorageMax:          650,
            LoadIoSpeed:         50,
            LoadIoSpeedMax:      80,
            Bod:                 3,
            Evasion:             1,
            Masking:             6,
            Sensor:              2,
            Description:         "Solid mid-high deck. Punches above its price in storage and speed."),

        new(
            Name:                "Fuchi Cyber-7",
            Price:               125_000,
            DiscountPerNegLevel: 4_880,      // 125,000 → 76,200 over neg 3-12
            Mpcp:                10,
            Hardening:           4,
            Response:            2,
            Memory:              300,
            MemoryMax:           400,
            Storage:             1_000,
            StorageMax:          1_000,
            LoadIoSpeed:         50,
            LoadIoSpeedMax:      100,
            Bod:                 4,
            Evasion:             2,
            Masking:             8,
            Sensor:              3,
            Description:         "High-end Fuchi flagship. Excellent all-around performance."),

        new(
            Name:                "Fairlight Excalibur",
            Price:               250_000,
            DiscountPerNegLevel: 9_760,      // 250,000 → 152,400 over neg 3-12
            Mpcp:                12,
            Hardening:           5,
            Response:            3,
            Memory:              500,
            MemoryMax:           500,
            Storage:             1_000,
            StorageMax:          1_000,
            LoadIoSpeed:         100,
            LoadIoSpeedMax:      120,
            Bod:                 5,
            Evasion:             4,
            Masking:             10,
            Sensor:              4,
            Description:         "The best money can buy. Rumoured to have no equal in the Matrix."),
    };

    // ── Programs ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Programs available for purchase, covering levels 1–6.
    /// Size/price follow the Small/Medium/Large formula from the game.
    /// Small: size = 2*L², price = 30*L²
    /// Medium: size = 3*L², price = 45*L²
    /// Large: size = 4*L², price = 60*L²  (approximations)
    /// </summary>
    public static IReadOnlyList<ProgramListing> Programs { get; } = BuildPrograms();

    private static List<ProgramListing> BuildPrograms()
    {
        var list = new List<ProgramListing>();

        // ── Small programs (size = 2·L²) ─────────────────────────────────────
        // Discounts per neg level above 2 (from Genesis source table):
        int[] smallDisc   = [1, 15, 50, 120, 234, 405, 643, 960];
        int[] smallSizes  = [2, 8, 18, 32, 50, 72, 98, 128];
        int[] smallPrices = [60, 480, 1_620, 3_840, 7_500, 12_960, 20_580, 30_720];

        // ── Medium programs (size = 3·L²) ────────────────────────────────────
        int[] medDisc   = [2, 22, 75, 180, 351, 607, 964, 1_440];
        int[] medSizes  = [3, 12, 27, 48, 75, 108, 143, 192];
        int[] medPrices = [90, 720, 2_430, 5_760, 11_250, 19_440, 30_870, 46_080];

        // ── Large programs (size = 4·L²) ─────────────────────────────────────
        int[] largeDisc   = [3, 30, 101, 240, 468, 810, 1_286, 1_920];
        int[] largeSizes  = [4, 16, 36, 64, 100, 144, 196, 256];
        int[] largePrices = [120, 960, 3_240, 7_680, 15_000, 25_920, 41_160, 61_440];

        // Attack (Combat — Medium)
        AddLevels(list, "Attack",    "Combat",  "Medium", medDisc, medSizes, medPrices,
            "Deals direct damage to ICE. Primary offensive tool.");

        // Armor (Defense — Medium)
        AddLevels(list, "Armor",     "Defense", "Medium", medDisc, medSizes, medPrices,
            "Reduces incoming ICE damage. Stack with Shield for best results.");

        // Medic (Defense — Small)
        AddLevels(list, "Medic",     "Defense", "Small",  smallDisc, smallSizes, smallPrices,
            "Restores Persona energy. Use before engaging ICE.");

        // Shield (Defense — Small)
        AddLevels(list, "Shield",    "Defense", "Small",  smallDisc, smallSizes, smallPrices,
            "Passive damage reduction. Run once; effect lingers until combat ends.");

        // Deception (Stealth — Small)
        AddLevels(list, "Deception", "Stealth", "Small",  smallDisc, smallSizes, smallPrices,
            "Bypasses ICE before combat. Useless once combat is engaged.");

        // Sleaze (Stealth — Medium)
        AddLevels(list, "Sleaze",    "Stealth", "Medium", medDisc, medSizes, medPrices,
            "Bypasses a Node without defeating its ICE. Works on all ICE types including Barrier and BlackIce. ICE persists — you'll need to deal with it again on any revisit.");

        // Relocate (Stealth — Small)
        AddLevels(list, "Relocate",  "Stealth", "Small",  smallDisc, smallSizes, smallPrices,
            "Disrupts Trace probes in flight. Counters Trace & Dump / Trace & Burn.");

        // Analyze (Utility — Small)
        AddLevels(list, "Analyze",   "Utility", "Small",  smallDisc, smallSizes, smallPrices,
            "Reveals ICE type, rating, and weaknesses before engaging.");

        // Smoke (Utility — Large)
        AddLevels(list, "Smoke",     "Utility", "Large",  largeDisc, largeSizes, largePrices,
            "Emergency escape: allows jack-out past active Black ICE.");

        return list;
    }

    private static void AddLevels(
        List<ProgramListing> list,
        string programName, string category, string size,
        int[] discounts, int[] sizes, int[] prices,
        string description)
    {
        for (int i = 0; i < sizes.Length; i++)
        {
            list.Add(new ProgramListing(
                ProgramName:          programName,
                Level:                i + 1,
                Price:                prices[i],
                DiscountPerNegLevel:  discounts[i],
                SizeInMp:             sizes[i],
                Category:             category,
                Description:          description));
        }
    }

    // ── Upgrades ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Cyberdeck stat upgrades purchaseable at the Black Market.
    /// StatName must exactly match a value in the <see cref="Shadowrun.Matrix.Enums.UpgradableStat"/> enum.
    /// </summary>
    public static IReadOnlyList<UpgradeListing> Upgrades { get; } = new List<UpgradeListing>
    {
        new("Response",      "Improves program execution speed and success rates across the board.",                  CostPerPoint: 10_000, MaxPoints: 4),
        new("Bod",           "Raw durability — raises Persona max energy. Synergises with Medic programs.",          CostPerPoint:  5_000, MaxPoints: 6),
        new("Evasion",       "Dodge chance against ICE attacks. Reduces frequency of hits taken.",                   CostPerPoint:  7_000, MaxPoints: 6),
        new("Masking",       "Stealth multiplier — makes the Persona harder for ICE to detect.",                     CostPerPoint:  9_000, MaxPoints: 8),
        new("Sensor",        "Enhances node-detection range and Analyze program effectiveness.",                     CostPerPoint:  6_000, MaxPoints: 4),
        new("Memory",        "Expands active program slots (in Mp). Required for loading more/bigger programs.",     CostPerPoint:     25, MaxPoints: 200),
        new("MemoryMax",     "Raises the ceiling Memory can be upgraded to.",                                        CostPerPoint:     40, MaxPoints: 100),
        new("Storage",       "Increases deck storage capacity. Required for collecting datafiles and more programs.", CostPerPoint:     10, MaxPoints: 500),
        new("StorageMax",    "Raises the ceiling Storage can be upgraded to.",                                       CostPerPoint:     15, MaxPoints: 500),
        new("LoadIoSpeed",   "Reduces program loading time. Critical when swapping programs mid-run.",               CostPerPoint:  3_000, MaxPoints: 50),
        new("LoadIoSpeedMax","Raises the ceiling Load/IO Speed can be upgraded to.",                                 CostPerPoint:  4_000, MaxPoints: 30),
    };
}
