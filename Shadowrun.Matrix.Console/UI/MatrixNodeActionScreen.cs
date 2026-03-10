using Shadowrun.Matrix.Data;
using Shadowrun.Matrix.Enums;
using Shadowrun.Matrix.Models;
using Shadowrun.Matrix.UI;

namespace Shadowrun.Matrix.UI.Screens;

/// <summary>
/// Screen 6 — Node Action sub-menu.
/// Presents the actions available for the current node type.
/// CPU → GoToNode / CancelAlert / CrashSystem.
/// DS  → TransferData / Erase.
/// SM  → TurnOffNode.
/// </summary>
public sealed class MatrixNodeActionScreen : IScreen
{
    private readonly MatrixSession _session;
    private readonly int           _systemNumber;
    private readonly GameState     _gameState;

    private string? _resultMessage;
    private string? _actionNarrative;   // what the decker attempted
    private bool    _sessionEnded;
    private bool    _needsGoToNodeInput;
    private string  _goToNodeBuffer = "";

    public MatrixNodeActionScreen(MatrixSession session, int systemNumber, GameState? gameState = null)
    {
        _session      = session;
        _systemNumber = systemNumber;
        _gameState    = gameState ?? new GameState();
    }

    public void Render(int w, int h)
    {
        if (_resultMessage is not null)
        {
            RenderResult(w);
            return;
        }

        Node node = _session.System.GetNode(_session.Persona.CurrentNodeId);
        string key = NodeKey(node.Id);

        RenderHelper.DrawWindowOpen($"[Node Action — ({key}) {node.Type} \"{node.Label}\"]", w);
        RenderHelper.DrawWindowDivider(w);
        RenderHelper.DrawWindowBlankLine(w);

        if (_needsGoToNodeInput)
        {
            RenderGoToNodeInput(node, w);
            return;
        }

        var actions = GetAvailableActions(node);

        if (actions.Count == 0)
        {
            RenderHelper.DrawWindowCentredLine("No special actions available at this node.", w);
        }
        else
        {
            int inner = w - 2;
            foreach (var (i, (label, desc)) in actions.Select((a, i) => (i, a)))
            {
                string content = $"  [{i + 1}]  {label,-18}  {desc}";
                if (content.Length > inner) content = RenderHelper.Truncate(content, inner);
                VC.Write("║");
                VC.Write(content.PadRight(inner));
                VC.WriteLine("║");
            }
        }

        RenderHelper.DrawWindowBlankLine(w);
        RenderHelper.DrawWindowClose(w);
        VC.WriteLine();

        if (actions.Count > 0)
            VC.WriteLine($"  Select action (1–{actions.Count}) or [0] to cancel:".PadRight(w));
        else
            VC.WriteLine("  [0] Cancel".PadRight(w));
    }

    public IScreen? HandleInput(ConsoleKeyInfo key)
    {
        if (_resultMessage is not null)
        {
            if (_sessionEnded)
            {
                TryAwardRunReward();
                _gameState.ActiveSession = null;
                // Session already ended via CrashSystem → EndSession inside the engine.
                // Calling JackOut() here would throw GuardSessionActive. Skip it.
                return new MatrixSessionEndScreen(_session, _systemNumber, "crashed", _gameState);
            }
            return NavigationToken.Back;
        }

        // ── GoToNode text input ───────────────────────────────────────────────
        if (_needsGoToNodeInput)
        {
            if (key.Key == ConsoleKey.Escape || key.Key == ConsoleKey.Backspace && _goToNodeBuffer.Length == 0)
            {
                _needsGoToNodeInput = false;
                _goToNodeBuffer     = "";
            }
            else if (key.Key == ConsoleKey.Backspace)
            {
                _goToNodeBuffer = _goToNodeBuffer[..^1];
            }
            else if (key.Key == ConsoleKey.Enter)
            {
                ExecuteGoToNode(_goToNodeBuffer.Trim().ToUpper());
                _needsGoToNodeInput = false;
                _goToNodeBuffer     = "";
            }
            else if (!char.IsControl(key.KeyChar) && _goToNodeBuffer.Length < 4)
            {
                _goToNodeBuffer += key.KeyChar;
            }
            return null;
        }

        // ── Cancel ────────────────────────────────────────────────────────────
        if (key.Key == ConsoleKey.Escape || key.KeyChar == '0')
            return NavigationToken.Back;

        if (char.IsDigit(key.KeyChar))
        {
            int choice  = key.KeyChar - '0';
            Node node   = _session.System.GetNode(_session.Persona.CurrentNodeId);
            var actions = GetAvailableActions(node);

            if (choice >= 1 && choice <= actions.Count)
            {
                HandleAction(choice - 1, actions, node);
                return null;
            }
        }

        return null;
    }

