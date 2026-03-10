using Shadowrun.Matrix.Enums;
using Shadowrun.Matrix.Models;

namespace Shadowrun.Matrix.UI.Screens;

/// <summary>
/// Screen 15 — Matrix Contract detail and accept/decline prompt.
/// Stats and narrative sit inside a unified window. The Accept/Decline
/// prompt lives below as interface chrome (outside the window border).
/// </summary>
public sealed class MatrixContractSubmenuScreen : IScreen
{
    private readonly MatrixRunEntry           _entry;
    private readonly int                      _displayIndex;
    private readonly Func<MatrixRun, IScreen?>? _onAccepted;

    // 0 = Accept, 1 = Decline. Default to Decline (safer).
    private int _selected = 1;

    public MatrixContractSubmenuScreen(
        MatrixRunEntry               entry,
        int                          displayIndex,
        Func<MatrixRun, IScreen?>?   onAccepted = null)
    {
        _entry        = entry;
        _displayIndex = displayIndex;
        _onAccepted   = onAccepted;
    }

    public void Render(int w, int h)
    {
        MatrixRun run = _entry.Run;

        // ── Window ─────────────────────────────────────────────────────────────
        RenderHelper.DrawWindowOpen($"[Matrix Contracts -> [{_displayIndex}]]", w);
        RenderHelper.DrawWindowStatLine("Johnson:",    run.JohnsonName,                           w);
        RenderHelper.DrawWindowStatLine("System:",     _entry.SystemName,                         w);
        RenderHelper.DrawWindowStatLine("Difficulty:", CapFirst(run.Difficulty),                  w);
        RenderHelper.DrawWindowStatLine("Objective:",  FormatObjective(run.Objective),            w);
        RenderHelper.DrawWindowStatLine("Payout:",     $"{run.BasePayNuyen}\u00a5  +{run.KarmaReward} karma", w);
        RenderHelper.DrawWindowDivider(w);
        RenderHelper.DrawWindowWrappedText(GenerateDescription(_entry), w, indent: 2);
        RenderHelper.DrawWindowClose(w);

        // ── Accept / Decline chrome (outside window) ───────────────────────────
        VC.WriteLine();
        VC.Write("  ACCEPT?  ");
        RenderHelper.WriteInlineChoice(" [Y] Accept ",  _selected == 0);
        VC.Write("  ");
        RenderHelper.WriteInlineChoice(" [N] Decline ", _selected == 1);
        VC.WriteLine();
        VC.WriteLine();
        VC.WriteLine("  Selection:".PadRight(w));
    }

    public IScreen? HandleInput(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.Escape)    return NavigationToken.Back;
        if (key.Key == ConsoleKey.Backspace) return NavigationToken.Back;

        if (key.Key is ConsoleKey.LeftArrow  or ConsoleKey.UpArrow)   _selected = 0;
        if (key.Key is ConsoleKey.RightArrow or ConsoleKey.DownArrow) _selected = 1;

        if (key.KeyChar is 'y' or 'Y') return Accept();
        if (key.KeyChar is 'n' or 'N') return NavigationToken.Back;

        if (key.Key == ConsoleKey.Enter)
            return _selected == 0 ? Accept() : NavigationToken.Back;

        return null;
    }

    // ── Description generation ────────────────────────────────────────────────

    private static string GenerateDescription(MatrixRunEntry entry)
    {
        MatrixRun run  = entry.Run;
        string    sys  = entry.SystemName;
        string    node = run.TargetNodeTitle;
        string    file = run.ContractedFilename ?? string.Empty;
        int       seed = Math.Abs(run.Id.GetHashCode());

        return run.Objective switch
        {
            MatrixRunObjective.CrashCpu     => PickCrash(seed, sys, node),
            MatrixRunObjective.DownloadData => PickDownload(seed, sys, node, file),
            MatrixRunObjective.DeleteData   => PickDelete(seed, sys, node, file),
            MatrixRunObjective.UploadData   => PickUpload(seed, sys, node, file),
            _                               => $"Jack into {sys} and complete the objective in the {node}."
        };
    }

    private static string PickCrash(int seed, string sys, string node) =>
        (seed % 4) switch
        {
            0 => $"Jack into {sys} and bring the {node} down hard. No files, no finesse — the client wants a full blackout and doesn't care how you get there.",
            1 => $"Your target is the {node} inside {sys}. Crash it from the inside and make it look like a hardware fault. Clean, untraceable, permanent.",
            2 => $"The {node} at {sys} needs to go dark. Get in, fry it clean, and ghost out before the trace locks on your signal.",
            _ => $"Mr. Johnson wants the {node} in {sys} dead. Simple sabotage — no extraction needed, just total system destruction.",
        };

    private static string PickDownload(int seed, string sys, string node, string file) =>
        (seed % 4) switch
        {
            0 => $"Slip into {sys} and pull '{file}' from the {node} store. Clean copy, no flags — the client needs it to arrive intact and undetected.",
            1 => $"There's a file called '{file}' sitting in {sys}'s {node}. Extract it quietly, cover your exit, and don't leave a trace you were ever there.",
            2 => $"Your client wants '{file}' out of {sys}'s {node} before anyone notices it's gone. Copy it clean and walk away like nothing happened.",
            _ => $"Get into {sys}, locate '{file}' in the {node}, and exfil the data. The client needs it uncorrupted — sloppy work is not acceptable.",
        };

    private static string PickDelete(int seed, string sys, string node, string file) =>
        (seed % 4) switch
        {
            0 => $"'{file}' in {sys}'s {node} needs to stop existing. Wipe it clean and leave no trace it was ever stored there.",
            1 => $"Burn '{file}' out of the {node} inside {sys}. The kind of deleted that courts can't undo and auditors can't recover.",
            2 => $"Someone wants '{file}' erased from {sys}'s {node} before it can be pulled as evidence. Make it disappear — permanently.",
            _ => $"Jack into {sys} and purge '{file}' from the {node}. No backups, no shadows, no recovery path — total erasure.",
        };

    private static string PickUpload(int seed, string sys, string node, string file) =>
        (seed % 4) switch
        {
            0 => $"Smuggle '{file}' into {sys}'s {node} without triggering a trace. It needs to look like it was always part of the system.",
            1 => $"Push '{file}' into the {node} at {sys}. Blend it in with the existing data and ghost out before anyone runs a comparison.",
            2 => $"Plant '{file}' inside {sys}'s {node}. No alerts, no audit trail — in and out before the next scheduled security sweep.",
            _ => $"Your job is to drop '{file}' into {sys}'s {node}. Upload clean, authenticate the file signature, then vanish without a footprint.",
        };

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IScreen? Accept() =>
        new AcceptRunConfirmScreen(_entry, _displayIndex, _onAccepted);

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
