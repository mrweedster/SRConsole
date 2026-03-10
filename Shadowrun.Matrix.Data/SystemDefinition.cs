using Shadowrun.Matrix.Enums;

namespace Shadowrun.Matrix.Data;

/// <summary>
/// Complete static definition of one Matrix system.
/// Passed to SystemFactory.Build() to produce a live MatrixSystem.
/// </summary>
public class SystemDefinition
{
    public int    SystemNumber     { get; init; }
    public string Name             { get; init; } = "";
    public string Difficulty       { get; init; } = "simple";
    public string? CorporationName { get; init; }

    /// <summary>All nodes, keyed by their map character (e.g. "1", "A", "S").</summary>
    public IReadOnlyList<NodeDefinition> Nodes { get; init; } = [];

    /// <summary>
    /// Directed adjacency pairs as (fromKey, toKey) strings.
    /// All edges are bidirectional unless explicitly marked otherwise.
    /// </summary>
    public IReadOnlyList<(string From, string To)> Edges { get; init; } = [];
}

public class NodeDefinition
{
    /// <summary>Map key, e.g. "1", "2", "A", "B". Used as Node.Id suffix.</summary>
    public string       Key            { get; init; } = "";
    public NodeType     Type           { get; init; }
    public NodeColor    Color          { get; init; }
    public int          SecurityRating { get; init; }
    public string       Title          { get; init; } = "";
    public SmModuleType? SmModuleType  { get; init; }

    /// <summary>Primary (non-hidden) ICE. Null if the node is unguarded.</summary>
    public IceDefinition? PrimaryIce   { get; init; }

    /// <summary>
    /// Secondary hidden Tar ICE. Only valid when PrimaryIce is non-null
    /// and the secondary is TarPaper or TarPit.
    /// </summary>
    public IceDefinition? SecondaryIce { get; init; }

    /// <summary>
    /// Non-Tar secondary ICE that cannot yet be modelled (TODO).
    /// Displayed as a note on the node screen but NOT added to the live Node.
    /// </summary>
    public IceDefinition? TodoSecondaryIce { get; init; }
}

public class IceDefinition
{
    public IceType Type       { get; init; }
    public int     BaseRating { get; init; }
}
