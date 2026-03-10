using Shadowrun.Matrix.Models;
using MatrixProgram = Shadowrun.Matrix.Models.Program;

namespace Shadowrun.Matrix.UI.Screens;

/// <summary>Screen 12 — Programs → Loaded → [N]. Load or unload a single program.</summary>
public sealed class ProgramsLoadedSubmenuScreen : MenuScreen
{
    private readonly Cyberdeck     _deck;
    private readonly MatrixProgram _program;
    private readonly int           _slotIndex;
    private readonly int           _parentDisplayIndex;

    public ProgramsLoadedSubmenuScreen(
        Cyberdeck deck, MatrixProgram program, int slotIndex, int parentDisplayIndex)
    {
        _deck               = deck;
        _program            = program;
        _slotIndex          = slotIndex;
        _parentDisplayIndex = parentDisplayIndex;
    }

    protected override int GetItemCount() => 1;

    protected override IScreen? OnItemConfirmed(int index)
    {
        if (index != 0) return null;

        if (_program.IsLoaded)
        {
            int slot = FindSlotIndex();
            if (slot < 0) { PendingError = "Could not locate program in deck slots."; return null; }
            var result = _deck.UnloadProgram(slot);
            if (result.IsFailure) { PendingError = result.Error; return null; }
        }
        else
        {
            var result = _deck.LoadProgram(_program);
            if (result.IsFailure) { PendingError = result.Error; return null; }
        }
        return NavigationToken.Back;
    }

    public override void Render(int w, int h)
    {
        string action = _program.IsLoaded ? "Unload" : "Load";
        string title  = $"[Loaded -> [{_parentDisplayIndex}] — {_program.Spec.Name}]";

        RenderHelper.DrawWindowOpen(title, w);
        RenderHelper.DrawWindowCentredLine(_deck.Name, w);
        RenderHelper.DrawWindowStatLine("Program:", _program.Spec.Name.ToString(), w);
        RenderHelper.DrawWindowStatLine("Level:",   _program.Spec.Level.ToString(), w);
        RenderHelper.DrawWindowStatLine("Status:",  _program.IsLoaded ? "Loaded" : "Not loaded", w);
        RenderHelper.DrawWindowDivider(w);
        RenderHelper.DrawWindowMenuItem(1, action, null, SelectedIndex == 0, w);
        RenderHelper.DrawWindowClose(w);

        VC.WriteLine();
        VC.WriteLine("  Selection:".PadRight(w));
        if (PendingError is not null) { RenderHelper.DrawErrorLine(PendingError, w); PendingError = null; }
    }

    private int FindSlotIndex()
    {
        for (int i = 0; i < _deck.LoadedSlots.Count; i++)
            if (ReferenceEquals(_deck.LoadedSlots[i], _program)) return i;
        return -1;
    }
}
