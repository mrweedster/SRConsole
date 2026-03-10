using Shadowrun.Matrix.Data;
using Shadowrun.Matrix.Enums;
using Shadowrun.Matrix.Models;
using Shadowrun.Matrix.ValueObjects;
using MatrixProgram = Shadowrun.Matrix.Models.Program;

namespace Shadowrun.Matrix.UI.Screens;

/// <summary>
/// Screen — Black Market.
/// Four-tab shop: Cyberdecks | Programs | Upgrades | Data (sell files).
/// Uses a MenuScreen-style tab selector at the top, then a scrollable item list.
/// </summary>
public sealed class BlackMarketScreen : MenuScreen
{
    private readonly Decker _decker;

    // Tabs: 0=Decks, 1=Programs, 2=Upgrades, 3=Data
    private int _tab = 0;

    public BlackMarketScreen(Decker decker) { _decker = decker; }

    // One entry per distinct program name — dynamically computed from decker state.
    private List<(ProgramListing Listing, MatrixProgram? Existing, bool IsMax)> GetEffectiveProgramListings()
    {
        var result = new List<(ProgramListing, MatrixProgram?, bool)>();
        var names  = BlackMarketCatalog.Programs.Select(p => p.ProgramName).Distinct().ToList();
        foreach (string name in names)
        {
            // Highest-level copy the decker owns, or null
            MatrixProgram? existing = _decker.Deck.Programs
                .Where(p => p.Spec.Name.ToString() == name)
                .OrderByDescending(p => p.Spec.Level)
                .FirstOrDefault();

            int ownedLevel  = existing?.Spec.Level ?? 0;
            int targetLevel = ownedLevel + 1;
            int maxLevel    = BlackMarketCatalog.Programs.Where(p => p.ProgramName == name).Max(p => p.Level);

            bool isMax = ownedLevel >= maxLevel;

            // Listing for the level we'd be buying/upgrading to (or the max listing if capped)
            var listing = BlackMarketCatalog.Programs
                .FirstOrDefault(p => p.ProgramName == name && p.Level == (isMax ? maxLevel : targetLevel))
                ?? BlackMarketCatalog.Programs.First(p => p.ProgramName == name);

            result.Add((listing, existing, isMax));
        }
        return result;
    }

    protected override int GetItemCount() => _tab switch
    {
        0 => BlackMarketCatalog.Decks.Count + (_decker.Deck.IsBroken ? 1 : 0),
        1 => GetEffectiveProgramListings().Count,
        2 => BlackMarketCatalog.Upgrades.Count,
        3 => _decker.Deck.DataFiles.Count,
        _ => 0
    };

    protected override IScreen? OnItemConfirmed(int index) => _tab switch
    {
        0 when _decker.Deck.IsBroken && index == 0
              => new BlackMarketRepairDeckScreen(_decker),
        0     => new BlackMarketBuyDeckScreen(_decker,
                      BlackMarketCatalog.Decks[_decker.Deck.IsBroken ? index - 1 : index]),
        1 => OpenProgramScreen(index),
        2 => new BlackMarketBuyUpgradeScreen(_decker, BlackMarketCatalog.Upgrades[index]),
        3 => new DataSellConfirmScreen(_decker, _decker.Deck.DataFiles[index]),
        _ => null
    };

    private IScreen? OpenProgramScreen(int index)
    {
        var list = GetEffectiveProgramListings();
        if (index < 0 || index >= list.Count) return null;
        var (listing, existing, isMax) = list[index];
        if (isMax) { PendingError = $"{listing.ProgramName} is already at max level."; return null; }
        return new BlackMarketBuyProgramScreen(_decker, listing, existing);
    }

    public override IScreen? HandleInput(ConsoleKeyInfo key)
    {
        // Left/right arrow always switches tab
        if (key.Key == ConsoleKey.LeftArrow && key.Modifiers == ConsoleModifiers.None)
        {
            _tab = (_tab - 1 + 4) % 4;
            ResetCursor();
            return null;
        }
        if (key.Key == ConsoleKey.RightArrow && key.Modifiers == ConsoleModifiers.None)
        {
            _tab = (_tab + 1) % 4;
            ResetCursor();
            return null;
        }

        // Tab hotkeys
        if (key.KeyChar is 'd' or 'D') { _tab = 0; ResetCursor(); return null; }
        if (key.KeyChar is 'p' or 'P') { _tab = 1; ResetCursor(); return null; }
        if (key.KeyChar is 'u' or 'U') { _tab = 2; ResetCursor(); return null; }
        if (key.KeyChar is 's' or 'S') { _tab = 3; ResetCursor(); return null; }

        return base.HandleInput(key);
    }

