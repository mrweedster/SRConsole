using Shadowrun.Matrix.Enums;
using Shadowrun.Matrix.Models;

namespace Shadowrun.Matrix.UI.Screens;

/// <summary>
/// Screen 5 — Travel sub-menu.
/// Lists nodes adjacent to the Persona's current position.
/// On selection calls <see cref="MatrixSession.TravelToNode"/> and shows ICE encounters.
/// </summary>
public sealed class MatrixTravelScreen : IScreen
{
    private readonly MatrixSession _session;
    private readonly int           _systemNumber;

    private string? _resultMessage;
    private bool    _sessionEnded;

    public MatrixTravelScreen(MatrixSession session, int systemNumber)
    {
        _session      = session;
        _systemNumber = systemNumber;
    }

    public void Render(int w, int h)
    {
        if (_resultMessage is not null)
        {
            RenderResult(w);
            return;
        }

        Node currentNode  = _session.System.GetNode(_session.Persona.CurrentNodeId);
        var  adjacentIds  = currentNode.AdjacentNodeIds;

        RenderHelper.DrawWindowOpen("[Travel — Select Destination]", w);
        RenderHelper.DrawWindowDivider(w);
        RenderHelper.DrawWindowBlankLine(w);

        if (adjacentIds.Count == 0)
        {
            RenderHelper.DrawWindowCentredLine("No adjacent nodes.", w);
        }
        else
        {
            for (int i = 0; i < adjacentIds.Count; i++)
            {
                Node dest    = _session.System.GetNode(adjacentIds[i]);
                string key   = NodeKey(dest.Id);
                string type  = dest.Type.ToString();
                string color = dest.Color.ToString();
                string sr    = $"SR:{dest.SecurityRating}";
                string label = dest.Label;

                string status;
                ConsoleColor fg;

                if (dest.IsConquered)
                {
                    status = "Conquered ✓";
                    fg = ConsoleColor.Green;
                }
                else
                {
                    var liveIce = dest.GetLiveIce().ToList();
                    if (liveIce.Count > 0)
                    {
                        var ice   = liveIce[0];
                        status    = $"ICE: {ice.Spec.Type} R{ice.EffectiveRating}";
                        fg        = ConsoleColor.Red;
                    }
                    else if (dest.IceInstances.Count > 0)
                    {
                        status = "ICE defeated";
                        fg     = ConsoleColor.DarkGray;
                    }
                    else
                    {
                        status = "No ICE";
                        fg     = ConsoleColor.Gray;
                    }
                }

                int inner = w - 2;
                // Helper: exact-width column (truncate or pad)
                static string Col(string s, int w) => s.Length >= w ? s[..w] : s.PadRight(w);

                // Fixed columns: num(4) key(3) type(3) color(6) sr(5) label(16) then status
                string num     = $"[{i + 1}]";
                string line =
                    "  " + Col(num, 4) + " " +
                    Col(key,   3) + " " +
                    Col(type,  3) + " " +
                    Col(color, 6) + " " +
                    Col(sr,    5) + "  " +
                    Col(RenderHelper.Truncate(label, 16), 16) +
                    "  " + status;
                if (line.Length > inner) line = line[..inner];
                else line = line.PadRight(inner);

                VC.Write("║");
                VC.ForegroundColor = fg;
                VC.Write(line);
                VC.ResetColor();
                VC.WriteLine("║");
            }
        }

        RenderHelper.DrawWindowBlankLine(w);
        RenderHelper.DrawWindowClose(w);
        VC.WriteLine();
        VC.WriteLine($"  Travel to (1–{adjacentIds.Count}) or [0] to cancel:".PadRight(w));
    }

