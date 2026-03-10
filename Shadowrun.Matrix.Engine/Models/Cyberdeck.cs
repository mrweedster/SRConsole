using Shadowrun.Matrix.Core;
using Shadowrun.Matrix.Enums;
using Shadowrun.Matrix.ValueObjects;

namespace Shadowrun.Matrix.Models;

/// <summary>
/// The Decker's hardware interface for the Matrix.
/// Manages the five active program slots, total program storage,
/// data file inventory, and all upgradeable statistics.
///
/// Hard limits enforced by this class:
/// <list type="bullet">
///   <item>Maximum 5 programs loaded simultaneously (<see cref="MaxLoadedPrograms"/>).</item>
///   <item>Maximum 5 data files at any time (<see cref="MaxDataFiles"/>).</item>
///   <item>No stat may be upgraded beyond <see cref="DeckStats.Mpcp"/>.</item>
///   <item>Response hard-capped at <see cref="DeckStats.MaxResponse"/> (3) globally.</item>
/// </list>
/// </summary>
public class Cyberdeck
{
    // ── Constants ─────────────────────────────────────────────────────────────

    /// <summary>Maximum simultaneous loaded programs, regardless of available memory.</summary>
    public const int MaxLoadedPrograms = 5;

    /// <summary>Maximum data files the deck can hold, regardless of available storage.</summary>
    public const int MaxDataFiles = 5;

    // ── Identity ──────────────────────────────────────────────────────────────

    public string Name { get; }

    // ── Core stats ────────────────────────────────────────────────────────────

    /// <summary>
    /// All numeric statistics for this deck.
    /// Read freely; mutate only via <see cref="UpgradeStat"/>.
    /// </summary>
    public DeckStats Stats { get; }

    // ── Programs ──────────────────────────────────────────────────────────────

    /// <summary>
    /// All programs installed on the deck (loaded or unloaded).
    /// Persists when the Decker purchases a new deck.
    /// </summary>
    public IReadOnlyList<Program> Programs => _programs.AsReadOnly();

    /// <summary>
    /// The five active memory slots. A slot is <c>null</c> when empty.
    /// Ordered left-to-right; loading progresses in this order.
    /// </summary>
    public IReadOnlyList<Program?> LoadedSlots => _loadedSlots.AsReadOnly();

    private readonly List<Program>   _programs    = [];
    private readonly List<Program?>  _loadedSlots = [null, null, null, null, null];

    // ── Data files ────────────────────────────────────────────────────────────

    /// <summary>
    /// Data files currently stored on the deck.
    /// Maximum 5, also bounded by free storage.
    /// </summary>
    public IReadOnlyList<DataFile> DataFiles => _dataFiles.AsReadOnly();

    private readonly List<DataFile> _dataFiles = [];

    // ── Deck health ───────────────────────────────────────────────────────────

    /// <summary>
    /// Set to <c>true</c> when Trace &amp; Burn damages the MPCP chip on dump.
    /// While broken, the Decker cannot jack in until
    /// <see cref="RepairMpcp"/> is called (after visiting a computer shop).
    /// </summary>
    public bool IsBroken { get; private set; }

    // ── Construction ─────────────────────────────────────────────────────────

    /// <param name="name">Display name of the deck (e.g. "Fairlight Excalibur").</param>
    /// <param name="stats">Fully specified stats snapshot for this deck.</param>
    public Cyberdeck(string name, DeckStats stats)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(stats);