    public override void Render(int w, int h)
    {
        int inner = w - 2;

        // Overhead: open(3) + tabs(1) + divider(1) + nuyen(1) + divider(1)
        //           + scroll×2(2) + close(1) + blank(1) + prompt(1) = 12
        int visibleRows = Math.Max(1, h - 12);

        RenderHelper.DrawWindowOpen("[Main Menu -> Black Market]", w);

        // Tab bar — 4 equal columns
        int col = inner / 4;
        VC.Write("║");
        WriteTab(" [D] Decks      ", _tab == 0, col);
        WriteTab(" [P] Programs   ", _tab == 1, col);
        WriteTab(" [U] Upgrades   ", _tab == 2, col);
        WriteTab(" [S] Sell Data  ", _tab == 3, inner - col * 3);
        VC.WriteLine("║");

        RenderHelper.DrawWindowDivider(w);

        // Nuyen display
        VC.Write("║");
        VC.ForegroundColor = ConsoleColor.Yellow;
        string nuyenLine = $"  Nuyen: {_decker.Nuyen}\u00a5";
        VC.Write(nuyenLine.PadRight(inner));
        VC.ResetColor();
        VC.WriteLine("║");
        RenderHelper.DrawWindowDivider(w);

        int count        = GetItemCount();
        bool canScrollUp = ScrollOffset > 0;
        bool canScrollDn = ScrollOffset + visibleRows < count;

        RenderHelper.DrawWindowScrollUp(canScrollUp, w);

        if (count == 0)
        {
            RenderHelper.DrawWindowCentredLine("No items available.", w);
        }
        else
        {
            var effectiveProgs = _tab == 1 ? GetEffectiveProgramListings() : null;
            int negRating = _decker.NegotiationSkill;
            bool broken   = _decker.Deck.IsBroken;
            for (int i = ScrollOffset; i < Math.Min(count, ScrollOffset + visibleRows); i++)
            {
                bool sel = (i == SelectedIndex);
                switch (_tab)
                {
                    case 0:
                        if (broken && i == 0)
                            RenderRepairItem(sel, w, negRating);
                        else
                        {
                            int di = broken ? i - 1 : i;
                            RenderDeckItem(BlackMarketCatalog.Decks[di], i + 1, sel, w, negRating);
                        }
                        break;
                    case 1:
                        var (pl, ex, isMax) = effectiveProgs![i];
                        RenderProgItem(pl, ex, isMax, i + 1, sel, w, negRating);
                        break;
                    case 2: RenderUpgItem(BlackMarketCatalog.Upgrades[i],  i + 1, sel, w, negRating); break;
                    case 3: RenderDataItem(_decker.Deck.DataFiles[i],      i + 1, sel, w, negRating); break;
                }
            }
        }

        RenderHelper.DrawWindowScrollDown(canScrollDn, w);
        RenderHelper.DrawWindowClose(w);
        VC.WriteLine();
        VC.WriteLine("  [↑↓] Browse  [Enter] Select  [←→/D/P/U/S] Switch tab  [Esc] Back".PadRight(w));

        if (PendingError is not null) { RenderHelper.DrawErrorLine(PendingError, w); PendingError = null; }
    }

    private static void RenderDeckItem(DeckListing d, int idx, bool sel, int w, int negRating)
    {
        int    effPrice = NegDiscount(d.Price, negRating);
        string left  = $"  [{idx}]  {d.Name}";
        string right = $"{effPrice,9}\u00a5  MPCP:{d.Mpcp}";
        RenderItem(left, right, sel, w);
    }