    // ── Action dispatch ───────────────────────────────────────────────────────

    private void HandleAction(int index, List<(string Label, string Desc)> actions, Node node)
    {
        switch (node.Type)
        {
            case NodeType.CPU: ExecuteCpuAction(index, node); break;
            case NodeType.DS:  ExecuteDsAction(index, node);  break;
            case NodeType.SM:  ExecuteSmAction(node);         break;
        }
    }

    private void ExecuteCpuAction(int index, Node node)
    {
        switch (index)
        {
            case 0: // GoToNode
                _needsGoToNodeInput = true;
                _actionNarrative = "You interface with the CPU to navigate the system topology.";
                return;

            case 1: // CancelAlert
                _actionNarrative = $"You jack into the CPU at \"{node.Label}\" and attempt to cancel the system alert.";
                var cancelResult = _session.InitiateNodeAction(NodeAction.CancelAlert);
                _resultMessage = cancelResult.Success
                    ? "Success — you wipe the alert log. The system resets to Normal."
                    : $"Failed — the security countermeasures hold. {cancelResult.ErrorReason}";
                break;

            case 2: // CrashSystem
                _actionNarrative = $"You unleash a cascade overload through the CPU at \"{node.Label}\" — no going back.";
                var crashResult = _session.InitiateNodeAction(NodeAction.CrashSystem);
                _resultMessage  = crashResult.Success
                    ? "CPU destroyed. Building systems crash offline. The run is over — jacking out."
                    : $"The CPU fights back and holds. {crashResult.ErrorReason}";
                if (crashResult.Success)
                    _sessionEnded = true;
                break;
        }
    }

    private void ExecuteDsAction(int index, Node node)
    {
        switch (index)
        {
            case 0: // TransferData
                _actionNarrative = $"You search the datastore at \"{node.Label}\" and initiate a file transfer.";
                var xferResult = _session.PerformDataTransfer();
                if (!xferResult.Success)
                {
                    _resultMessage = $"Transfer failed — the system rejects your intrusion. {xferResult.ErrorReason}";
                }
                else if (xferResult.DownloadedFile is not null)
                {
                    _resultMessage = $"Download complete — \"{xferResult.DownloadedFile.Name}\" ({xferResult.DownloadedFile.SizeInMp}Mp) copied to your deck.";
                    if (xferResult.IsObjectiveTransfer)
                        _resultMessage += "\nObjective data secured. Mission target achieved.";
                }
                else if (xferResult.IsObjectiveTransfer)
                {
                    _resultMessage = "Objective transfer complete — the target operation executed successfully.";
                }
                else
                {
                    _resultMessage = "Search complete — no usable data found in this node.";
                }
                break;

            case 1: // Erase
                _actionNarrative = $"You target the datastore at \"{node.Label}\" for erasure.";
                var eraseResult = _session.InitiateNodeAction(NodeAction.Erase);
                _resultMessage = eraseResult.Success
                    ? "Data erased — the storage node has been wiped clean."
                    : $"Erase failed — the data is write-protected. {eraseResult.ErrorReason}";
                break;
        }
    }