        Name  = name;
        Stats = stats;
    }

    // ── Stat upgrades ─────────────────────────────────────────────────────────

    /// <summary>
    /// Returns <c>true</c> if <paramref name="stat"/> can legally be raised to
    /// <paramref name="newValue"/> given this deck's current state and MPCP cap.
    /// Does not mutate any state.
    /// </summary>
    public bool CanUpgradeStat(UpgradableStat stat, int newValue)
    {
        if (newValue <= 0) return false;

        return stat switch
        {
            // Response is globally capped at 3 — not at MPCP
            UpgradableStat.Response =>
                newValue > Stats.Response && newValue <= DeckStats.MaxResponse,

            // Memory and Storage current values are bounded by their respective Max stats
            UpgradableStat.Memory =>
                newValue > Stats.Memory && newValue <= Stats.MemoryMax,

            UpgradableStat.MemoryMax =>
                newValue > Stats.MemoryMax && newValue <= Stats.Mpcp * 50, // soft guideline

            UpgradableStat.Storage =>
                newValue > Stats.Storage && newValue <= Stats.StorageMax,

            UpgradableStat.StorageMax =>
                newValue > Stats.StorageMax,

            UpgradableStat.LoadIoSpeed =>
                newValue > Stats.LoadIoSpeed && newValue <= Stats.LoadIoSpeedMax,

            UpgradableStat.LoadIoSpeedMax =>
                newValue > Stats.LoadIoSpeedMax,

            // Attributes are capped at MPCP
            UpgradableStat.Bod     => newValue > Stats.Bod     && newValue <= Stats.Mpcp,
            UpgradableStat.Evasion => newValue > Stats.Evasion && newValue <= Stats.Mpcp,
            UpgradableStat.Masking => newValue > Stats.Masking && newValue <= Stats.Mpcp,
            UpgradableStat.Sensor  => newValue > Stats.Sensor  && newValue <= Stats.Mpcp,

            _ => throw new ArgumentOutOfRangeException(nameof(stat), stat, "Unknown stat.")
        };
    }

    /// <summary>
    /// Raises <paramref name="stat"/> to <paramref name="newValue"/>.
    /// Throws <see cref="InvalidOperationException"/> if the upgrade is not legal
    /// (programming error — callers should check <see cref="CanUpgradeStat"/> first,
    /// or catch the exception in UI code).
    /// </summary>
    public void UpgradeStat(UpgradableStat stat, int newValue)
    {
        if (!CanUpgradeStat(stat, newValue))
            throw new InvalidOperationException(
                $"Cannot upgrade {stat} to {newValue}. " +
                $"MPCP:{Stats.Mpcp}, current value:{GetCurrentStatValue(stat)}.");

        switch (stat)
        {
            case UpgradableStat.Response:     Stats.Response      = newValue; break;
            case UpgradableStat.Memory:       Stats.Memory        = newValue; break;
            case UpgradableStat.MemoryMax:    Stats.MemoryMax     = newValue; break;
            case UpgradableStat.Storage:      Stats.Storage       = newValue; break;
            case UpgradableStat.StorageMax:   Stats.StorageMax    = newValue; break;
            case UpgradableStat.LoadIoSpeed:  Stats.LoadIoSpeed   = newValue; break;
            case UpgradableStat.LoadIoSpeedMax: Stats.LoadIoSpeedMax = newValue; break;
            case UpgradableStat.Bod:          Stats.Bod           = newValue; break;
            case UpgradableStat.Evasion:      Stats.Evasion       = newValue; break;
            case UpgradableStat.Masking:      Stats.Masking       = newValue; break;
            case UpgradableStat.Sensor:       Stats.Sensor        = newValue; break;
            default:
                throw new ArgumentOutOfRangeException(nameof(stat), stat, "Unknown stat.");
        }
    }

    // ── Program management ────────────────────────────────────────────────────

    /// <summary>
    /// Installs a program onto the deck (storage only — does not load into a slot).
    /// Fails if there is insufficient free storage.
    /// </summary>
    public Result InstallProgram(Program program)
    {
        ArgumentNullException.ThrowIfNull(program);

        if (_programs.Contains(program))
            return Result.Fail($"'{program.Spec.Name}' is already installed on this deck.");

        if (FreeStorage() < program.Spec.SizeInMp)
            return Result.Fail(
                $"Not enough storage to install '{program.Spec.Name}' " +
                $"({program.Spec.SizeInMp}Mp needed, {FreeStorage()}Mp free).");

        _programs.Add(program);
        return Result.Ok();
    }

    /// <summary>
    /// Loads an installed program into the next empty active memory slot.
    /// Fails if: the program is not installed, memory is full, all 5 slots are taken,
    /// or there is insufficient free memory for the program.
    /// </summary>
    /// <param name="program">The program to load.</param>
    /// <param name="midSession">
    /// <c>true</c> if the Decker is currently jacked in — the program will load
    /// progressively via <see cref="TickLoading"/>. <c>false</c> loads it instantly.
    /// </param>
    public Result LoadProgram(Program program, bool midSession = false)
    {
        ArgumentNullException.ThrowIfNull(program);

        if (!_programs.Contains(program))
            return Result.Fail(
                $"'{program.Spec.Name}' is not installed on this deck.");

        if (program.IsLoaded)
            return Result.Fail(
                $"'{program.Spec.Name}' is already loaded.");

        int emptySlotIndex = _loadedSlots.IndexOf(null);
        if (emptySlotIndex < 0)
            return Result.Fail(
                $"All {MaxLoadedPrograms} program slots are occupied.");

        if (UsedMemory() + program.Spec.SizeInMp > Stats.Memory)
            return Result.Fail(
                $"Not enough memory to load '{program.Spec.Name}' " +
                $"({program.Spec.SizeInMp}Mp needed, {FreeMemory()}Mp free).");

        program.BeginLoad(midSession);
        _loadedSlots[emptySlotIndex] = program;
        return Result.Ok();
    }

    /// <summary>
    /// Unloads a program from its active slot, freeing the memory.
    /// Throws <see cref="ArgumentOutOfRangeException"/> for an invalid slot index
    /// (programming error). Returns a failed <see cref="Result"/> if the slot is empty.
    /// </summary>
    public Result UnloadProgram(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= MaxLoadedPrograms)
            throw new ArgumentOutOfRangeException(nameof(slotIndex),
                $"Slot index must be 0–{MaxLoadedPrograms - 1}.");

        Program? program = _loadedSlots[slotIndex];
        if (program is null)
            return Result.Fail($"Slot {slotIndex} is already empty.");

        program.Unload();
        _loadedSlots[slotIndex] = null;
        return Result.Ok();
    }

    /// <summary>
    /// Permanently removes a program from the deck — both from storage and from
    /// its active slot if loaded. This is the effect of a Tar Pit hit.
    ///
    /// This is the <b>only</b> way to delete a program from a deck in-game.
    /// </summary>
    public void DeleteProgram(Program program)
    {
        ArgumentNullException.ThrowIfNull(program);

        if (!_programs.Contains(program))
            throw new InvalidOperationException(
                $"Program '{program.Spec.Name}' is not on this deck.");

        // Remove from slot if loaded
        int slotIndex = _loadedSlots.IndexOf(program);
        if (slotIndex >= 0)
        {
            program.Unload();
            _loadedSlots[slotIndex] = null;
        }

        _programs.Remove(program);
    }

    // ── Data file management ──────────────────────────────────────────────────

    /// <summary>
    /// Adds a downloaded data file to the deck.
    /// Returns a failed <see cref="Result"/> if either the 5-file cap or the
    /// available storage limit would be exceeded.
    /// </summary>
    public Result AddDataFile(DataFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (_dataFiles.Count >= MaxDataFiles)
            return Result.Fail(
                $"Deck is already holding the maximum of {MaxDataFiles} data files.");

        if (FreeStorage() < file.SizeInMp)
            return Result.Fail(
                $"Not enough storage for '{file.Name}' " +
                $"({file.SizeInMp}Mp needed, {FreeStorage()}Mp free).");

        _dataFiles.Add(file);
        return Result.Ok();
    }

    /// <summary>
    /// Removes a data file from the deck (e.g. after sale or notebook transfer).
    /// Throws if the file is not present.
    /// </summary>
    public void RemoveDataFile(DataFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (!_dataFiles.Remove(file))
            throw new InvalidOperationException(
                $"Data file '{file.Name}' is not on this deck.");
    }

    // ── Storage / memory helpers ──────────────────────────────────────────────

    /// <summary>Megapulses currently occupied by all installed programs.</summary>
    public int UsedProgramStorage() =>
        _programs.Sum(p => p.Spec.SizeInMp);

    /// <summary>Megapulses currently occupied by all stored data files.</summary>
    public int UsedDataStorage() =>
        _dataFiles.Sum(f => f.SizeInMp);

    /// <summary>Total storage consumed (programs + data files).</summary>
    public int UsedStorage() => UsedProgramStorage() + UsedDataStorage();

    /// <summary>Free storage remaining in Megapulses.</summary>
    public int FreeStorage() => Math.Max(0, Stats.Storage - UsedStorage());

    /// <summary>Megapulses currently occupied by loaded programs.</summary>
    public int UsedMemory() =>
        _loadedSlots.Where(s => s is not null).Sum(s => s!.Spec.SizeInMp);

    /// <summary>Free memory remaining in Megapulses.</summary>
    public int FreeMemory() => Math.Max(0, Stats.Memory - UsedMemory());

    /// <summary>Number of currently occupied active program slots.</summary>
    public int OccupiedSlotCount() => _loadedSlots.Count(s => s is not null);

    /// <summary>Number of currently empty active program slots.</summary>
    public int FreeSlotCount() => MaxLoadedPrograms - OccupiedSlotCount();

    // ── Deck health ───────────────────────────────────────────────────────────

    /// <summary>
    /// Marks the deck as broken after a Trace &amp; Burn MPCP damage event.
    /// The Decker cannot jack in again until <see cref="RepairMpcp"/> is called.
    /// </summary>
    internal void BreakMpcp() => IsBroken = true;

    /// <summary>
    /// Restores the deck to working order after a visit to a computer repair shop.
    /// </summary>
    public void RepairMpcp()
    {
        if (!IsBroken)
            throw new InvalidOperationException("Deck MPCP is not damaged — no repair needed.");

        IsBroken = false;
    }

    // ── Tick loop ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Advances the loading and refresh progress of all active slots.
    /// Called once per game frame / tick while the Persona is inside the Matrix.
    ///
    /// Loading rule: only the left-most incomplete slot loads at a time.
    /// Refresh rule: all loaded programs advance their refresh independently.
    /// </summary>
    /// <param name="deltaTime">Elapsed seconds since the last tick.</param>
    public void TickLoading(float deltaTime)
    {
        if (deltaTime <= 0f) return;

        // ── Drive loading for the first slot that still needs it ──────────────
        Program? loadingTarget = _loadedSlots
            .FirstOrDefault(s => s is not null && s.LoadProgress < 1.0f);

        if (loadingTarget is not null)
        {
            // Loading speed: LoadIoSpeed Mp/s; program takes SizeInMp Mp to load.
            // Progress delta = (LoadIoSpeed * deltaTime) / SizeInMp
            float loadDelta = Stats.LoadIoSpeed > 0
                ? (Stats.LoadIoSpeed * deltaTime) / loadingTarget.Spec.SizeInMp
                : 0f;

            loadingTarget.AdvanceLoading(loadDelta);
        }

        // ── Advance refresh for all loaded programs ───────────────────────────
        // Refresh speed scales with Response (higher = faster cooldown).
        // Base speed: 1 full refresh per (4 - Response) seconds, minimum 1s.
        float refreshSpeed = 1.0f / Math.Max(1f, 4f - Stats.Response);

        foreach (Program? slot in _loadedSlots)
        {
            if (slot is not null && slot.RefreshProgress < 1.0f)
                slot.AdvanceRefresh(refreshSpeed * deltaTime);
        }
    }

    // ── Deck transfer (new deck purchase) ────────────────────────────────────

    /// <summary>
    /// Transfers all programs and data files from this deck to a new one.
    /// Used when the Decker purchases a replacement deck.
    /// The source deck is left empty after the transfer.
    /// </summary>
    /// <param name="target">The new deck to transfer everything to.</param>
    public Result TransferContentsTo(Cyberdeck target)
    {
        ArgumentNullException.ThrowIfNull(target);

        // Unload everything first
        for (int i = 0; i < MaxLoadedPrograms; i++)
        {
            if (_loadedSlots[i] is not null)
                UnloadProgram(i);
        }

        // Attempt to install each program on the target
        var failedPrograms = new List<string>();
        foreach (Program program in _programs.ToList())
        {
            var result = target.InstallProgram(program);
            if (result.IsFailure)
                failedPrograms.Add($"{program.Spec.Name}: {result.Error}");
            else
                _programs.Remove(program);
        }

        // Transfer data files
        var failedFiles = new List<string>();
        foreach (DataFile file in _dataFiles.ToList())
        {
            var result = target.AddDataFile(file);
            if (result.IsFailure)
                failedFiles.Add($"{file.Name}: {result.Error}");
            else
                _dataFiles.Remove(file);
        }

        if (failedPrograms.Count > 0 || failedFiles.Count > 0)
        {
            var issues = failedPrograms.Concat(failedFiles);
            return Result.Fail(
                $"Deck transfer partially failed:\n{string.Join("\n", issues)}");
        }

        return Result.Ok();
    }

    // ── Convenience queries ───────────────────────────────────────────────────

    /// <summary>
    /// Returns the program in the specified slot, or <c>null</c> if empty.
    /// Throws for out-of-range indices.
    /// </summary>
    public Program? GetSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= MaxLoadedPrograms)
            throw new ArgumentOutOfRangeException(nameof(slotIndex),
                $"Slot index must be 0–{MaxLoadedPrograms - 1}.");

        return _loadedSlots[slotIndex];
    }

    /// <summary>
    /// Returns all programs of the given name that are installed on this deck,
    /// regardless of level or loaded status.
    /// </summary>
    public IEnumerable<Program> GetInstalledByName(ProgramName name) =>
        _programs.Where(p => p.Spec.Name == name);

    /// <summary>
    /// Returns the highest-level installed program with the given name,
    /// or <c>null</c> if none exists.
    /// </summary>
    public Program? GetBestInstalled(ProgramName name) =>
        GetInstalledByName(name).MaxBy(p => p.Spec.Level);

    /// <summary>Returns all programs currently ready to run.</summary>
    public IEnumerable<Program> GetReadyPrograms() =>
        _loadedSlots.Where(s => s is not null && s.IsReadyToRun)!;

    // ── Private helpers ───────────────────────────────────────────────────────

    private int GetCurrentStatValue(UpgradableStat stat) => stat switch
    {
        UpgradableStat.Response      => Stats.Response,
        UpgradableStat.Memory        => Stats.Memory,
        UpgradableStat.MemoryMax     => Stats.MemoryMax,
        UpgradableStat.Storage       => Stats.Storage,
        UpgradableStat.StorageMax    => Stats.StorageMax,
        UpgradableStat.LoadIoSpeed   => Stats.LoadIoSpeed,
        UpgradableStat.LoadIoSpeedMax => Stats.LoadIoSpeedMax,
        UpgradableStat.Bod           => Stats.Bod,
        UpgradableStat.Evasion       => Stats.Evasion,
        UpgradableStat.Masking       => Stats.Masking,
        UpgradableStat.Sensor        => Stats.Sensor,
        _ => throw new ArgumentOutOfRangeException(nameof(stat))
    };

    // ── Display ───────────────────────────────────────────────────────────────

    public override string ToString()
    {
        string brokenFlag = IsBroken ? " [BROKEN]" : "";
        return $"[Cyberdeck] {Name}{brokenFlag} | " +
               $"MPCP:{Stats.Mpcp} Hard:{Stats.Hardening} | " +
               $"Slots:{OccupiedSlotCount()}/{MaxLoadedPrograms} | " +
               $"Mem:{UsedMemory()}/{Stats.Memory}Mp | " +
               $"Stor:{UsedStorage()}/{Stats.Storage}Mp | " +
               $"Files:{_dataFiles.Count}/{MaxDataFiles}";
    }
}