    private static void RenderProgItem(ProgramListing p, MatrixProgram? existing, bool isMax, int idx, bool sel, int w, int negRating)
    {
        int    ownedLevel = existing?.Spec.Level ?? 0;
        int    effPrice   = ProgramEffectivePrice(p, negRating);
        string tag   = isMax      ? " [MAX]"
                     : existing != null ? $" [Upgrade L{ownedLevel}\u2192L{p.Level}]"
                     :                    " [New]";
        string left  = $"  [{idx}]  {p.ProgramName,-12}{tag}";
        string right = isMax ? "  ---" : $"{effPrice,8}\u00a5  {p.SizeInMp}Mp";
        int inner = w - 2;
        int gap   = Math.Max(1, inner - left.Length - right.Length);
        string content = (left + new string(' ', gap) + right).PadRight(inner);
        if (content.Length > inner) content = content[..inner];
        VC.Write("\u2551");
        if (isMax)        VC.ForegroundColor = ConsoleColor.DarkGray;
        else if (sel)     { VC.BackgroundColor = ConsoleColor.Green; VC.ForegroundColor = ConsoleColor.Black; }
        else if (existing != null) VC.ForegroundColor = ConsoleColor.Cyan;
        VC.Write(content);
        VC.ResetColor();
        VC.WriteLine("\u2551");
    }

    private static void RenderUpgItem(UpgradeListing u, int idx, bool sel, int w, int negRating)
    {
        int    effCost = NegDiscount(u.CostPerPoint, negRating);
        string left  = $"  [{idx}]  {u.StatName,-15}";
        string right = $"{effCost,7}\u00a5/pt";
        RenderItem(left, right, sel, w);
    }

    private static void RenderDataItem(Shadowrun.Matrix.ValueObjects.DataFile f, int idx, bool sel, int w, int negRating)
    {
        int    effValue = NegBonus(f.NuyenValue, negRating);
        string plotTag = f.IsPlotRelevant ? " \u26a0PLOT" : "";
        string left    = $"  [{idx}]  {f.Name}{plotTag}";
        string right   = f.IsPlotRelevant ? "  not for sale" : $"{effValue,8}\u00a5";
        RenderItem(left, right, sel, w);
    }

    private static void RenderItem(string left, string right, bool sel, int w)
    {
        int inner = w - 2;
        int gap   = Math.Max(1, inner - left.Length - right.Length);
        string content = left + new string(' ', gap) + right;
        if (content.Length > inner) content = content[..inner];
        content = content.PadRight(inner);

        VC.Write("║");
        if (sel)
        {
            VC.BackgroundColor = ConsoleColor.Green;
            VC.ForegroundColor = ConsoleColor.Black;
        }
        VC.Write(content);
        VC.ResetColor();
        VC.WriteLine("║");
    }

    private void RenderRepairItem(bool sel, int w, int negRating)
    {
        int    repairCost = BlackMarketRepairDeckScreen.RepairCost(_decker.Deck);
        int    effCost    = NegDiscount(repairCost, negRating);
        string left  = "  [!]  *** REPAIR MPCP — Deck is BROKEN ***";
        string right = $"{effCost,9}\u00a5";
        int inner = w - 2;
        int gap   = Math.Max(1, inner - left.Length - right.Length);
        string content = (left + new string(' ', gap) + right).PadRight(inner);
        if (content.Length > inner) content = content[..inner];
        VC.Write("\u2551");
        if (sel) { VC.BackgroundColor = ConsoleColor.DarkRed; VC.ForegroundColor = ConsoleColor.White; }
        else       VC.ForegroundColor = ConsoleColor.Red;
        VC.Write(content);
        VC.ResetColor();
        VC.WriteLine("\u2551");
    }

    private static void WriteTab(string label, bool active, int colW)
    {
        label = label.PadRight(colW)[..colW];
        if (active)
        {
            VC.BackgroundColor = ConsoleColor.DarkGreen;
            VC.ForegroundColor = ConsoleColor.White;
        }
        else
        {
            VC.ForegroundColor = ConsoleColor.DarkGray;
        }
        VC.Write(label);
        VC.ResetColor();
    }

    // ── Negotiation price helpers (shared by all tabs) ────────────────────────

