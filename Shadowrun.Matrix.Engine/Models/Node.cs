using Shadowrun.Matrix.Core;
using Shadowrun.Matrix.Enums;
using Shadowrun.Matrix.ValueObjects;

namespace Shadowrun.Matrix.Models;

/// <summary>
/// A single node in a Matrix system's network graph.
///
/// A Node has a type, a colour/rating security classification, zero-to-two
/// ICE instances guarding it, a set of executable actions, and connection
/// edges to adjacent nodes.
///
/// State tracked here:
/// <list type="bullet">
///   <item><see cref="IsConquered"/> — set once all ICE is cleared or bypassed via Sleaze.</item>
///   <item><see cref="IsIdentified"/> — set after a complete Analyze scan, or on revisit.</item>
///   <item><see cref="IsOnline"/> — SM nodes toggle this via <see cref="NodeAction.TurnOffNode"/>.</item>
/// </list>
///
/// Node type → available actions:
/// <list type="bullet">
///   <item>CPU → GoToNode, CancelAlert, CrashSystem</item>
///   <item>DS  → LeaveNode, TransferData, Erase</item>
///   <item>IOP → LeaveNode, Lockout</item>
///   <item>SAN → EnterSystem</item>
///   <item>SM  → LeaveNode, TurnOffNode</item>
///   <item>SPU → LeaveNode</item>
/// </list>
/// </summary>
public class Node
{
    // ── Identity ──────────────────────────────────────────────────────────────

    public string    Id             { get; }
    public NodeType  Type           { get; }
    public NodeColor Color          { get; }

    /// <summary>Security rating 1–7. Combined with <see cref="Color"/> to express overall power.</summary>
    public int       SecurityRating { get; private set; }

    /// <summary>Human-readable label (e.g. "Aztechnology CPU", "Research DS-3").</summary>
    public string    Label          { get; }

    /// <summary>For SM nodes: identifies which physical system this module controls.</summary>
    public SmModuleType? SmModuleType { get; }

    // ── Graph ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// IDs of all Nodes directly reachable from this one.
    /// Travel is only allowed to adjacent nodes (or via CPU GoToNode teleport).
    /// </summary>
    public IReadOnlyList<string> AdjacentNodeIds => _adjacentNodeIds.AsReadOnly();

    private readonly List<string> _adjacentNodeIds = [];

    // ── ICE ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// All ICE instances guarding this node.
    /// Rule: 0, 1, or 2 ICE; if 2, the second is always a hidden Tar-type.
    /// Use <see cref="GetPrimaryIce"/> and <see cref="GetSecondaryIce"/> for
    /// safe typed access.
    /// </summary>
    public IReadOnlyList<Ice> IceInstances => _iceInstances.AsReadOnly();

    private readonly List<Ice> _iceInstances = [];

    // ── Runtime state ─────────────────────────────────────────────────────────

    /// <summary>
    /// True once all live ICE has been defeated or the node was bypassed via Sleaze.
    /// Conquered nodes are highlighted on the system map; their ICE is still
    /// reset to full health on revisit.
    /// </summary>
    public bool IsConquered { get; private set; }

    /// <summary>
    /// True once the node has been fully scanned by Analyze, or after a revisit
    /// to a previously conquered node (the map colours it automatically).
    /// Enables success-bar display for loaded programs.
    /// </summary>
    public bool IsIdentified { get; private set; }

    /// <summary>
    /// For SM nodes: whether the module is currently online.
    /// Set to false by <see cref="NodeAction.TurnOffNode"/>.
    /// </summary>
    public bool IsOnline { get; private set; } = true;

    /// <summary>
    /// Cumulative probability that a probe from this node will trigger an alert.
    /// Varies by ICE type and security rating; consumed by <see cref="MatrixSystem"/>.
    /// </summary>
    public float AlertContribution { get; private set; }

    // ── Construction ─────────────────────────────────────────────────────────

