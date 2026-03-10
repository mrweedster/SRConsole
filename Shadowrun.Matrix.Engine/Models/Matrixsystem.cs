using Shadowrun.Matrix.Core;
using Shadowrun.Matrix.Enums;
using Shadowrun.Matrix.ValueObjects;

namespace Shadowrun.Matrix.Models;

/// <summary>
/// A complete Matrix network: a directed graph of <see cref="Node"/>s connected
/// by adjacency edges, guarded by <see cref="Ice"/> instances, with a shared
/// <see cref="AlertState"/> that scales all ICE in real time.
///
/// Key rules enforced here:
/// <list type="bullet">
///   <item>Exactly one SAN and one CPU per system.</item>
///   <item>Alert escalation is one-way during a session (Normal → Passive → Active);
///         only the CPU's CancelAlert action resets it.</item>
///   <item>System state (conquered/identified nodes) persists between same-system
///         jack-ins. ICE health resets on revisit.</item>
///   <item>GoToNode teleport is one-way — the Persona must fight back through
///         unconquered nodes to return.</item>
/// </list>
/// </summary>
public class MatrixSystem
{
    // ── Identity ──────────────────────────────────────────────────────────────

    public string  Id                  { get; }
    public string  Name                { get; }
    public string? CorporationName     { get; }

    /// <summary>"simple", "moderate", or "expert".</summary>
    public string  Difficulty          { get; }

    // ── Alert state ───────────────────────────────────────────────────────────

    public AlertState AlertState { get; private set; } = AlertState.Normal;

    // ── Node graph ────────────────────────────────────────────────────────────

    private readonly Dictionary<string, Node> _nodes = new();

    /// <summary>Read-only view over all nodes in the system.</summary>
    public IReadOnlyDictionary<string, Node> Nodes => _nodes;

    // ── Special node IDs ──────────────────────────────────────────────────────

    /// <summary>The single SAN — entry point from public terminals.</summary>
    public string SanNodeId { get; private set; } = string.Empty;

    /// <summary>The single CPU — brain of the system.</summary>
    public string CpuNodeId { get; private set; } = string.Empty;

    // ── Probe alert accumulation ──────────────────────────────────────────────

    /// <summary>
    /// Running probability that the next probe-reached-edge event will escalate
    /// the alert state. Resets after each escalation.
    /// </summary>
    private float _accumulatedAlertProbability;

    // ── Random ────────────────────────────────────────────────────────────────

    private readonly Random _rng;

    // ── Construction ─────────────────────────────────────────────────────────

    public MatrixSystem(
        string  id,
        string  name,
        string  difficulty,
        string? corporationName = null,
        Random? rng             = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!MatrixRun.ValidDifficulties.Contains(difficulty))
            throw new ArgumentException($"Invalid difficulty: '{difficulty}'.", nameof(difficulty));