    /// <summary>
    /// Effective deck price using the Genesis source table linear formula:
    /// no discount for neg 0-2; fixed DiscountPerNegLevel subtracted for each
    /// level above 2 (neg 3 = 1× discount, neg 12 = 10× discount).
    /// </summary>
    internal static int DeckEffectivePrice(DeckListing d, int neg) =>
        Math.Max(0, d.Price - d.DiscountPerNegLevel * Math.Max(0, neg - 2));

    /// <summary>
    /// Effective program price — same linear formula as decks, driven by the
    /// per-level <see cref="ProgramListing.DiscountPerNegLevel"/> from the Genesis tables.
    /// </summary>
    internal static int ProgramEffectivePrice(ProgramListing p, int neg) =>
        Math.Max(0, p.Price - p.DiscountPerNegLevel * Math.Max(0, neg - 2));

    /// <summary>Sale value boosted 5 % compound per negotiation level.</summary>
    internal static int NegBonus(int baseValue, int neg) =>
        (int)Math.Round(baseValue * Math.Pow(1.05, neg));

    /// <summary>Buy price reduced 5 % compound per negotiation level.</summary>
    internal static int NegDiscount(int basePrice, int neg) =>
        (int)Math.Round(basePrice / Math.Pow(1.05, neg));
}

// ── Buy sub-screens ───────────────────────────────────────────────────────────

/// <summary>Confirm + purchase a cyberdeck.</summary>
public sealed class BlackMarketBuyDeckScreen : MenuScreen
{
    private readonly Decker      _decker;
    private readonly DeckListing _deck;

    public BlackMarketBuyDeckScreen(Decker decker, DeckListing deck)
    {
        _decker = decker;
        _deck   = deck;
        SetSelectedIndex(1); // default No
    }

    protected override int GetItemCount() => 2;
    protected override IScreen? OnItemConfirmed(int index)
    {
        if (index == 0) return TryBuy();
        return NavigationToken.Back;
    }

    public override void Render(int w, int h)
    {
        int effPrice = BlackMarketScreen.NegDiscount(_deck.Price, _decker.NegotiationSkill);
        RenderHelper.DrawWindowOpen($"[Buy Cyberdeck — {_deck.Name}]", w);
        RenderHelper.DrawWindowBlankLine(w);
        RenderHelper.DrawWindowStatLine("MPCP:",         _deck.Mpcp.ToString(),                                              w);
        RenderHelper.DrawWindowStatLine("Hardening:",    _deck.Hardening.ToString(),                                        w);
        RenderHelper.DrawWindowStatLine("Response:",     _deck.Response.ToString(),                                         w);
        RenderHelper.DrawWindowStatLine("Memory:",       $"{_deck.Memory} / {_deck.MemoryMax} Mp",                          w);
        RenderHelper.DrawWindowStatLine("Storage:",      $"{_deck.Storage} / {_deck.StorageMax} Mp",                       w);
        RenderHelper.DrawWindowStatLine("Load I/O:",     $"{_deck.LoadIoSpeed} / {_deck.LoadIoSpeedMax}",                  w);
        RenderHelper.DrawWindowStatLine("Bod/Eva/Msk/Sns:", $"{_deck.Bod}/{_deck.Evasion}/{_deck.Masking}/{_deck.Sensor}", w);
        RenderHelper.DrawWindowBlankLine(w);
        RenderHelper.DrawWindowStatLine("Price:",        $"{effPrice}\u00a5",                                               w);
        RenderHelper.DrawWindowStatLine("Your nuyen:",   $"{_decker.Nuyen}\u00a5",                                         w);
        RenderHelper.DrawWindowBlankLine(w);
        RenderHelper.DrawWindowWrappedText(_deck.Description, w, 2);
        RenderHelper.DrawWindowBlankLine(w);
        RenderHelper.DrawWindowDivider(w);
        RenderHelper.DrawWindowMenuItem(1, "BUY (replaces current deck)", null, SelectedIndex == 0, w);
        RenderHelper.DrawWindowMenuItem(2, "Cancel",                       null, SelectedIndex == 1, w);
        RenderHelper.DrawWindowClose(w);
        VC.WriteLine();
        VC.WriteLine("  Selection:".PadRight(w));
        if (PendingError is not null) { RenderHelper.DrawErrorLine(PendingError, w); PendingError = null; }
    }

