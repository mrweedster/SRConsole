using System.Xml.Linq;
using Shadowrun.Matrix.Enums;
using Shadowrun.Matrix.Models;
using Shadowrun.Matrix.UI;
using Shadowrun.Matrix.ValueObjects;
using MatrixProgram = Shadowrun.Matrix.Models.Program;

namespace Shadowrun.Matrix.Persistence;

/// <summary>
/// Saves and loads the complete decker + game state to/from an XML file.
/// The file is placed next to the executable (or in the current working directory).
/// </summary>
public static class SaveGameManager
{
    private static string SavePath =>
        Path.Combine(AppContext.BaseDirectory, "savegame.xml");

    // ── Public API ────────────────────────────────────────────────────────────

    public static bool SaveExists => File.Exists(SavePath);

    /// <summary>
    /// Persists the decker and game state to XML.
    /// Safe to call at any time (e.g. on exit from the main menu).
    /// </summary>
    public static void Save(Decker decker, GameState state)
    {
        var root = new XElement("SaveGame",
            new XAttribute("version", "1"),
            new XAttribute("saved", DateTimeOffset.Now.ToString("o")),
            SerializeDecker(decker),
            SerializeGameState(state));

        root.Save(SavePath);
    }

    /// <summary>
    /// Loads a previously saved game.
    /// Returns null if no save file exists or the file is corrupt.
    /// </summary>
    public static (Decker decker, GameState state)? Load()
    {
        if (!SaveExists) return null;
        try
        {
            var root    = XElement.Load(SavePath);
            var decker  = DeserializeDecker(root.Element("Decker")!);
            var state   = DeserializeGameState(root.Element("GameState"));
            return (decker, state);
        }
        catch
        {
            return null;   // corrupt or outdated save — start fresh
        }
    }

    // ── Serialization ─────────────────────────────────────────────────────────

    private static XElement SerializeDecker(Decker d)
    {
        var deck = d.Deck;
        return new XElement("Decker",
            new XAttribute("name",  d.Name),
            new XAttribute("nuyen", d.Nuyen),
            SerializeSkills(d.Skills),
            SerializeDeck(deck));
    }

    private static XElement SerializeSkills(DeckerSkills s) =>
        new XElement("Skills",
            new XAttribute("computer",    s.Computer),
            new XAttribute("combat",      s.Combat),
            new XAttribute("negotiation", s.Negotiation),
            new XAttribute("charisma",    s.Charisma),
            new XAttribute("magic",       s.Magic),
            new XAttribute("strength",    s.Strength));

    private static XElement SerializeDeck(Cyberdeck deck)
    {
        var stats = deck.Stats;
        var el = new XElement("Deck",
            new XAttribute("name", deck.Name),
            new XElement("Stats",
                new XAttribute("mpcp",           stats.Mpcp),
                new XAttribute("hardening",      stats.Hardening),
                new XAttribute("response",       stats.Response),
                new XAttribute("memory",         stats.Memory),
                new XAttribute("memoryMax",      stats.MemoryMax),
                new XAttribute("storage",        stats.Storage),
                new XAttribute("storageMax",     stats.StorageMax),
                new XAttribute("loadIoSpeed",    stats.LoadIoSpeed),
                new XAttribute("loadIoSpeedMax", stats.LoadIoSpeedMax),
                new XAttribute("bod",            stats.Bod),
                new XAttribute("evasion",        stats.Evasion),
                new XAttribute("masking",        stats.Masking),
                new XAttribute("sensor",         stats.Sensor)));

        var programsEl = new XElement("Programs");
        foreach (var p in deck.Programs)
        {
            bool isLoaded = deck.LoadedSlots.Contains(p);
            programsEl.Add(new XElement("Program",
                new XAttribute("name",        p.Spec.Name.ToString()),
                new XAttribute("level",       p.Spec.Level),
                new XAttribute("type",        p.Spec.Type.ToString()),
                new XAttribute("sizeInMp",    p.Spec.SizeInMp),
                new XAttribute("basePrice",   p.Spec.BasePrice),
                new XAttribute("description", p.Spec.Description),
                new XAttribute("usefulness",  p.Spec.UsefulnessRating),
                new XAttribute("reloads",     p.Spec.ReloadsAfterUse),
                new XAttribute("loaded",      isLoaded)));
        }
        el.Add(programsEl);

        var filesEl = new XElement("DataFiles");
        foreach (var f in deck.DataFiles)
        {
            filesEl.Add(new XElement("DataFile",
                new XAttribute("name",     f.Name),
                new XAttribute("sizeInMp", f.SizeInMp),
                new XAttribute("value",    f.NuyenValue),
                new XAttribute("plot",     f.IsPlotRelevant)));
        }
        el.Add(filesEl);

        return el;
    }