    private void ExecuteSmAction(Node node)
    {
        _actionNarrative = $"You reach through the slave module \"{node.Label}\" and cut its power feed.";
        var smResult = _session.InitiateNodeAction(NodeAction.TurnOffNode);
        _resultMessage = smResult.Success
            ? $"Slave module \"{node.Label}\" taken offline — its systems go dark."
            : $"Failed — the module's failsafes prevent shutdown. {smResult.ErrorReason}";
    }

    private void ExecuteGoToNode(string keyInput)
    {
        string sysId    = _session.System.Id;
        string targetId = $"{sysId}-{keyInput}";

        if (!_session.System.Nodes.ContainsKey(targetId))
        {
            _resultMessage   = $"Node key \"{keyInput}\" not found in this system.";
            _actionNarrative = "You search the network topology — that address doesn't exist.";
            return;
        }

        var result = _session.InitiateNodeAction(NodeAction.GoToNode, targetId);
        _actionNarrative = $"You trace the path to node [{keyInput}] and prepare to jack across.";
        _resultMessage   = result.Success
            ? $"Teleport successful — you materialise at node [{keyInput}]."
            : $"GoToNode failed — the path is blocked. {result.ErrorReason}";
    }

    // ── Reward helper ─────────────────────────────────────────────────────────

    /// <summary>
    /// Awards nuyen and karma for a completed run without going through
    /// <see cref="MatrixSession.CompleteRun"/>, which guards against IsComplete
    /// being already true (set by CheckRunObjective inside InitiateNodeAction).
    /// Safe to call even if no active run or objective was not met.
    /// </summary>
    private void TryAwardRunReward()
    {
        var run = _session.ActiveRun;
        if (run is null || !run.ObjectiveAchieved || run.RewardClaimed) return;

        int nuyen = run.ComputePay(_session.Decker.NegotiationSkill);
        int karma = run.KarmaReward;

        _session.Decker.AddNuyen(nuyen);
        _gameState.Karma += karma;
        run.MarkRewardClaimed();
        _gameState.PendingReward = RunCompletionResult.Ok(nuyen, karma);
        _gameState.ActiveRun     = null;
    }

    // ── Available action lists ────────────────────────────────────────────────

    private static List<(string Label, string Desc)> GetAvailableActions(Node node) => node.Type switch
    {
        NodeType.CPU => [
            ("Go To Node",     "Teleport to any node in the system"),
            ("Cancel Alert",   "Reset system alert to Normal"),
            ("Crash System",   "Destroy CPU — disables building systems")
        ],
        NodeType.DS => [
            ("Transfer Data",  "Download / upload / search for files"),
            ("Erase",          "Delete data from this storage node")
        ],
        NodeType.SM => [
            ("Turn Off Node",  "Take this slave module offline")
        ],
        _ => []
    };

    // ── Render helpers ────────────────────────────────────────────────────────

    private void RenderGoToNodeInput(Node node, int w)
    {
        int inner = w - 2;
        var nodes = _session.System.Nodes.Values.OrderBy(n => n.Id).ToList();
        int perRow = Math.Max(1, inner / 12);

        foreach (var chunk in nodes.Chunk(perRow))
        {
            var parts = chunk.Select(n =>
            {
                string k      = NodeKey(n.Id);
                string marker = n.IsConquered ? $"({k})" : k;
                return $"{marker}:{n.Type.ToString()[..2]}".PadRight(10);
            });

            string line = "  " + string.Concat(parts);
            if (line.Length > inner) line = line[..inner];
            VC.Write("║");
            VC.Write(line.PadRight(inner));
            VC.WriteLine("║");
        }

        RenderHelper.DrawWindowBlankLine(w);
        RenderHelper.DrawWindowClose(w);
        VC.WriteLine();
        VC.WriteLine($"  Enter node key: {_goToNodeBuffer}_".PadRight(w));
        VC.WriteLine("  [Enter] Confirm   [Esc] Cancel".PadRight(w));
    }