    private IScreen? TryBuy()
    {
        int effPrice = BlackMarketScreen.NegDiscount(_deck.Price, _decker.NegotiationSkill);
        if (_decker.Nuyen < effPrice) { PendingError = "Insufficient nuyen."; return null; }
        try
        {
            var spendResult = _decker.SpendNuyen(effPrice);
            if (spendResult.IsFailure) { PendingError = spendResult.Error; return null; }
            var newDeck = new Cyberdeck(_deck.Name, _deck.ToDeckStats());
            _decker.SwapDeck(newDeck);
            PendingError = $"Purchase complete — {_deck.Name} installed!";
            return NavigationToken.Back;
        }
        catch (Exception ex)
        {
            PendingError = $"Purchase failed: {ex.Message}";
            return null;
        }
    }
}

/// <summary>Confirm + purchase a program.</summary>
public sealed class BlackMarketBuyProgramScreen : MenuScreen
{
    private readonly Decker          _decker;
    private readonly ProgramListing  _prog;
    private readonly MatrixProgram?  _existing;   // non-null = this is an upgrade

    public BlackMarketBuyProgramScreen(Decker decker, ProgramListing prog, MatrixProgram? existing = null)
    {
        _decker   = decker;
        _prog     = prog;
        _existing = existing;
        SetSelectedIndex(1); // default No
    }

    protected override int GetItemCount() => 2;
    protected override IScreen? OnItemConfirmed(int index)
    {
        if (index == 0) return TryBuy();
        return NavigationToken.Back;
    }

    public override void Render(int w, int h)
    {
        int  effPrice = BlackMarketScreen.ProgramEffectivePrice(_prog, _decker.NegotiationSkill);
        bool   isUpgrade = _existing is not null;
        string title     = isUpgrade
            ? $"[Upgrade — {_prog.ProgramName} L{_existing!.Spec.Level}\u2192L{_prog.Level}]"
            : $"[Buy Program — {_prog.ProgramName} L{_prog.Level}]";

        RenderHelper.DrawWindowOpen(title, w);
        RenderHelper.DrawWindowBlankLine(w);
        RenderHelper.DrawWindowStatLine("Program:",    _prog.ProgramName,    w);
        if (isUpgrade)
            RenderHelper.DrawWindowStatLine("Upgrade:", $"L{_existing!.Spec.Level} \u2192 L{_prog.Level}", w);
        else
            RenderHelper.DrawWindowStatLine("Level:",   _prog.Level.ToString(), w);
        RenderHelper.DrawWindowStatLine("Category:",   _prog.Category,       w);
        RenderHelper.DrawWindowStatLine("Size:",       $"{_prog.SizeInMp} Mp", w);
        RenderHelper.DrawWindowStatLine("Price:",      $"{effPrice}\u00a5", w);
        RenderHelper.DrawWindowStatLine("Your nuyen:", $"{_decker.Nuyen}\u00a5", w);
        RenderHelper.DrawWindowBlankLine(w);
        RenderHelper.DrawWindowWrappedText(_prog.Description, w, 2);
        RenderHelper.DrawWindowBlankLine(w);

        int freedStorage     = isUpgrade ? _existing!.Spec.SizeInMp : 0;
        int availableStorage = _decker.Deck.FreeStorage() + freedStorage;
        if (availableStorage < _prog.SizeInMp)
        {
            VC.Write("\u2551");
            VC.ForegroundColor = ConsoleColor.Red;
            VC.Write($"  \u26a0 Insufficient storage ({availableStorage}/{_prog.SizeInMp}Mp needed)".PadRight(w - 2));
            VC.ResetColor();
            VC.WriteLine("\u2551");
        }

        RenderHelper.DrawWindowDivider(w);
        string buyLabel = isUpgrade ? $"UPGRADE to L{_prog.Level}" : "BUY — install to storage";
        RenderHelper.DrawWindowMenuItem(1, buyLabel, null, SelectedIndex == 0, w);
        RenderHelper.DrawWindowMenuItem(2, "Cancel",  null, SelectedIndex == 1, w);
        RenderHelper.DrawWindowClose(w);
        VC.WriteLine();
        VC.WriteLine("  Selection:".PadRight(w));
        if (PendingError is not null) { RenderHelper.DrawErrorLine(PendingError, w); PendingError = null; }
    }