        Id              = id;
        Name            = name;
        Difficulty      = difficulty;
        CorporationName = corporationName;
        _rng            = rng ?? Random.Shared;
    }

    // ── Node management ───────────────────────────────────────────────────────

    /// <summary>
    /// Adds a node to the system.
    /// Enforces the one-SAN / one-CPU rules.
    /// </summary>
    public void AddNode(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (_nodes.ContainsKey(node.Id))
            throw new InvalidOperationException(
                $"A node with ID '{node.Id}' already exists in system '{Id}'.");

        if (node.Type == NodeType.SAN && SanNodeId != string.Empty)
            throw new InvalidOperationException(
                "A Matrix system can have only one SAN node.");

        if (node.Type == NodeType.CPU && CpuNodeId != string.Empty)
            throw new InvalidOperationException(
                "A Matrix system can have only one CPU node.");

        _nodes[node.Id] = node;

        if (node.Type == NodeType.SAN) SanNodeId = node.Id;
        if (node.Type == NodeType.CPU) CpuNodeId = node.Id;
    }

    /// <summary>
    /// Returns the node with <paramref name="nodeId"/>.
    /// Throws <see cref="KeyNotFoundException"/> if not found (programming error).
    /// </summary>
    public Node GetNode(string nodeId)
    {
        if (_nodes.TryGetValue(nodeId, out Node? node))
            return node;

        throw new KeyNotFoundException(
            $"Node '{nodeId}' does not exist in system '{Id}'.");
    }

    /// <summary>
    /// Returns the node with <paramref name="nodeId"/>, or null if absent.
    /// Use when the caller genuinely does not know if the node exists.
    /// </summary>
    public Node? FindNode(string nodeId) =>
        _nodes.GetValueOrDefault(nodeId);

    /// <summary>
    /// Returns all nodes directly reachable from <paramref name="nodeId"/>.
    /// </summary>
    public IReadOnlyList<Node> GetAdjacentNodes(string nodeId)
    {
        Node source = GetNode(nodeId);
        return source.AdjacentNodeIds
            .Select(id => _nodes[id])
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Adds a directed adjacency edge from <paramref name="fromId"/> to
    /// <paramref name="toId"/>. Optionally adds the reverse edge too.
    /// </summary>
    public void ConnectNodes(string fromId, string toId, bool bidirectional = true)
    {
        GetNode(fromId).AddAdjacentNode(toId);
        if (bidirectional)
            GetNode(toId).AddAdjacentNode(fromId);
    }

    // ── Alert management ──────────────────────────────────────────────────────

    /// <summary>
    /// Escalates the alert state to Passive if currently Normal, or to Active
    /// if already at Passive (second alert escalation rule).
    /// Recalculates effective ICE ratings across the entire system.
    /// No-op if already Active.
    /// </summary>
    public void TriggerPassiveAlert()
    {
        if (AlertState == AlertState.Active) return;

        if (AlertState == AlertState.Passive)
        {
            // Second alert while already at Passive escalates to Active
            TriggerActiveAlert();
            return;
        }

        AlertState = AlertState.Passive;
        RecalculateAllIceRatings();
    }

    /// <summary>
    /// Escalates the alert state to Active regardless of current level.
    /// Recalculates effective ICE ratings across the entire system.
    /// No-op if already Active.
    /// </summary>
    public void TriggerActiveAlert()
    {
        if (AlertState == AlertState.Active) return;

        AlertState = AlertState.Active;
        RecalculateAllIceRatings();
    }

    /// <summary>
    /// Resets the alert state to Normal.
    /// Only achievable via the CPU's CancelAlert action.
    /// Recalculates effective ICE ratings.
    /// </summary>
    public void CancelAlert()
    {
        if (AlertState == AlertState.Normal) return;

        AlertState = AlertState.Normal;
        _accumulatedAlertProbability = 0f;
        RecalculateAllIceRatings();
    }

    /// <summary>
    /// Called when a probe from Access or Barrier ICE reaches the screen edge.
    /// Accumulates alert probability and may escalate the alert state.
    /// </summary>
    /// <param name="alertChance">
    /// The <see cref="ProbeSpawnedEvent.AlertChance"/> from the spawning ICE.
    /// </param>
    public void IncrementAlertChance(float alertChance)
    {
        _accumulatedAlertProbability += alertChance;

        if ((float)_rng.NextDouble() <= _accumulatedAlertProbability)
        {
            _accumulatedAlertProbability = 0f;

            if (AlertState == AlertState.Normal)
                TriggerPassiveAlert();
            else if (AlertState == AlertState.Passive)
                TriggerActiveAlert();
        }
    }

    // ── System actions ────────────────────────────────────────────────────────

    /// <summary>
    /// Crashes the system: marks the CPU as conquered, triggers a system-wide
    /// shutdown, and returns the <see cref="SystemCrashedEvent"/> for the session
    /// to propagate to the game layer.
    /// </summary>
    public SystemCrashedEvent CrashSystem(bool crashedFromInsideBuilding)
    {
        if (CpuNodeId != string.Empty && _nodes.TryGetValue(CpuNodeId, out Node? cpu))
            cpu.MarkConquered();

        return new SystemCrashedEvent(crashedFromInsideBuilding);
    }

    // ── Revisit state management ──────────────────────────────────────────────

    /// <summary>
    /// Resets ICE health on all nodes for a return visit to the same system.
    /// Conquest and identification flags are preserved.
    /// Should be called when the Decker jacks back into this system without
    /// having visited any other system in between.
    /// </summary>
    public void PrepareForRevisit()
    {
        foreach (Node node in _nodes.Values)
            node.ResetIceForRevisit(AlertState);
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    /// <summary>Returns true if every node in the system has been conquered.</summary>
    public bool IsFullyConquered() =>
        _nodes.Values.All(n => n.IsConquered);

    /// <summary>Returns all nodes of the given type.</summary>
    public IEnumerable<Node> GetNodesByType(NodeType type) =>
        _nodes.Values.Where(n => n.Type == type);

    /// <summary>Returns all nodes that still have live ICE.</summary>
    public IEnumerable<Node> GetGuardedNodes() =>
        _nodes.Values.Where(n => n.GetLiveIce().Any());

    /// <summary>
    /// Returns true if this system has a matching corporate building — used when
    /// determining whether crashing the CPU deactivates cameras / maglocks.
    /// </summary>
    public bool HasAssociatedBuilding => CorporationName is not null;

    // ── Procedural generation ─────────────────────────────────────────────────

    /// <summary>
    /// Generates a complete, connected Matrix system procedurally.
    ///
    /// Node type distribution (approximate, from game data):
    /// <list type="bullet">
    ///   <item>CPU  6.3% — always exactly 1</item>
    ///   <item>DS  30.7% — data stores</item>
    ///   <item>IOP 16.3% — internal entry points</item>
    ///   <item>SAN  6.3% — always exactly 1</item>
    ///   <item>SM  19.4% — slave modules</item>
    ///   <item>SPU 23.2% — backbone nodes</item>
    /// </list>
    ///
    /// ICE occurrence weights (from game data):
    ///   Access=20%, Barrier=15.9%, BlackIce=13.3%, Blaster=12.6%,
    ///   Killer=10.2%, TarPaper=6.7%, TarPit=8.9%,
    ///   TraceAndBurn=6.4%, TraceAndDump=5.9%
    /// </summary>
    public static MatrixSystem GenerateProcedural(
        string                   systemId,
        string                   systemName,
        ProceduralSystemConfig   config,
        int                      seed,
        string?                  corporationName = null)
    {
        var rng    = new Random(seed);
        var system = new MatrixSystem(systemId, systemName, config.Difficulty,
                                      corporationName, rng);

        int totalNodes = rng.Next(config.MinNodes, config.MaxNodes + 1);

        // ── 1. Determine node type distribution ───────────────────────────────
        var nodeTypes = DistributeNodeTypes(totalNodes, rng);

        // ── 2. Create nodes ───────────────────────────────────────────────────
        var createdNodes = new List<Node>();
        int smIndex      = 0;
        int nodeCounter  = 0;

        foreach (NodeType type in nodeTypes)
        {
            nodeCounter++;
            string  nodeId    = $"{systemId}-{type}-{nodeCounter:D2}";
            NodeColor color   = config.AllowedColors[rng.Next(config.AllowedColors.Count)];
            int secRating     = rng.Next(1, 8); // 1–7

            SmModuleType? smType = null;
            if (type == NodeType.SM)
            {
                smType = smIndex switch
                {
                    0 => Enums.SmModuleType.Maglocks,
                    1 => Enums.SmModuleType.Cameras,
                    _ => Enums.SmModuleType.Generic
                };
                smIndex++;
            }

            string label = type == NodeType.SM
                ? $"{smType} SM"
                : $"{type}-{nodeCounter:D2}";

            var node = new Node(nodeId, type, color, secRating, label, smType);
            createdNodes.Add(node);
            system.AddNode(node);
        }

        // ── 3. Build a connected backbone (SPUs form the spine) ───────────────
        ConnectAsSpine(system, createdNodes, rng);

        // ── 4. Assign ICE ─────────────────────────────────────────────────────
        AssignIce(system, createdNodes, config, rng);

        return system;
    }

    // ── Private generation helpers ────────────────────────────────────────────

    /// <summary>
    /// Distributes node types according to game-observed percentages,
    /// always guaranteeing exactly one SAN and one CPU.
    /// </summary>
    private static List<NodeType> DistributeNodeTypes(int total, Random rng)
    {
        // Mandatory singletons
        var types = new List<NodeType> { NodeType.SAN, NodeType.CPU };
        int remaining = total - 2;

        // Weighted pool for the rest
        var weighted = new (NodeType Type, float Weight)[]
        {
            (NodeType.DS,  30.7f),
            (NodeType.IOP, 16.3f),
            (NodeType.SM,  19.4f),
            (NodeType.SPU, 23.2f),
        };

        float totalWeight = weighted.Sum(w => w.Weight);

        for (int i = 0; i < remaining; i++)
        {
            float roll    = (float)rng.NextDouble() * totalWeight;
            float running = 0f;

            foreach (var (type, weight) in weighted)
            {
                running += weight;
                if (roll <= running)
                {
                    types.Add(type);
                    break;
                }
            }
        }

        // Shuffle so SAN/CPU aren't always first
        Shuffle(types, rng);

        // Ensure SAN and CPU are not adjacent to each other directly
        // (minor cosmetic fix — swap CPU toward the end if needed)
        int sanIdx = types.IndexOf(NodeType.SAN);
        int cpuIdx = types.IndexOf(NodeType.CPU);

        if (Math.Abs(sanIdx - cpuIdx) == 1 && types.Count > 4)
        {
            int swapTarget = (cpuIdx + types.Count / 2) % types.Count;
            (types[cpuIdx], types[swapTarget]) = (types[swapTarget], types[cpuIdx]);
        }

        return types;
    }

    /// <summary>
    /// Connects nodes into a valid traversable graph.
    /// Strategy: build a linear spine through SPU nodes, then attach
    /// remaining nodes as branches off the spine.
    /// </summary>
    private static void ConnectAsSpine(
        MatrixSystem system,
        List<Node>   nodes,
        Random       rng)
    {
        // SAN is always the entry; CPU is near the end
        Node sanNode = nodes.First(n => n.Type == NodeType.SAN);
        Node cpuNode = nodes.First(n => n.Type == NodeType.CPU);

        // Spine: SAN → SPUs (ordered) → CPU
        var spuNodes   = nodes.Where(n => n.Type == NodeType.SPU).ToList();
        var otherNodes = nodes
            .Where(n => n.Type != NodeType.SAN  &&
                        n.Type != NodeType.CPU  &&
                        n.Type != NodeType.SPU)
            .ToList();

        // Build spine list
        var spine = new List<Node> { sanNode };
        spine.AddRange(spuNodes);
        spine.Add(cpuNode);

        // Connect spine sequentially
        for (int i = 0; i < spine.Count - 1; i++)
            system.ConnectNodes(spine[i].Id, spine[i + 1].Id);

        // Attach branch nodes to random spine positions (excluding CPU directly)
        int spineAttachRange = Math.Max(1, spine.Count - 1);
        foreach (Node branch in otherNodes)
        {
            int attachIdx  = rng.Next(0, spineAttachRange);
            Node attachTo  = spine[attachIdx];
            system.ConnectNodes(attachTo.Id, branch.Id);
        }
    }

    /// <summary>
    /// Assigns ICE to nodes according to config density and occurrence weights.
    /// Tar ICE is always placed as secondary (hidden) behind a primary non-Tar ICE.
    /// SAN nodes always remain unguarded (they are usually low security).
    /// CPU nodes always receive ICE.
    /// </summary>
    private static void AssignIce(
        MatrixSystem           system,
        List<Node>             nodes,
        ProceduralSystemConfig config,
        Random                 rng)
    {
        // ICE type weights (excluding Tar — handled separately as secondary)
        var primaryWeights = new (IceType Type, float Weight)[]
        {
            (IceType.Access,       20.0f),
            (IceType.Barrier,      15.9f),
            (IceType.BlackIce,     config.AllowBlackIce ? 13.3f : 0f),
            (IceType.Blaster,      12.6f),
            (IceType.Killer,       10.2f),
            (IceType.TraceAndBurn,  6.4f),
            (IceType.TraceAndDump,  5.9f),
        };

        float totalPrimaryWeight = primaryWeights.Sum(w => w.Weight);

        foreach (Node node in nodes)
        {
            // SAN never has ICE
            if (node.Type == NodeType.SAN) continue;

            // CPU always has ICE; others roll for it
            bool addIce = node.Type == NodeType.CPU ||
                          (float)rng.NextDouble() <= config.IceDensity;

            if (!addIce) continue;

            // Select primary ICE type
            IceType primaryType = RollWeighted(primaryWeights, totalPrimaryWeight, rng);
            int     iceRating   = rng.Next(config.MinIceRating, config.MaxIceRating + 1);

            var primarySpec = new IceSpec(
                type:               primaryType,
                baseRating:         iceRating,
                occurrenceWeight:   0f,         // Not needed at runtime
                weakAgainst:        GetWeaknesses(primaryType),
                isHidden:           false,
                primaryIceType:     null,
                graphicDescription: GetGraphicDescription(primaryType));

            node.AddIce(new Ice(primarySpec, system.AlertState, rng));

            // Roll for a secondary Tar ICE
            bool addTar = (float)rng.NextDouble() <= config.TarIceProbability;
            if (!addTar) continue;

            IceType tarType = rng.NextDouble() < 0.55 ? IceType.TarPit : IceType.TarPaper;

            var secondarySpec = new IceSpec(
                type:               tarType,
                baseRating:         iceRating,
                occurrenceWeight:   0f,
                weakAgainst:        [],
                isHidden:           true,
                primaryIceType:     primaryType,
                graphicDescription: GetGraphicDescription(tarType));

            node.AddIce(new Ice(secondarySpec, system.AlertState, rng));
        }
    }

    /// <summary>Recalculates effective ratings for every ICE in every node.</summary>
    private void RecalculateAllIceRatings()
    {
        foreach (Node node in _nodes.Values)
            foreach (Ice ice in node.IceInstances)
                ice.RecalculateEffectiveRating(AlertState);
    }

    // ── Static utility helpers ────────────────────────────────────────────────

    private static IceType RollWeighted(
        (IceType Type, float Weight)[] pool,
        float                          totalWeight,
        Random                         rng)
    {
        float roll    = (float)rng.NextDouble() * totalWeight;
        float running = 0f;

        foreach (var (type, weight) in pool)
        {
            if (weight <= 0f) continue;
            running += weight;
            if (roll <= running) return type;
        }

        // Fallback
        return pool.First(p => p.Weight > 0f).Type;
    }

    private static void Shuffle<T>(List<T> list, Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private static IEnumerable<ProgramName> GetWeaknesses(IceType type) => type switch
    {
        IceType.Access       => [ProgramName.Deception],
        IceType.Blaster      => [ProgramName.Deception],
        IceType.Killer       => [ProgramName.Deception],
        IceType.TraceAndBurn => [ProgramName.Deception, ProgramName.Relocate],
        IceType.TraceAndDump => [ProgramName.Deception, ProgramName.Relocate],
        _                    => []      // Barrier, BlackIce, TarPaper, TarPit
    };

    private static string GetGraphicDescription(IceType type) => type switch
    {
        IceType.Access       => "A square hatch with doors that repeatedly slide open and shut.",
        IceType.Barrier      => "A rotating three-spoked circular spark.",
        IceType.BlackIce     => "A dark form that morphs between a circle and a four-point star.",
        IceType.Blaster      => "An orange and black explosion.",
        IceType.Killer       => "A blue-grey sphere with electrical current circling around it.",
        IceType.TarPaper     => "Brownish, bubbling tar.",
        IceType.TarPit       => "An orange circle with tar bubbling inside.",
        IceType.TraceAndBurn => "A cylindrical base with a spherical probe topped with a flame.",
        IceType.TraceAndDump => "A cylindrical base with a spherical probe topped with smoke.",
        _                    => "Unknown ICE type."
    };

    // ── Display ───────────────────────────────────────────────────────────────

    public override string ToString()
    {
        string corp = CorporationName is not null ? $" ({CorporationName})" : "";
        return $"[MatrixSystem] '{Name}'{corp} [{Difficulty.ToUpper()}] " +
               $"Nodes:{_nodes.Count} Alert:{AlertState} " +
               $"SAN:{SanNodeId} CPU:{CpuNodeId}";
    }
}