    private void RenderResult(int w)
    {
        int inner = w - 2;
        RenderHelper.DrawWindowOpen("[Node Action Result]", w);

        // Narrative — what was attempted
        if (_actionNarrative is not null)
        {
            VC.Write("║");
            VC.ForegroundColor = ConsoleColor.DarkGray;
            VC.Write(RenderHelper.Truncate(_actionNarrative, inner).PadRight(inner));
            VC.ResetColor();
            VC.WriteLine("║");
            RenderHelper.DrawWindowDivider(w);
        }

        // Outcome sentence
        if (_resultMessage is not null)
        {
            bool success = !_resultMessage.StartsWith("Failed") &&
                           !_resultMessage.StartsWith("Transfer failed") &&
                           !_resultMessage.StartsWith("Erase failed") &&
                           !_resultMessage.StartsWith("GoToNode failed");
            VC.Write("║");
            VC.ForegroundColor = success ? ConsoleColor.Green : ConsoleColor.Red;
            string outcome = $"  {RenderHelper.Truncate(_resultMessage.Split('\n')[0], inner - 2)}";
            VC.Write(outcome.PadRight(inner));
            VC.ResetColor();
            VC.WriteLine("║");
            // Extra lines if multi-line result
            foreach (string extra in _resultMessage.Split('\n').Skip(1))
            {
                VC.Write("║");
                VC.ForegroundColor = ConsoleColor.Yellow;
                VC.Write($"  {RenderHelper.Truncate(extra, inner - 2)}".PadRight(inner));
                VC.ResetColor();
                VC.WriteLine("║");
            }
        }

        RenderHelper.DrawWindowDivider(w);

        // Recent session log
        var log   = _session.SessionLog;
        int count = Math.Min(5, log.Count);
        int start = Math.Max(0, log.Count - count);

        for (int i = start; i < log.Count; i++)
        {
            var evt  = log[i];
            string entry = $"  {evt.Timestamp:HH:mm:ss}  {RenderHelper.Truncate(evt.Description, inner - 14)}";
            VC.Write("║");
            VC.ForegroundColor = EventColor(evt.Type);
            VC.Write(entry.PadRight(inner));
            VC.ResetColor();
            VC.WriteLine("║");
        }

        RenderHelper.DrawWindowClose(w);
        VC.WriteLine();
        VC.WriteLine("  [Any key] Continue".PadRight(w));
    }

    // ── Utilities ─────────────────────────────────────────────────────────────

    private static string NodeKey(string nodeId)
    {
        int dash = nodeId.IndexOf('-');
        return dash >= 0 ? nodeId[(dash + 1)..] : nodeId;
    }

    private static ConsoleColor EventColor(SessionEventType t) => t switch
    {
        SessionEventType.PersonaDamaged      => ConsoleColor.Red,
        SessionEventType.PersonaDumped       => ConsoleColor.Red,
        SessionEventType.DeckDamaged         => ConsoleColor.Red,
        SessionEventType.AlertEscalated      => ConsoleColor.Yellow,
        SessionEventType.IceDefeated         => ConsoleColor.Green,
        SessionEventType.NodeConquered       => ConsoleColor.Green,
        SessionEventType.RunObjectiveMet     => ConsoleColor.Green,
        SessionEventType.SlaveModeDisabled   => ConsoleColor.Green,
        SessionEventType.SystemCrashed       => ConsoleColor.Red,
        SessionEventType.AlertCancelled      => ConsoleColor.Yellow,
        SessionEventType.DataFileFound       => ConsoleColor.Cyan,
        SessionEventType.DataFileTransferred => ConsoleColor.Cyan,
        _                                    => ConsoleColor.Gray
    };
}