    private IScreen? TryBuy()
    {
        int effPrice = BlackMarketScreen.ProgramEffectivePrice(_prog, _decker.NegotiationSkill);
        if (_decker.Nuyen < effPrice) { PendingError = "Insufficient nuyen."; return null; }
        bool isUpgrade    = _existing is not null;
        int  freedStorage = isUpgrade ? _existing!.Spec.SizeInMp : 0;
        int  avail        = _decker.Deck.FreeStorage() + freedStorage;
        if (avail < _prog.SizeInMp) { PendingError = $"Not enough storage ({avail}/{_prog.SizeInMp}Mp)."; return null; }
        try
        {
            var spendResult = _decker.SpendNuyen(effPrice);
            if (spendResult.IsFailure) { PendingError = spendResult.Error; return null; }

            if (isUpgrade) _decker.Deck.DeleteProgram(_existing!);

            // Derive ProgramType from size formula: Small=2·L², Medium=3·L², Large=4·L²
            int l2 = _prog.Level * _prog.Level;
            var progType = _prog.SizeInMp >= l2 * 4 ? Shadowrun.Matrix.Enums.ProgramType.Large
                         : _prog.SizeInMp >= l2 * 3 ? Shadowrun.Matrix.Enums.ProgramType.Medium
                         :                             Shadowrun.Matrix.Enums.ProgramType.Small;
            var spec = new ProgramSpec(
                name:             Enum.Parse<ProgramName>(_prog.ProgramName),
                type:             progType,
                level:            _prog.Level,
                description:      _prog.Description,
                usefulnessRating: 5,
                reloadsAfterUse:  false,
                overrideSizeInMp: _prog.SizeInMp);
            _decker.Deck.InstallProgram(new MatrixProgram(spec));
            PendingError = isUpgrade
                ? $"Upgraded: {_prog.ProgramName} to L{_prog.Level}."
                : $"Installed: {_prog.ProgramName} L{_prog.Level} ({_prog.SizeInMp}Mp).";
            return NavigationToken.Back;
        }
        catch (Exception ex) { PendingError = $"Purchase failed: {ex.Message}"; return null; }
    }
}

/// <summary>Confirm + purchase an upgrade for the current deck.</summary>
public sealed class BlackMarketBuyUpgradeScreen : MenuScreen
{
    private readonly Decker          _decker;
    private readonly UpgradeListing  _upg;
    private int                      _qty;

    // Step size per arrow-key press.
    // Memory/MemoryMax: 12 Mp per step; Storage/StorageMax: 50 Mp per step; everything else: 1.
    private int StepSize => _upg.StatName switch
    {
        "Memory"    => 12,
        "MemoryMax" => 12,
        "Storage"   => 50,
        "StorageMax"=> 50,
        _           => 1
    };

    private string StepUnit => _upg.StatName switch
    {
        "Memory" or "MemoryMax" or "Storage" or "StorageMax" => "Mp",
        _                                                      => "pt"
    };

    public BlackMarketBuyUpgradeScreen(Decker decker, UpgradeListing upg)
    {
        _decker = decker;
        _upg    = upg;
        _qty    = StepSize;    // default = one step
        SetSelectedIndex(1);   // default No
    }

    protected override int GetItemCount() => 2;
    protected override IScreen? OnItemConfirmed(int index)
    {
        if (index == 0) return TryBuy();
        return NavigationToken.Back;
    }

    public override IScreen? HandleInput(ConsoleKeyInfo key)
    {
        int step = StepSize;
        if (key.Key == ConsoleKey.LeftArrow  && _qty > step)           { _qty -= step; return null; }
        if (key.Key == ConsoleKey.RightArrow && _qty < _upg.MaxPoints) { _qty = Math.Min(_qty + step, _upg.MaxPoints); return null; }
        return base.HandleInput(key);
    }

