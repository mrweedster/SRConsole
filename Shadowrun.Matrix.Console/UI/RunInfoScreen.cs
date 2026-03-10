using Shadowrun.Matrix.Enums;
using Shadowrun.Matrix.Models;

namespace Shadowrun.Matrix.UI.Screens;

/// <summary>
/// Screen: Run Info — accessible from Decker menu or mid-session via [5].
/// Displays the full mission briefing and allows reward collection once the
/// objective has been achieved.
/// </summary>
public sealed class RunInfoScreen : IScreen
{
    private readonly MatrixRun  _run;
    private readonly GameState? _gameState;
    private readonly Decker?    _decker;

    /// <summary>Full constructor — supports reward collection.</summary>
    public RunInfoScreen(MatrixRun run, GameState? gameState = null, Decker? decker = null)
    {
        _run       = run;
        _gameState = gameState;
        _decker    = decker;
    }

    // True when this screen can pay out the reward
    private bool CanClaimReward =>
        _run.ObjectiveAchieved &&
        !_run.RewardClaimed    &&
        _gameState is not null &&
        _decker    is not null;

    public void Render(int w, int h)
    {
        RenderHelper.DrawWindowOpen("[Main Menu -> Decker -> Run Info]", w);

        RenderHelper.DrawWindowStatLine("Johnson:",    _run.JohnsonName,                                    w);
        RenderHelper.DrawWindowStatLine("Difficulty:", CapFirst(_run.Difficulty),                           w);
        RenderHelper.DrawWindowStatLine("Objective:",  FormatObjective(_run.Objective),                     w);
        RenderHelper.DrawWindowStatLine("Target:",     _run.TargetNodeTitle,                                w);
        RenderHelper.DrawWindowStatLine("Payout:",     $"{_run.BasePayNuyen}\u00a5  +{_run.KarmaReward} karma", w);

        // Status line — colour-coded
        int    inner     = w - 2;
        string statusStr;
        ConsoleColor statusColor;

        if (_run.RewardClaimed)
        {
            statusStr   = "COMPLETE \u2713 (reward collected)";
            statusColor = ConsoleColor.DarkGreen;
        }
        else if (_run.ObjectiveAchieved)
        {
            statusStr   = "COMPLETE \u2713 \u2014 reward ready!";
            statusColor = ConsoleColor.Green;
        }
        else
        {
            statusStr   = "Pending\u2026";
            statusColor = ConsoleColor.Gray;
        }

        const string label = "  Status:  ";
        VC.Write("\u2551");
        VC.ForegroundColor = ConsoleColor.Gray;
        VC.Write(label);
        VC.ForegroundColor = statusColor;
        VC.Write(statusStr.PadRight(inner - label.Length));
        VC.ResetColor();
        VC.WriteLine("\u2551");

        // Reward claim banner
        if (CanClaimReward)
        {
            RenderHelper.DrawWindowDivider(w);
            VC.Write("\u2551");
            VC.ForegroundColor = ConsoleColor.Yellow;
            VC.Write(RenderHelper.Centre("\u2605  OBJECTIVE ACHIEVED \u2014 COLLECT YOUR REWARD  \u2605", inner));
            VC.ResetColor();
            VC.WriteLine("\u2551");
            int pay = _run.ComputePay(_decker!.NegotiationSkill);
            RenderHelper.DrawWindowStatLine("Payment:", $"+{pay}\u00a5  +{_run.KarmaReward} karma", w);
        }

        RenderHelper.DrawWindowDivider(w);
        RenderHelper.DrawWindowBlankLine(w);

        // Objective description
        string desc = BuildDescription(_run);
        RenderHelper.DrawWindowWrappedText(desc, w, indent: 2);

        RenderHelper.DrawWindowBlankLine(w);

        // Tip (only when not yet complete)
        if (!_run.ObjectiveAchieved)
        {
            string tip = _run.Objective switch
            {
                MatrixRunObjective.CrashCpu     => "Tip: Navigate to the CPU node and use Node Action \u2192 Crash System.",
                MatrixRunObjective.DownloadData => $"Tip: Find the '{_run.TargetNodeTitle}' DS node and use Node Action \u2192 Transfer Data.",
                MatrixRunObjective.DeleteData   => $"Tip: Find the '{_run.TargetNodeTitle}' DS node and use Node Action \u2192 Erase.",
                MatrixRunObjective.UploadData   => $"Tip: Find the '{_run.TargetNodeTitle}' DS node and use Node Action \u2192 Transfer Data.",
                _                               => ""
            };

            if (!string.IsNullOrEmpty(tip))
            {
                RenderHelper.DrawWindowDivider(w);
                RenderHelper.DrawWindowBlankLine(w);
                RenderHelper.DrawWindowWrappedText(tip, w, indent: 2);
                RenderHelper.DrawWindowBlankLine(w);
            }
        }

        RenderHelper.DrawWindowClose(w);
        VC.WriteLine();

        if (CanClaimReward)
            VC.WriteLine("  [C] Collect Reward   [Backspace] Back".PadRight(w));
        else
            VC.WriteLine("  [Backspace] Back".PadRight(w));
    }