    public IScreen? HandleInput(ConsoleKeyInfo key)
    {
        if (_resultMessage is not null)
        {
            if (_sessionEnded)
                return new MatrixSessionEndScreen(_session, _systemNumber, "dumped");
            return NavigationToken.Back;
        }

        if (key.Key == ConsoleKey.Escape || key.KeyChar == '0')
            return NavigationToken.Back;

        if (char.IsDigit(key.KeyChar))
        {
            int choice = key.KeyChar - '0';
            var adj    = _session.System.GetNode(_session.Persona.CurrentNodeId).AdjacentNodeIds;

            if (choice >= 1 && choice <= adj.Count)
            {
                ExecuteTravel(adj[choice - 1]);
                return null;
            }
        }

        return null;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void ExecuteTravel(string targetNodeId)
    {
        // Tick probe/ICE state before traveling
        _session.TickFrame(2.0f);

        var result = _session.TravelToNode(targetNodeId);

        if (!result.Success)
        {
            _resultMessage = $"Cannot travel: {result.ErrorReason}";
            return;
        }

        // Build result: show recent log entries
        var recent = _session.SessionLog.TakeLast(5).ToList();
        var lines  = new List<string>();

        Node dest = _session.System.GetNode(targetNodeId);
        lines.Add($"Moved to: ({NodeKey(dest.Id)}) {dest.Type}  {dest.Color}  SR:{dest.SecurityRating}  \"{dest.Label}\"");

        if (result.EncounteredIce.Count > 0)
        {
            lines.Add("");
            lines.Add("⚠ ICE ENCOUNTERED:");
            foreach (var ice in result.EncounteredIce)
                lines.Add($"   {ice.Spec.Type}  Rating:{ice.EffectiveRating}  HP:{ice.CurrentHealth:F0}/{ice.MaxHealth:F0}");
        }
        else
        {
            lines.Add("No ICE. Node is clear.");
        }

        _resultMessage = string.Join("\n", lines);

        if (!_session.IsActive)
            _sessionEnded = true;
    }

    private void RenderResult(int w)
    {
        int inner = w - 2;
        RenderHelper.DrawWindowOpen("[Travel Result]", w);
        RenderHelper.DrawWindowDivider(w);
        RenderHelper.DrawWindowBlankLine(w);

        // Show recent session log entries with colour
        var log   = _session.SessionLog;
        int count = Math.Min(8, log.Count);
        int start = Math.Max(0, log.Count - count);

        for (int i = start; i < log.Count; i++)
        {
            var evt = log[i];
            VC.Write("║ ");
            VC.ForegroundColor = EventColor(evt.Type);
            string line = $" {evt.Timestamp:HH:mm:ss}  {RenderHelper.Truncate(evt.Description, inner - 13)}";
            VC.Write(line.PadRight(inner));
            VC.ResetColor();
            VC.WriteLine("║");
        }

        // ICE warning if present
        Node dest    = _session.System.GetNode(_session.Persona.CurrentNodeId);
        var liveIce  = dest.GetLiveIce().ToList();
        if (liveIce.Count > 0)
        {
            RenderHelper.DrawWindowBlankLine(w);
            VC.Write("║");
            VC.ForegroundColor = ConsoleColor.Red;
            string warn = $"  ⚠ COMBAT — Use [Run Program] to fight the ICE!";
            VC.Write(warn.PadRight(inner));
            VC.ResetColor();
            VC.WriteLine("║");
        }

        RenderHelper.DrawWindowBlankLine(w);
        RenderHelper.DrawWindowClose(w);
        VC.WriteLine();
        VC.WriteLine("  [Any key] Continue".PadRight(w));
    }

    private static string NodeKey(string nodeId)
    {
        int dash = nodeId.IndexOf('-');
        return dash >= 0 ? nodeId[(dash + 1)..] : nodeId;
    }

    private static ConsoleColor EventColor(SessionEventType t) => t switch
    {
        SessionEventType.PersonaDamaged  => ConsoleColor.Red,
        SessionEventType.PersonaDumped   => ConsoleColor.Red,
        SessionEventType.AlertEscalated  => ConsoleColor.Yellow,
        SessionEventType.IceDefeated     => ConsoleColor.Green,
        SessionEventType.NodeConquered   => ConsoleColor.Green,
        SessionEventType.NodeEntered     => ConsoleColor.Cyan,
        SessionEventType.CombatEngaged   => ConsoleColor.Red,
        _                                => ConsoleColor.Gray
    };
}