    public override void Render(int w, int h)
    {
        int effPerPoint = BlackMarketScreen.NegDiscount(_upg.CostPerPoint, _decker.NegotiationSkill);
        int totalCost   = effPerPoint * (_qty / StepSize);   // scale: qty units, effPerPoint per step unit

        // Recalculate proportionally: totalCost = NegDiscount(base total, neg)
        totalCost = BlackMarketScreen.NegDiscount(_upg.CostPerPoint * _qty, _decker.NegotiationSkill);

        RenderHelper.DrawWindowOpen($"[Upgrade Deck — {_upg.StatName}]", w);
        RenderHelper.DrawWindowBlankLine(w);
        RenderHelper.DrawWindowStatLine("Stat:",             _upg.StatName,                                          w);
        RenderHelper.DrawWindowStatLine($"Cost/{StepUnit}:", $"{effPerPoint * StepSize}\u00a5",                      w);
        RenderHelper.DrawWindowStatLine("Max upgrade:",      $"{_upg.MaxPoints} {StepUnit}",                         w);
        RenderHelper.DrawWindowBlankLine(w);
        RenderHelper.DrawWindowWrappedText(_upg.Description, w, 2);
        RenderHelper.DrawWindowBlankLine(w);
        RenderHelper.DrawWindowDivider(w);
        RenderHelper.DrawWindowStatLine($"Amount (←→, step {StepSize}):", $"{_qty} {StepUnit}", w);
        RenderHelper.DrawWindowStatLine("Total cost:",       $"{totalCost}\u00a5",               w);
        RenderHelper.DrawWindowStatLine("Your nuyen:",       $"{_decker.Nuyen}\u00a5",           w);
        RenderHelper.DrawWindowBlankLine(w);
        RenderHelper.DrawWindowMenuItem(1, $"BUY +{_qty} {StepUnit} of {_upg.StatName}", null, SelectedIndex == 0, w);
        RenderHelper.DrawWindowMenuItem(2, "Cancel",                                      null, SelectedIndex == 1, w);
        RenderHelper.DrawWindowClose(w);
        VC.WriteLine();
        VC.WriteLine($"  [←→] Adjust amount (step: {StepSize} {StepUnit})   Selection:".PadRight(w));
        if (PendingError is not null) { RenderHelper.DrawErrorLine(PendingError, w); PendingError = null; }
    }

    private IScreen? TryBuy()
    {
        int totalCost = BlackMarketScreen.NegDiscount(_upg.CostPerPoint * _qty, _decker.NegotiationSkill);
        if (_decker.Nuyen < totalCost) { PendingError = "Insufficient nuyen."; return null; }
        try
        {
            // Map the listing's StatName string to the UpgradableStat enum
            if (!Enum.TryParse<Shadowrun.Matrix.Enums.UpgradableStat>(
                    _upg.StatName.Replace(" ", "").Replace("/", ""),
                    ignoreCase: true, out var stat))
            {
                PendingError = $"Unknown stat '{_upg.StatName}'.";
                return null;
            }

            var spendResult = _decker.SpendNuyen(totalCost);
            if (spendResult.IsFailure) { PendingError = spendResult.Error; return null; }

            // Apply the upgrade: raise current value by _qty points
            int currentVal = GetCurrentStatValue(stat);
            int targetVal  = currentVal + _qty;
            if (_decker.Deck.CanUpgradeStat(stat, targetVal))
            {
                _decker.Deck.UpgradeStat(stat, targetVal);
            }
            else
            {
                // Apply as many as possible
                int max = currentVal;
                while (_decker.Deck.CanUpgradeStat(stat, max + 1)) max++;
                if (max > currentVal)
                    _decker.Deck.UpgradeStat(stat, max);
                else
                {
                    PendingError = $"Stat {stat} is already at its maximum.";
                    _decker.AddNuyen(totalCost); // refund
                    return null;
                }
            }

            PendingError = $"Upgrade applied: {_upg.StatName} +{_qty} {StepUnit}.";
            return NavigationToken.Back;
        }
        catch (Exception ex) { PendingError = $"Upgrade failed: {ex.Message}"; return null; }
    }

