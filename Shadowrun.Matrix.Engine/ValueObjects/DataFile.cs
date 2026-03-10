namespace Shadowrun.Matrix.ValueObjects;

/// <summary>
/// Represents a data file that can be downloaded from a DS Node,
/// sold to Roscoe for nuyen, or stored in the Decker's notebook
/// if it contains plot-relevant information.
///
/// Files occupy <see cref="SizeInMp"/> of deck storage.
/// A deck may hold a maximum of five data files at any time, regardless of
/// remaining storage space.
/// </summary>
public class DataFile
{
    // ── Identity ──────────────────────────────────────────────────────────────

    /// <summary>Unique identifier — used to match mission-specific files to objectives.</summary>
    public string Id            { get; }

    public string Name          { get; }

    // ── Storage ───────────────────────────────────────────────────────────────

    /// <summary>Megapulses consumed in deck storage.</summary>
    public int SizeInMp { get; }

    // ── Value ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Nuyen earned when sold to Roscoe. 0 for mission-specific files
    /// that have no street value.
    /// </summary>
    public int NuyenValue { get; }

    // ── Plot ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// When true, this file contains lore or quest information.
    /// On jack-out the game will display "an interesting file that you download
    /// to your notebook" and move it to the Decker's notebook automatically.
    /// </summary>
    public bool IsPlotRelevant { get; }

    /// <summary>
    /// The readable text content of the file. Only populated for plot-relevant
    /// files; null otherwise.
    /// </summary>
    public string? Content { get; }

    // ── Construction ─────────────────────────────────────────────────────────

    public DataFile(
        string  id,
        string  name,
        int     sizeInMp,
        int     nuyenValue,
        bool    isPlotRelevant,
        string? content = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id,   nameof(id));
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));

        if (sizeInMp    < 0) throw new ArgumentException("SizeInMp must not be negative.",        nameof(sizeInMp));
        if (nuyenValue < 0)  throw new ArgumentException("NuyenValue cannot be negative.",       nameof(nuyenValue));

        if (isPlotRelevant && content is null)
            throw new ArgumentException(
                "Plot-relevant files must provide a content string.", nameof(content));

        Id             = id;
        Name           = name;
        SizeInMp       = sizeInMp;
        NuyenValue     = nuyenValue;
        IsPlotRelevant = isPlotRelevant;
        Content        = content;
    }

    // ── Convenience factory ───────────────────────────────────────────────────

    /// <summary>Creates a generic sellable data file with a generated id.</summary>
    public static DataFile CreateSellable(string name, int sizeInMp, int nuyenValue) =>
        new(
            id:             Guid.NewGuid().ToString("N"),
            name:           name,
            sizeInMp:       sizeInMp,
            nuyenValue:     nuyenValue,
            isPlotRelevant: false);

    /// <summary>Creates a plot-relevant file that will be auto-moved to the notebook.</summary>
    public static DataFile CreatePlotFile(string id, string name, int sizeInMp, string content) =>
        new(
            id:             id,
            name:           name,
            sizeInMp:       sizeInMp,
            nuyenValue:     0,
            isPlotRelevant: true,
            content:        content);

    // ── Display ───────────────────────────────────────────────────────────────

    public override string ToString() =>
        IsPlotRelevant
            ? $"[DataFile] '{Name}' — PLOT FILE ({SizeInMp}Mp)"
            : $"[DataFile] '{Name}' — {NuyenValue}¥ ({SizeInMp}Mp)";
}