    public Node(
        string       id,
        NodeType     type,
        NodeColor    color,
        int          securityRating,
        string?      label        = null,
        SmModuleType? smModuleType = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        if (securityRating is < 1 or > 7)
            throw new ArgumentOutOfRangeException(nameof(securityRating),
                "Security rating must be 1–7.");

        if (type == NodeType.SM && smModuleType is null)
            throw new ArgumentException(
                "SM nodes must specify a SmModuleType.", nameof(smModuleType));

        if (type != NodeType.SM && smModuleType is not null)
            throw new ArgumentException(
                "Only SM nodes may specify a SmModuleType.", nameof(smModuleType));

        Id             = id;
        Type           = type;
        Color          = color;
        SecurityRating = securityRating;
        Label          = label ?? $"{type}-{id[..Math.Min(4, id.Length)]}";
        SmModuleType   = smModuleType;
    }

    // ── Graph management ──────────────────────────────────────────────────────

    /// <summary>Adds a directed edge from this node to <paramref name="targetNodeId"/>.</summary>
    public void AddAdjacentNode(string targetNodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetNodeId);

        if (!_adjacentNodeIds.Contains(targetNodeId))
            _adjacentNodeIds.Add(targetNodeId);
    }

    /// <summary>Returns true if <paramref name="nodeId"/> is directly reachable from here.</summary>
    public bool IsAdjacentTo(string nodeId) => _adjacentNodeIds.Contains(nodeId);

    // ── ICE management ────────────────────────────────────────────────────────

    /// <summary>
    /// Adds an ICE instance to this node.
    /// Enforces the two-ICE maximum and the Tar-ICE-is-always-secondary rule.
    /// </summary>
    public void AddIce(Ice ice)
    {
        ArgumentNullException.ThrowIfNull(ice);

        if (_iceInstances.Count >= 2)
            throw new InvalidOperationException(
                $"Node '{Id}' already has the maximum of 2 ICE instances.");

        if (_iceInstances.Count == 1 && !ice.Spec.IsHidden)
            throw new InvalidOperationException(
                $"The second ICE on a node must be a hidden Tar-type (got {ice.Spec.Type}).");

        if (_iceInstances.Count == 0 && ice.Spec.IsHidden)
            throw new InvalidOperationException(
                $"A hidden Tar-type ICE ({ice.Spec.Type}) cannot be the sole ICE on a node.");

        _iceInstances.Add(ice);
        RecalculateAlertContribution();
    }

    /// <summary>
    /// Returns the primary (non-hidden) ICE guarding this node, or <c>null</c>
    /// if the node is unguarded.
    /// </summary>
    public Ice? GetPrimaryIce() =>
        _iceInstances.FirstOrDefault(i => !i.Spec.IsHidden);

    /// <summary>
    /// Returns the hidden Tar-type ICE lurking behind the primary, or <c>null</c>
    /// if none is present. Only revealed after the primary ICE is dealt with.
    /// </summary>
    public Ice? GetSecondaryIce() =>
        _iceInstances.FirstOrDefault(i => i.Spec.IsHidden);

    /// <summary>
    /// Returns all ICE on this node that are still alive.
    /// </summary>
    public IEnumerable<Ice> GetLiveIce() =>
        _iceInstances.Where(i => i.IsAlive);

    /// <summary>
    /// Returns the currently relevant ICE: the primary if alive, otherwise the
    /// revealed secondary. Null if all ICE has been defeated.
    /// </summary>
    public Ice? GetActiveIce()
    {
        Ice? primary = GetPrimaryIce();
        if (primary is not null && primary.IsAlive) return primary;

        Ice? secondary = GetSecondaryIce();
        return secondary?.IsAlive == true ? secondary : null;
    }

    /// <summary>
    /// True when all ICE on this node has been defeated.
    /// </summary>
    public bool AllIceDefeated() =>
        _iceInstances.All(i => !i.IsAlive);

    // ── Identification ────────────────────────────────────────────────────────

    /// <summary>
    /// Marks this node as fully identified (complete Analyze scan).
    /// Enables success-bar display for loaded programs.
    /// </summary>
    public void MarkIdentified() => IsIdentified = true;

    /// <summary>
    /// Marks this node as conquered. Called when all ICE is cleared or Sleaze bypasses it.
    /// </summary>
    public void MarkConquered()
    {
        IsConquered  = true;
        IsIdentified = true; // Conquering a node fully identifies it
    }

    /// <summary>
    /// Resets ICE to full health for a revisit (system state is preserved between
    /// same-system jack-ins, but ICE regenerates).
    /// Does NOT reset <see cref="IsConquered"/> or <see cref="IsIdentified"/>.
    /// </summary>
    public void ResetIceForRevisit(AlertState currentAlertState)
    {
        foreach (Ice ice in _iceInstances)
        {
            // Re-construct fresh ICE from the same spec
            // (health and alive state reset; spec/rating recalculated)
            ice.RecalculateEffectiveRating(currentAlertState);

            // We rely on internal state: call TakeDamage with 0 is a no-op.
            // The Ice class doesn't expose a direct Reset(), so we signal
            // the intent through the session layer. The simplest clean approach:
            // replace the ICE instance. But to avoid breaking references held
            // by callers, we instead expose a package-internal reset.
            ice.ResetHealth();
        }
    }

    // ── Security rating ───────────────────────────────────────────────────────

    /// <summary>
    /// Temporarily reduces the node's security rating by 1 (Degrade program effect).
    /// Cannot drop below 1.
    /// </summary>
    public void DecrementSecurityRating()
    {
        SecurityRating = Math.Max(1, SecurityRating - 1);

        // Recalculate ICE effective ratings at the new security level
        foreach (Ice ice in _iceInstances)
            ice.RecalculateEffectiveRating(AlertState.Normal); // Session layer passes real state
    }

    /// <summary>
    /// Restores the node's security rating to its original value.
    /// Called at session end or when the Degrade effect expires.
    /// </summary>
    public void RestoreSecurityRating(int originalRating)
    {
        if (originalRating is < 1 or > 7)
            throw new ArgumentOutOfRangeException(nameof(originalRating));

        SecurityRating = originalRating;
    }

    // ── Available actions ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns all actions the Persona may attempt at this node given its type
    /// and current state.
    ///
    /// Actions are always returned regardless of whether live ICE is present —
    /// the session layer is responsible for blocking action execution while
    /// ICE is in active combat.
    /// </summary>
    public IReadOnlyList<NodeAction> GetAvailableActions()
    {
        return Type switch
        {
            NodeType.CPU => [NodeAction.GoToNode, NodeAction.CancelAlert, NodeAction.CrashSystem],
            NodeType.DS  => [NodeAction.LeaveNode, NodeAction.TransferData, NodeAction.Erase],
            NodeType.IOP => [NodeAction.LeaveNode, NodeAction.Lockout],
            NodeType.SAN => [NodeAction.EnterSystem],
            NodeType.SM  => [NodeAction.LeaveNode, NodeAction.TurnOffNode],
            NodeType.SPU => [NodeAction.LeaveNode],
            _            => throw new InvalidOperationException($"Unknown node type: {Type}")
        };
    }

    /// <summary>
    /// Executes the given action at this node, producing an <see cref="ActionResult"/>.
    ///
    /// This method handles state changes local to the node (SM online toggle).
    /// Actions with system-wide effects (CancelAlert, CrashSystem, GoToNode) emit
    /// <see cref="SystemEvent"/>s for <c>MatrixSession</c> and <c>MatrixSystem</c>
    /// to process.
    ///
    /// Throws <see cref="InvalidOperationException"/> if <paramref name="action"/>
    /// is not valid for this node type (programming error).
    /// Returns a failed <see cref="ActionResult"/> for expected in-game refusals
    /// (e.g. trying to turn off an already-offline SM).
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <param name="targetNodeId">
    /// Required for <see cref="NodeAction.GoToNode"/> — the destination.
    /// </param>
    /// <param name="crashedFromInsideBuilding">
    /// For <see cref="NodeAction.CrashSystem"/> — whether the Decker is
    /// physically inside the associated building.
    /// </param>
    public ActionResult ExecuteAction(
        NodeAction action,
        string?    targetNodeId             = null,
        bool       crashedFromInsideBuilding = false)
    {
        var validActions = GetAvailableActions();
        if (!validActions.Contains(action))
            throw new InvalidOperationException(
                $"Action '{action}' is not valid for a {Type} node.");

        return action switch
        {
            NodeAction.LeaveNode    => ActionResult.Ok(Id, action),
            NodeAction.EnterSystem  => ActionResult.Ok(Id, action),
            NodeAction.Lockout      => ActionResult.Ok(Id, action), // Visual effect only

            NodeAction.GoToNode     => ExecuteGoToNode(targetNodeId),
            NodeAction.CancelAlert  => ExecuteCancelAlert(),
            NodeAction.CrashSystem  => ExecuteCrashSystem(crashedFromInsideBuilding),
            NodeAction.TransferData => ActionResult.Ok(Id, action), // Handled by session
            NodeAction.Erase        => ActionResult.Ok(Id, action), // Handled by session

            NodeAction.TurnOffNode  => ExecuteTurnOffNode(),

            _ => throw new InvalidOperationException($"Unhandled action: {action}")
        };
    }

    // ── Private action handlers ───────────────────────────────────────────────

    private ActionResult ExecuteGoToNode(string? targetNodeId)
    {
        if (string.IsNullOrWhiteSpace(targetNodeId))
            return ActionResult.Fail(Id, NodeAction.GoToNode,
                "GoToNode requires a target node ID.");

        var sideEffects = new List<SystemEvent>
        {
            new TeleportedToNodeEvent(targetNodeId)
        };

        return ActionResult.Ok(Id, NodeAction.GoToNode, sideEffects);
    }

    private ActionResult ExecuteCancelAlert()
    {
        var sideEffects = new List<SystemEvent> { new AlertCancelledEvent() };
        return ActionResult.Ok(Id, NodeAction.CancelAlert, sideEffects);
    }

    private ActionResult ExecuteCrashSystem(bool fromInsideBuilding)
    {
        var sideEffects = new List<SystemEvent>
        {
            new SystemCrashedEvent(fromInsideBuilding)
        };
        return ActionResult.Ok(Id, NodeAction.CrashSystem, sideEffects);
    }

    private ActionResult ExecuteTurnOffNode()
    {
        if (!IsOnline)
            return ActionResult.Fail(Id, NodeAction.TurnOffNode,
                $"Slave module '{Label}' is already offline.");

        IsOnline = false;

        var sideEffects = new List<SystemEvent>
        {
            new SlaveModuleDisabledEvent(Id, SmModuleType?.ToString() ?? "Unknown")
        };

        return ActionResult.Ok(Id, NodeAction.TurnOffNode, sideEffects);
    }

    // ── Alert contribution ────────────────────────────────────────────────────

    private void RecalculateAlertContribution()
    {
        // Base contribution from security rating; scaled by number of ICE present
        AlertContribution = (SecurityRating / 10f) * _iceInstances.Count;
    }

    // ── Display ───────────────────────────────────────────────────────────────

    public override string ToString()
    {
        string ice      = _iceInstances.Count > 0
            ? $" ICE:[{string.Join(", ", _iceInstances.Select(i => i.Spec.Type))}]"
            : " (unguarded)";

        string flags    = $"{(IsConquered ? " ✓" : "")}{(IsIdentified ? " ID" : "")}";
        string adjacent = $" →[{string.Join(", ", _adjacentNodeIds)}]";

        return $"[Node] {Label} {Color} SR:{SecurityRating}{ice}{flags}{adjacent}";
    }
}