    public IScreen? HandleInput(ConsoleKeyInfo key)
    {
        // Collect reward
        if ((key.KeyChar is 'c' or 'C') && CanClaimReward)
        {
            int nuyen = _run.ComputePay(_decker!.NegotiationSkill);
            int karma = _run.KarmaReward;
            _decker!.AddNuyen(nuyen);
            _gameState!.Karma += karma;
            _run.MarkRewardClaimed();
            _gameState!.PendingReward = RunCompletionResult.Ok(nuyen, karma);
            _gameState!.ActiveRun     = null;
            return NavigationToken.Root;
        }

        if (key.Key == ConsoleKey.Escape)    return NavigationToken.Back;
        if (key.Key == ConsoleKey.Backspace) return NavigationToken.Back;
        return null;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string BuildDescription(MatrixRun run)
    {
        string sys  = run.TargetNodeTitle;
        string file = run.ContractedFilename ?? string.Empty;
        int    seed = Math.Abs(run.Id.GetHashCode());

        return run.Objective switch
        {
            MatrixRunObjective.CrashCpu => (seed % 3) switch
            {
                0 => $"Jack into the target system and bring the {sys} down hard. No files, no finesse \u2014 the client wants a full blackout.",
                1 => $"Your target is the {sys}. Crash it from the inside and make it look like a hardware fault. Clean and untraceable.",
                _ => $"Mr. Johnson wants the {sys} dead. Simple sabotage \u2014 no extraction needed, just total system destruction."
            },
            MatrixRunObjective.DownloadData => (seed % 3) switch
            {
                0 => $"Slip into the system and pull '{file}' from the {sys} store. Clean copy, no flags \u2014 the client needs it intact.",
                1 => $"There's a file called '{file}' sitting in the {sys}. Extract it quietly, cover your exit, and leave no trace.",
                _ => $"Get into the system, locate '{file}' in the {sys}, and exfiltrate the data. Sloppy work is not acceptable."
            },
            MatrixRunObjective.DeleteData => (seed % 3) switch
            {
                0 => $"'{file}' in the {sys} needs to stop existing. Wipe it clean.",
                1 => $"Burn '{file}' out of the {sys}. The kind of deleted that courts can't undo.",
                _ => $"Jack in and purge '{file}' from the {sys}. No backups, no shadows, no recovery path \u2014 total erasure."
            },
            MatrixRunObjective.UploadData => (seed % 3) switch
            {
                0 => $"Smuggle '{file}' into the {sys} without triggering a trace. It must look like it was always part of the system.",
                1 => $"Push '{file}' into the {sys}. Blend it in with existing data and ghost out before anyone runs a comparison.",
                _ => $"Plant '{file}' inside the {sys}. No alerts, no audit trail \u2014 in and out before the next security sweep."
            },
            _ => $"Complete the objective at the {sys}."
        };
    }

    private static string FormatObjective(MatrixRunObjective obj) => obj switch
    {
        MatrixRunObjective.DownloadData => "Download data",
        MatrixRunObjective.UploadData   => "Upload data",
        MatrixRunObjective.DeleteData   => "Delete data",
        MatrixRunObjective.CrashCpu     => "Crash CPU",
        _                               => obj.ToString()
    };

    private static string CapFirst(string s) =>
        s.Length == 0 ? s : char.ToUpper(s[0]) + s[1..];
}
