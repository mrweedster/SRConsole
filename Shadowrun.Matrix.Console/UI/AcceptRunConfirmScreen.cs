using Shadowrun.Matrix.Enums;
using Shadowrun.Matrix.Models;

namespace Shadowrun.Matrix.UI.Screens;

/// <summary>
/// Confirmation screen pushed when the player hits Accept on a Matrix contract.
/// Shows a concise summary and requires explicit confirmation (Y / N or Enter).
/// On confirm, fires the onAccepted callback and returns to Main Menu root.
/// </summary>
public sealed class AcceptRunConfirmScreen : IScreen
{
    private readonly MatrixRunEntry             _entry;
    private readonly int                        _displayIndex;
    private readonly Func<MatrixRun, IScreen?>? _onAccepted;

    // 0 = Confirm, 1 = Cancel. Default Confirm since player already reviewed details.
    private int _selected;

    public AcceptRunConfirmScreen(
        MatrixRunEntry               entry,
        int                          displayIndex,
        Func<MatrixRun, IScreen?>?   onAccepted)
    {
        _entry        = entry;
        _displayIndex = displayIndex;
        _onAccepted   = onAccepted;
        _selected     = 0; // default Confirm
    }

    public void Render(int w, int h)
    {
        MatrixRun run = _entry.Run;

        RenderHelper.DrawWindowOpen("[Accept Matrix Contract — Confirm]", w);

        RenderHelper.DrawWindowBlankLine(w);
        RenderHelper.DrawWindowCentredLine("You are about to commit to the following run:", w);
        RenderHelper.DrawWindowBlankLine(w);
        RenderHelper.DrawWindowDivider(w);

        RenderHelper.DrawWindowStatLine("Johnson:",    run.JohnsonName,                                    w);
        RenderHelper.DrawWindowStatLine("System:",     _entry.SystemName,                                  w);
        RenderHelper.DrawWindowStatLine("Difficulty:", CapFirst(run.Difficulty),                           w);
        RenderHelper.DrawWindowStatLine("Objective:",  FormatObjective(run.Objective),                     w);
        RenderHelper.DrawWindowStatLine("Target Node:", run.TargetNodeTitle,                               w);
        RenderHelper.DrawWindowStatLine("Payout:",     $"{run.BasePayNuyen}\u00a5  +{run.KarmaReward} karma", w);

        RenderHelper.DrawWindowBlankLine(w);
        RenderHelper.DrawWindowDivider(w);
        RenderHelper.DrawWindowBlankLine(w);
        RenderHelper.DrawWindowCentredLine("The run will be tracked — complete the objective to collect payment.", w);
        RenderHelper.DrawWindowBlankLine(w);
        RenderHelper.DrawWindowClose(w);

        VC.WriteLine();
        VC.Write("  CONFIRM?  ");
        RenderHelper.WriteInlineChoice(" [Y] Accept — start run ", _selected == 0);
        VC.Write("  ");
        RenderHelper.WriteInlineChoice(" [N] Cancel ", _selected == 1);
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

        if (key.KeyChar is 'y' or 'Y') return Confirm();
        if (key.KeyChar is 'n' or 'N') return NavigationToken.Back;

        if (key.Key == ConsoleKey.Enter)
            return _selected == 0 ? Confirm() : NavigationToken.Back;

        return null;
    }

    private IScreen Confirm()
    {
        // Always pass a fresh MatrixRun instance so a previously-failed attempt
        // (which left IsComplete=true on the catalog entry) cannot block objective
        // detection on a retry.  The catalog entry acts purely as a template.
        IScreen? next = _onAccepted?.Invoke(_entry.Run.CreateFresh());
        return next ?? NavigationToken.Root;
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