    private static XElement SerializeGameState(GameState s) =>
        new XElement("GameState",
            new XAttribute("karma", s.Karma));

    // ── Deserialization ───────────────────────────────────────────────────────

    private static Decker DeserializeDecker(XElement el)
    {
        string name  = (string)el.Attribute("name")!;
        int    nuyen = (int)el.Attribute("nuyen")!;

        var skills = DeserializeSkills(el.Element("Skills")!);
        var deck   = DeserializeDeck(el.Element("Deck")!);

        var decker = new Decker(name, deck, skills, startingNuyen: 0);
        decker.AddNuyen(nuyen);
        return decker;
    }

    private static DeckerSkills DeserializeSkills(XElement el) =>
        new DeckerSkills(
            computer:    (int)el.Attribute("computer")!,
            combat:      (int)el.Attribute("combat")!,
            negotiation: (int)el.Attribute("negotiation")!,
            charisma:    (int?)el.Attribute("charisma")    ?? 1,
            magic:       (int?)el.Attribute("magic")       ?? 0,
            strength:    (int?)el.Attribute("strength")    ?? 1);

    private static Cyberdeck DeserializeDeck(XElement el)
    {
        string name  = (string)el.Attribute("name")!;
        var    sEl   = el.Element("Stats")!;
        var    stats = new DeckStats(
            mpcp:           (int)sEl.Attribute("mpcp")!,
            hardening:      (int)sEl.Attribute("hardening")!,
            response:       (int)sEl.Attribute("response")!,
            memory:         (int)sEl.Attribute("memory")!,
            memoryMax:      (int)sEl.Attribute("memoryMax")!,
            storage:        (int)sEl.Attribute("storage")!,
            storageMax:     (int)sEl.Attribute("storageMax")!,
            loadIoSpeed:    (int)sEl.Attribute("loadIoSpeed")!,
            loadIoSpeedMax: (int)sEl.Attribute("loadIoSpeedMax")!,
            bod:            (int)sEl.Attribute("bod")!,
            evasion:        (int)sEl.Attribute("evasion")!,
            masking:        (int)sEl.Attribute("masking")!,
            sensor:         (int)sEl.Attribute("sensor")!);

        var deck = new Cyberdeck(name, stats);

        foreach (var pEl in el.Element("Programs")?.Elements("Program") ?? [])
        {
            try
            {
                var spec = new ProgramSpec(
                    name:             Enum.Parse<ProgramName>((string)pEl.Attribute("name")!),
                    type:             Enum.Parse<ProgramType>((string)pEl.Attribute("type")!),
                    level:            (int)pEl.Attribute("level")!,
                    description:      (string)pEl.Attribute("description")!,
                    usefulnessRating: (int?)pEl.Attribute("usefulness") ?? 5,
                    reloadsAfterUse:  (bool?)pEl.Attribute("reloads")   ?? false,
                    overrideSizeInMp: (int)pEl.Attribute("sizeInMp")!,
                    overrideBasePrice:(int?)pEl.Attribute("basePrice"));
                var program = new MatrixProgram(spec);
                deck.InstallProgram(program);
                // Restore loaded state — load instantly (not mid-session)
                if ((bool?)pEl.Attribute("loaded") == true)
                    deck.LoadProgram(program, midSession: false);
            }
            catch { /* skip corrupt program entry */ }
        }

        foreach (var fEl in el.Element("DataFiles")?.Elements("DataFile") ?? [])
        {
            try
            {
                bool isPlot = (bool?)fEl.Attribute("plot") ?? false;
                DataFile file = isPlot
                    ? DataFile.CreatePlotFile(
                        id:       (string)fEl.Attribute("name")!,
                        name:     (string)fEl.Attribute("name")!,
                        sizeInMp: (int)fEl.Attribute("sizeInMp")!,
                        content:  "")
                    : DataFile.CreateSellable(
                        name:     (string)fEl.Attribute("name")!,
                        sizeInMp: (int)fEl.Attribute("sizeInMp")!,
                        nuyenValue:(int)fEl.Attribute("value")!);
                deck.AddDataFile(file);
            }
            catch { /* skip corrupt file entry */ }
        }

        return deck;
    }

    private static GameState DeserializeGameState(XElement? el) =>
        new GameState { Karma = (int?)el?.Attribute("karma") ?? 0 };
}
