using Shadowrun.Matrix.Models;
using MatrixProgram = Shadowrun.Matrix.Models.Program;

namespace Shadowrun.Matrix.UI.Screens;

/// <summary>
/// Action screen for a single installed program.
/// Offers: [1] Load to cyberdeck  [2] Delete permanently.
/// When <paramref name="midSession"/> is true the load call passes midSession=true
/// so the program starts at 0% progress and loads at the deck's Load/IO speed.
/// </summary>
public sealed class ProgramInstalledActionScreen : MenuScreen
{
    private readonly Cyberdeck     _deck;
    private readonly MatrixProgram _program;
    private readonly bool          _midSession;

    public ProgramInstalledActionScreen(Cyberdeck deck, MatrixProgram program, bool midSession)
    {
        _deck       = deck;
        _program    = program;
        _midSession = midSession;
        SetSelectedIndex(0);
    }

    protected override int GetItemCount() => 2;

    protected override IScreen? OnItemConfirmed(int index)
    {
        if (index == 0)   // Load
        {
            if (_program.IsLoaded)
            {
                PendingError = $"{_program.Spec.Name} is already loaded in a slot.";
                return null;
            }
            var result = _deck.LoadProgram(_program, _midSession);
            if (result.IsFailure) { PendingError = result.Error; return null; }
            string msg = _midSession
                ? $"Loading {_program.Spec.Name} — progress visible in Matrix screen."
                : $"{_program.Spec.Name} loaded and ready.";
            PendingError = msg;   // re-use PendingError as a success notice (one render)
            return NavigationToken.Back;
        }
        if (index == 1)   // Delete
            return new ProgramDeleteConfirmScreen(_deck, _program);

        return null;
    }

    public override IScreen? HandleInput(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.Escape)    return NavigationToken.Back;
        if (key.Key == ConsoleKey.Backspace) return NavigationToken.Back;
        if (key.KeyChar is 'l' or 'L') return OnItemConfirmed(0);
        if (key.KeyChar is 'd' or 'D') return OnItemConfirmed(1);
        return base.HandleInput(key);
    }

    public override void Render(int w, int h)
    {
        string progName = _program.Spec.Name.ToString();
        string title    = $"[Installed — {progName} L{_program.Spec.Level}]";

        string statusStr = _program.IsLoaded
            ? $"Loaded  ({_program.LoadProgress:P0} ready)"
            : "Not loaded";

        string sizeStr  = $"{_program.Spec.SizeInMp}Mp";
        string memAvail = $"{_deck.FreeMemory()}Mp free";
        string freeSlot = _deck.FreeSlotCount() > 0
            ? $"{_deck.FreeSlotCount()} slot(s) free"
            : "No free slots";

        string loadNote = _midSession
            ? $"  (Will load at {_deck.Stats.LoadIoSpeed} Io/s — visible in Matrix screen)"
            : "  (Loads instantly — not in Matrix)";

        RenderHelper.DrawWindowOpen(title, w);
        RenderHelper.DrawWindowStatLine("Program:", progName,        w);
        RenderHelper.DrawWindowStatLine("Level:",   _program.Spec.Level.ToString(), w);
        RenderHelper.DrawWindowStatLine("Size:",    sizeStr,         w);
        RenderHelper.DrawWindowStatLine("Status:",  statusStr,       w);
        RenderHelper.DrawWindowStatLine("Memory:",  memAvail,        w);
        RenderHelper.DrawWindowStatLine("Slots:",   freeSlot,        w);
        if (_midSession)
        {
            RenderHelper.DrawWindowDivider(w);
            RenderHelper.DrawWindowCentredLine(loadNote, w);
        }
        RenderHelper.DrawWindowDivider(w);
        RenderHelper.DrawWindowMenuItem(1, "[L] Load to cyberdeck", null, SelectedIndex == 0, w);
        RenderHelper.DrawWindowMenuItem(2, "[D] Delete permanently", null, SelectedIndex == 1, w);
        RenderHelper.DrawWindowClose(w);
        VC.WriteLine();
        VC.WriteLine("  [L] Load  [D] Delete  [Backspace] Back".PadRight(w));
        if (PendingError is not null) { RenderHelper.DrawErrorLine(PendingError, w); PendingError = null; }
    }
}
