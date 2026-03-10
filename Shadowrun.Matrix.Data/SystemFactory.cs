using Shadowrun.Matrix.Enums;
using Shadowrun.Matrix.Models;
using Shadowrun.Matrix.ValueObjects;

namespace Shadowrun.Matrix.Data;

/// <summary>
/// Builds a live MatrixSystem from a SystemDefinition.
/// Node IDs are formatted as "{systemNumber}-{key}", e.g. "7-A".
/// </summary>
public static class SystemFactory
{
    public static MatrixSystem Build(SystemDefinition def, Random? rng = null)
    {
        string systemId = def.SystemNumber.ToString();

        var system = new MatrixSystem(
            id:              systemId,
            name:            def.Name,
            difficulty:      def.Difficulty,
            corporationName: def.CorporationName,
            rng:             rng);

        // Build and add all nodes
        foreach (NodeDefinition nd in def.Nodes)
        {
            string nodeId = $"{systemId}-{nd.Key}";

            var node = new Node(
                id:             nodeId,
                type:           nd.Type,
                color:          nd.Color,
                securityRating: nd.SecurityRating,
                label:          nd.Title,
                smModuleType:   nd.SmModuleType);

            // Attach ICE
            if (nd.PrimaryIce is not null)
            {
                var primarySpec = BuildIceSpec(nd.PrimaryIce, isHidden: false, primaryType: null);
                node.AddIce(new Ice(primarySpec, rng: rng));

                if (nd.SecondaryIce is not null)
                {
                    var secondarySpec = BuildIceSpec(
                        nd.SecondaryIce,
                        isHidden:    true,
                        primaryType: nd.PrimaryIce.Type);
                    node.AddIce(new Ice(secondarySpec, rng: rng));
                }
                // TodoSecondaryIce is stored on NodeDefinition only for display; NOT added here.
            }

            system.AddNode(node);
        }

        // Wire adjacency edges (all bidirectional)
        foreach (var (fromKey, toKey) in def.Edges)
        {
            string fromId = $"{systemId}-{fromKey}";
            string toId   = $"{systemId}-{toKey}";
            system.ConnectNodes(fromId, toId, bidirectional: true);
        }

        return system;
    }

    private static IceSpec BuildIceSpec(IceDefinition ice, bool isHidden, IceType? primaryType)
    {
        return new IceSpec(
            type:               ice.Type,
            baseRating:         ice.BaseRating,
            occurrenceWeight:   0f,
            weakAgainst:        GetWeaknesses(ice.Type),
            isHidden:           isHidden,
            primaryIceType:     primaryType,
            graphicDescription: GetGraphicDescription(ice.Type));
    }

    private static IEnumerable<ProgramName> GetWeaknesses(IceType type) => type switch
    {
        IceType.Access       => [ProgramName.Deception],
        IceType.Blaster      => [ProgramName.Deception],
        IceType.Killer       => [ProgramName.Deception],
        IceType.TraceAndBurn => [ProgramName.Deception, ProgramName.Relocate],
        IceType.TraceAndDump => [ProgramName.Deception, ProgramName.Relocate],
        _                    => []
    };

    private static string GetGraphicDescription(IceType type) => type switch
    {
        IceType.Access       => "A square hatch with sliding doors.",
        IceType.Barrier      => "A rotating three-spoked circular spark.",
        IceType.BlackIce     => "A dark morphing sphere/star form.",
        IceType.Blaster      => "An orange and black explosion.",
        IceType.Killer       => "A blue-grey electrified sphere.",
        IceType.TarPaper     => "Brownish bubbling tar.",
        IceType.TarPit       => "An orange circle with bubbling tar.",
        IceType.TraceAndBurn => "Cylindrical base with flaming probe.",
        IceType.TraceAndDump => "Cylindrical base with smoke-plume probe.",
        _                    => "Unknown ICE."
    };
}
