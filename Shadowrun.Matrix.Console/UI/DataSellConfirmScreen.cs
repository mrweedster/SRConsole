using Shadowrun.Matrix.Models;
using Shadowrun.Matrix.ValueObjects;

namespace Shadowrun.Matrix.UI.Screens;

/// <summary>
/// Confirm selling a data file via the Black Market → Data tab.
/// Plot-relevant files (IsPlotRelevant == true) cannot be sold and show a warning.
/// </summary>
public sealed class DataSellConfirmScreen : MenuScreen
{
    private readonly Decker   _decker;
    private readonly DataFile _file;

    public DataSellConfirmScreen(Decker decker, DataFile file)
    {
        _decker = decker;
        _file   = file;
        SetSelectedIndex(file.IsPlotRelevant ? 0 : 1);   // default No; only Cancel for plot files
    }

    protected override int GetItemCount() => _file.IsPlotRelevant ? 1 : 2;

    protected override IScreen? OnItemConfirmed(int index)
    {
        if (_file.IsPlotRelevant)
            return NavigationToken.Back;   // only Cancel available

        if (index == 0)   // Sell
        {
            int effValue = BlackMarketScreen.NegBonus(_file.NuyenValue, _decker.NegotiationSkill);
            try
            {
                _decker.Deck.RemoveDataFile(_file);
                _decker.AddNuyen(effValue);
                return NavigationToken.Back;
            }
            catch (Exception ex) { PendingError = $"Sale failed: {ex.Message}"; return null; }
        }
        return NavigationToken.Back;   // Cancel
    }

    public override IScreen? HandleInput(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.Escape)    return NavigationToken.Back;
        if (key.Key == ConsoleKey.Backspace) return NavigationToken.Back;
        if (!_file.IsPlotRelevant)
        {
            if (key.KeyChar is 'y' or 'Y') return OnItemConfirmed(0);
            if (key.KeyChar is 'n' or 'N') return NavigationToken.Back;
        }
        return base.HandleInput(key);
    }

    public override void Render(int w, int h)
    {
        int    inner    = w - 2;
        int    effValue = BlackMarketScreen.NegBonus(_file.NuyenValue, _decker.NegotiationSkill);
        string nameMax  = RenderHelper.Truncate(_file.Name, Math.Max(1, inner - 20));

        if (_file.IsPlotRelevant)
        {
            RenderHelper.DrawWindowOpen("[Sell Data File — WARNING]", w);
            RenderHelper.DrawWindowCentredLine($"'{nameMax}'", w);
            RenderHelper.DrawWindowDivider(w);
            VC.Write("\u2551");
            VC.ForegroundColor = ConsoleColor.Yellow;
            VC.Write("  \u26a0  This data looks important, better not sell it.".PadRight(inner));
            VC.ResetColor();
            VC.WriteLine("\u2551");
            RenderHelper.DrawWindowDivider(w);
            RenderHelper.DrawWindowMenuItem(1, "Cancel — keep the file", null, SelectedIndex == 0, w);
            RenderHelper.DrawWindowClose(w);
            VC.WriteLine();
            VC.WriteLine("  [Enter/Backspace] Cancel".PadRight(w));
        }
        else
        {
            string val = $"{effValue}\u00a5";
            RenderHelper.DrawWindowOpen("[Sell Data File]", w);
            RenderHelper.DrawWindowCentredLine($"Sell  '{nameMax}'  for {val}?", w);
            RenderHelper.DrawWindowStatLine("Value:", val, w);
            RenderHelper.DrawWindowStatLine("Size:",  $"{_file.SizeInMp}Mp", w);
            RenderHelper.DrawWindowStatLine("Nuyen:", $"{_decker.Nuyen}\u00a5  \u2192  {_decker.Nuyen + effValue}\u00a5 after sale", w);
            RenderHelper.DrawWindowDivider(w);
            RenderHelper.DrawWindowMenuItem(1, "Yes — sell it",   null, SelectedIndex == 0, w);
            RenderHelper.DrawWindowMenuItem(2, "No  — keep it",   null, SelectedIndex == 1, w);
            RenderHelper.DrawWindowClose(w);
            VC.WriteLine();
            VC.WriteLine("  [Y] Sell  [N] Keep  [Backspace] Back".PadRight(w));
        }

        if (PendingError is not null) { RenderHelper.DrawErrorLine(PendingError, w); PendingError = null; }
    }
}