    private int GetCurrentStatValue(Shadowrun.Matrix.Enums.UpgradableStat stat)
    {
        var s = _decker.Deck.Stats;
        return stat switch
        {
            Shadowrun.Matrix.Enums.UpgradableStat.Response      => s.Response,
            Shadowrun.Matrix.Enums.UpgradableStat.Memory        => s.Memory,
            Shadowrun.Matrix.Enums.UpgradableStat.MemoryMax     => s.MemoryMax,
            Shadowrun.Matrix.Enums.UpgradableStat.Storage       => s.Storage,
            Shadowrun.Matrix.Enums.UpgradableStat.StorageMax    => s.StorageMax,
            Shadowrun.Matrix.Enums.UpgradableStat.LoadIoSpeed   => s.LoadIoSpeed,
            Shadowrun.Matrix.Enums.UpgradableStat.LoadIoSpeedMax=> s.LoadIoSpeedMax,
            Shadowrun.Matrix.Enums.UpgradableStat.Bod           => s.Bod,
            Shadowrun.Matrix.Enums.UpgradableStat.Evasion       => s.Evasion,
            Shadowrun.Matrix.Enums.UpgradableStat.Masking       => s.Masking,
            Shadowrun.Matrix.Enums.UpgradableStat.Sensor        => s.Sensor,
            _                                                    => 0
        };
    }
}

/// <summary>Confirm + pay for an MPCP repair. Only reachable when IsBroken == true.</summary>
public sealed class BlackMarketRepairDeckScreen : MenuScreen
{
    private readonly Decker _decker;

    public BlackMarketRepairDeckScreen(Decker decker)
    {
        _decker = decker;
        SetSelectedIndex(1); // default No
    }

    /// <summary>Repair cost = MPCP × 1 000 ¥ (reduced by negotiation in the confirm screen).</summary>
    public static int RepairCost(Cyberdeck deck) => deck.Stats.Mpcp * 1_000;

    protected override int GetItemCount() => 2;
    protected override IScreen? OnItemConfirmed(int index)
    {
        if (index == 0) return TryRepair();
        return NavigationToken.Back;
    }

    public override void Render(int w, int h)
    {
        int effCost = BlackMarketScreen.NegDiscount(RepairCost(_decker.Deck), _decker.NegotiationSkill);

        RenderHelper.DrawWindowOpen("[Repair MPCP]", w);
        RenderHelper.DrawWindowBlankLine(w);
        VC.Write("\u2551"); VC.ForegroundColor = ConsoleColor.Red;
        VC.Write($"  \u26a0  {_decker.Deck.Name} — MPCP is damaged!".PadRight(w - 2));
        VC.ResetColor(); VC.WriteLine("\u2551");
        RenderHelper.DrawWindowBlankLine(w);
        RenderHelper.DrawWindowStatLine("MPCP rating:", _decker.Deck.Stats.Mpcp.ToString(), w);
        RenderHelper.DrawWindowStatLine("Repair cost:", $"{effCost}\u00a5", w);
        RenderHelper.DrawWindowStatLine("Your nuyen:",  $"{_decker.Nuyen}\u00a5", w);
        RenderHelper.DrawWindowBlankLine(w);
        RenderHelper.DrawWindowDivider(w);
        RenderHelper.DrawWindowMenuItem(1, "REPAIR — restore MPCP", null, SelectedIndex == 0, w);
        RenderHelper.DrawWindowMenuItem(2, "Cancel",                 null, SelectedIndex == 1, w);
        RenderHelper.DrawWindowClose(w);
        VC.WriteLine();
        VC.WriteLine("  Selection:".PadRight(w));
        if (PendingError is not null) { RenderHelper.DrawErrorLine(PendingError, w); PendingError = null; }
    }

    private IScreen? TryRepair()
    {
        int effCost = BlackMarketScreen.NegDiscount(RepairCost(_decker.Deck), _decker.NegotiationSkill);
        if (_decker.Nuyen < effCost) { PendingError = "Insufficient nuyen."; return null; }
        try
        {
            var spend = _decker.SpendNuyen(effCost);
            if (spend.IsFailure) { PendingError = spend.Error; return null; }
            _decker.Deck.RepairMpcp();
            PendingError = "MPCP repaired — deck is operational again!";
            return NavigationToken.Back;
        }
        catch (Exception ex) { PendingError = $"Repair failed: {ex.Message}"; return null; }
    }
}
