using Shadowrun.Matrix.Data;
using Shadowrun.Matrix.Enums;
using Shadowrun.Matrix.Models;
using Shadowrun.Matrix.UI;
using MatrixProgram = Shadowrun.Matrix.Models.Program;

namespace Shadowrun.Matrix.UI.Screens;

/// <summary>
/// Main Matrix Game Screen.
///
/// Sub-interactions (Run Program, Travel, Node Action, Deck Status) are rendered
/// as compact overlay windows drawn OVER the main screen using SetCursorPosition,
/// centered both horizontally and vertically.
///
/// Node-action results (e.g. "no usable data found") are posted to a local
/// message list that appears at the bottom of the Run Log section — no popup.
/// </summary>
public sealed class MatrixGameScreen : IScreen, ITicking
{
    private record struct CL(string Text, ConsoleColor Color = ConsoleColor.Gray);

    private readonly MatrixSession _session;
    private readonly int           _systemNumber;
    private readonly GameState     _gameState;

    private int     _lastLogCount;
    private string? _pendingError;

    // ── Overlay state ─────────────────────────────────────────────────────────
    private enum OverlayMode { None, RunProgram, Travel, NodeAction, DeckStatus }
    private OverlayMode _overlay = OverlayMode.None;

    // RunProgram cursor
    private int _programCursor = 0;

    // NodeAction overlay
    private bool _nodeGoToInput  = false;
    private int  _nodeGoToCursor = 0;

    // DeckStatus overlay
    private enum DeckPage { Main, Installed, InstalledAction, Loaded, Datastore, DatastoreDelete }
    private DeckPage _deckPage    = DeckPage.Main;
    private int      _deckCursor  = 0;
    private int      _deckSelProg = 0;
    private int      _deckSelFile = 0;   // datastore cursor
    private string?  _deckMsg     = null;

    // ── Real-time combat state ────────────────────────────────────────────────
    private float _playerCooldown    = 0f;   // seconds remaining until next action
    private float _playerCooldownMax = 5f;   // set when combat starts / Response changes
    private bool  _wasInCombat       = false; // tracks transitions for cooldown reset

    // ── Controller / action-bar cursor ───────────────────────────────────────
    // Tracks the highlighted cell in the 2×3 action bar (0-5) and inside overlays.
    private int _mainCursor       = 0;   // 0-5, action bar selection
    private int _travelCursor     = 0;   // selection inside Travel overlay
    private int _nodeActionCursor = 0;   // selection inside NodeAction overlay

    // Deck-status overlay cursors
    private int _deckMainCursor   = 0;   // 0-2 → Installed / Loaded / Datastore
    private int _deckActionCursor = 0;   // 0=Load  1=Unload  (InstalledAction page)
    private int _deckDeleteCursor = 1;   // 0=Yes   1=No      (DatastoreDelete, default No)

    // Tick spinner: | / - \ cycling every 250 ms
    private static readonly char[] SpinnerChars = ['|', '/', '-', '\\'];
    private int   _spinnerIndex  = 0;
    private float _spinnerTimer  = 0f;

    // NeedsRedraw for ITicking
    private bool _needsRedraw = false;
    public bool NeedsRedraw => _needsRedraw;

    // Pending automatic screen transition (set when session ends mid-tick)
    private IScreen? _pendingAutoTransition;
    public IScreen? PopAutoTransition()
    {
        var t = _pendingAutoTransition;
        _pendingAutoTransition = null;
        return t;
    }

    // ── Cached terminal size from last Render call (needed for overlays) ──────
    private int _lastW = 80;
    private int _lastH = 24;

    // ── Map grid layout cache ─────────────────────────────────────────────────
    private const int WIDE_W  = 150;  // terminal width above which map goes wide

    private Dictionary<string, (int gx, int gy)>? _mapLayout;
    private string? _mapLayoutSysId;

    public MatrixGameScreen(MatrixSession session, int systemNumber, GameState? gameState = null)
    {
        _session      = session;
        _systemNumber = systemNumber;
        _gameState    = gameState ?? new GameState();
        _gameState.ActiveSession      = _session;
        _gameState.ActiveSystemNumber = _systemNumber;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ITicking — real-time update
    // ═══════════════════════════════════════════════════════════════════════════

    public void Tick(float dt)
    {
        bool inCombat = _session.Persona.CombatState == CombatState.Active;

        // Detect combat start — apply cooldown so first action requires the usual wait
        if (inCombat && !_wasInCombat)
        {
            _playerCooldownMax = ComputePlayerCooldown();
            _playerCooldown    = _playerCooldownMax;
        }
        _wasInCombat = inCombat;

        // Tick engine (ICE attacks, program loading, traces, status effects)
        if (_session.IsActive)
        {
            _session.TickFrame(dt);
            _needsRedraw = true;
        }

        // Session ended mid-tick (BlackIce death, persona dumped, deck fried)
        // Set auto-transition so the game loop can switch screens immediately.
        if (!_session.IsActive && _pendingAutoTransition is null)
        {
            _pendingAutoTransition = BuildSessionEndScreen();
        }

        // Spinner always ticks so the node-info clock is always visible
        _spinnerTimer += dt;
        if (_spinnerTimer >= 0.25f)
        {
            _spinnerTimer -= 0.25f;
            _spinnerIndex  = (_spinnerIndex + 1) % SpinnerChars.Length;
            _needsRedraw   = true;
        }

        // Player cooldown countdown
        if (inCombat && _playerCooldown > 0f)
        {
            _playerCooldown = Math.Max(0f, _playerCooldown - dt);
            _needsRedraw    = true;
        }

        // Sync cooldown max to current Response stat
        _playerCooldownMax = ComputePlayerCooldown();
    }

    /// <summary>Player action cooldown: Response 0 → 5 s, Response 3 → 1 s.</summary>
    private float ComputePlayerCooldown() =>
        Math.Max(1.0f, 5.0f - _session.Decker.Deck.Stats.Response * (4.0f / 3.0f));

    // ═══════════════════════════════════════════════════════════════════════════
    // RENDER
    // ═══════════════════════════════════════════════════════════════════════════

    public void Render(int w, int h)
    {
        _lastW = w; _lastH = h;
        _needsRedraw = false;

        MatrixSystem system  = _session.System;
        Persona      persona = _session.Persona;
        Node         current = system.GetNode(persona.CurrentNodeId);
        string       nodeKey = NodeKey(current.Id);

        // ── Main screen (always fully rendered first) ──────────────────────────
        RenderHeader(system, current, nodeKey, w);
        RenderHelper.DrawWindowDivider(w);
        RenderSplitPane(system, persona, current, w);
        RenderHelper.DrawWindowDivider(w);
        RenderCombatLog(w);
        RenderHelper.DrawWindowDivider(w);
        RenderDeck(persona, w);           // yellow cursor highlight in RunProgram mode
        RenderHelper.DrawWindowDivider(w);
        RenderActions(w);
        RenderHelper.DrawWindowClose(w);

        if (_pendingError is not null)
        {
            RenderHelper.DrawErrorLine(_pendingError, w);
            _pendingError = null;
        }

        // ── Overlays drawn over the main screen via SetCursorPosition ─────────
        if (_overlay == OverlayMode.Travel)     DrawTravelOverlay(w, h);
        if (_overlay == OverlayMode.NodeAction) DrawNodeActionOverlay(w, h);
        if (_overlay == OverlayMode.DeckStatus) DrawDeckOverlay(w, h);

        _lastLogCount = _session.SessionLog.Count;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // INPUT
    // ═══════════════════════════════════════════════════════════════════════════

    public IScreen? HandleInput(ConsoleKeyInfo key)
    {
        // If session already ended (e.g. auto-transition was set but key pressed first),
        // use the same helper so we never show a stale screen.
        if (!_session.IsActive)
            return _pendingAutoTransition ?? BuildSessionEndScreen();

        return _overlay switch
        {
            OverlayMode.RunProgram => HandleRunProgramInput(key),
            OverlayMode.Travel     => HandleTravelInput(key),
            OverlayMode.NodeAction => HandleNodeActionInput(key),
            OverlayMode.DeckStatus => HandleDeckInput(key),
            _                      => HandleNormalInput(key)
        };
    }

    // ── Normal ────────────────────────────────────────────────────────────────

    private IScreen? HandleNormalInput(ConsoleKeyInfo key)
    {
        // Keyboard number shortcuts — delegate to shared action handler
        if (key.KeyChar >= '1' && key.KeyChar <= '6')
            return ConfirmMainAction(key.KeyChar - '1');

        // Arrow keys move the action-bar cursor (controller D-pad / keyboard)
        if (key.Key == ConsoleKey.LeftArrow)  { _mainCursor = (_mainCursor + 5) % 6; return null; }
        if (key.Key == ConsoleKey.RightArrow) { _mainCursor = (_mainCursor + 1) % 6; return null; }
        if (key.Key == ConsoleKey.UpArrow)    { _mainCursor = Math.Max(0, _mainCursor - 3); return null; }
        if (key.Key == ConsoleKey.DownArrow)  { _mainCursor = Math.Min(5, _mainCursor + 3); return null; }

        // Enter / controller A confirms highlighted action
        if (key.Key == ConsoleKey.Enter) return ConfirmMainAction(_mainCursor);

        if (key.Key == ConsoleKey.Escape) return DoJackOut();
        return null;
    }

    /// <summary>Executes the main action at <paramref name="index"/> (0-based, matches action bar).</summary>
    private IScreen? ConfirmMainAction(int index)
    {
        _mainCursor = index;   // keep cursor in sync when triggered by keyboard shortcut
        switch (index)
        {
            case 0:
                _programCursor = 0; _overlay = OverlayMode.RunProgram;
                return null;
            case 1:   // Travel — blocked while live ICE is present, UNLESS node was bypassed via Sleaze
                if (!DevSettings.DevMode && HasLiveIce() && !_session.IsNodeBypassed(_session.Persona.CurrentNodeId)) return null;
                _travelCursor = 0;
                _overlay = OverlayMode.Travel;
                return null;
            case 2:   // Node Action — blocked while live ICE is present (bypass does NOT unlock this)
                if (!DevSettings.DevMode && HasLiveIce()) return null;
                ResetNodeAction();
                _nodeActionCursor = 0;
                var na = _session.System.GetNode(_session.Persona.CurrentNodeId);
                if (GetNodeActions(na).Count == 0)
                {
                    _session.LogNote($"({NodeKey(na.Id)}) {na.Type} \"{na.Label}\" \u2014 terminal node, no actions.");
                    return null;
                }
                _overlay = OverlayMode.NodeAction;
                return null;
            case 3: return DoJackOut();
            case 4: return ShowRunInfo();
            case 5:
                _deckPage = DeckPage.Main; _deckCursor = 0; _deckMsg = null;
                _overlay = OverlayMode.DeckStatus;
                return null;
            default: return null;
        }
    }

    // ── RunProgram ────────────────────────────────────────────────────────────

    private IScreen? HandleRunProgramInput(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.Escape || key.Key == ConsoleKey.Backspace) { _overlay = OverlayMode.None; return null; }
        if (key.Key == ConsoleKey.LeftArrow) { _programCursor = Math.Max(0, _programCursor - 1); return null; }
        if (key.Key == ConsoleKey.RightArrow){ _programCursor = Math.Min(4, _programCursor + 1); return null; }
        if (key.Key == ConsoleKey.Enter)
        {
            ExecuteRunProgram(_programCursor);
            _overlay = OverlayMode.None;
            return null;
        }
        if (key.KeyChar >= '1' && key.KeyChar <= '5')
        {
            _programCursor = key.KeyChar - '1';
            ExecuteRunProgram(_programCursor);
            _overlay = OverlayMode.None;
            return null;
        }
        return null;
    }

    private void ExecuteRunProgram(int slot)
    {
        var prog = _session.Decker.Deck.LoadedSlots[slot];
        if (prog is null)       { _pendingError = $"Slot {slot + 1}: empty."; return; }
        if (!prog.IsReadyToRun) { _pendingError = prog.LoadProgress < 1f ? $"{prog.Spec.Name}: loading ({prog.LoadProgress:P0})" : $"{prog.Spec.Name}: cooling down."; return; }

        // Cooldown gate — only active during real-time combat
        if (_session.Persona.CombatState == CombatState.Active && _playerCooldown > 0f)
        {
            _session.LogNote($"NOT READY \u2014 {_playerCooldown:F1}s remaining.");
            _overlay = OverlayMode.None;
            return;
        }

        _session.RunProgram(slot);

        // Reset player cooldown
        if (_session.Persona.CombatState == CombatState.Active)
            _playerCooldown = ComputePlayerCooldown();
    }

    // ── Travel ────────────────────────────────────────────────────────────────

    private IScreen? HandleTravelInput(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.Escape || key.Key == ConsoleKey.Backspace || key.KeyChar == '0') { _overlay = OverlayMode.None; return null; }

        var adj = _session.System.GetNode(_session.Persona.CurrentNodeId).AdjacentNodeIds;

        // Arrow keys move the cursor (controller D-pad / keyboard)
        if (key.Key == ConsoleKey.UpArrow)
            { _travelCursor = Math.Max(0, _travelCursor - 1); return null; }
        if (key.Key == ConsoleKey.DownArrow)
            { _travelCursor = Math.Min(Math.Max(0, adj.Count - 1), _travelCursor + 1); return null; }

        // Resolve chosen index: Enter uses cursor; digit keys also sync cursor
        int choice = -1;
        if (key.Key == ConsoleKey.Enter)
        {
            choice = _travelCursor + 1;
        }
        else if (char.IsDigit(key.KeyChar))
        {
            choice = key.KeyChar - '0';
            if (choice >= 1 && choice <= adj.Count)
                _travelCursor = choice - 1;   // keep cursor in sync with keyboard shortcut
        }

        if (choice >= 1 && choice <= adj.Count)
        {
            _session.TickFrame(2.0f);

            // TickFrame can end the session (e.g. ICE kills decker mid-travel).
            if (!_session.IsActive)
            {
                _overlay = OverlayMode.None;
                return BuildSessionEndScreen();
            }

            var result = _session.TravelToNode(adj[choice - 1]);
            if (!result.Success) _pendingError = $"Cannot travel: {result.ErrorReason}";
            _overlay = OverlayMode.None;
        }
        return null;
    }

    // ── NodeAction ────────────────────────────────────────────────────────────

    private void ResetNodeAction()
    {
        _nodeGoToInput  = false;
        _nodeGoToCursor = 0;
    }

    private IScreen? HandleNodeActionInput(ConsoleKeyInfo key)
    {
        if (_nodeGoToInput)
        {
            // Escape or B/Circle cancels
            if (key.Key == ConsoleKey.Escape || key.Key == ConsoleKey.Backspace)
                { _nodeGoToInput = false; return null; }

            var goToNodes = _session.System.Nodes.Values.OrderBy(n => n.Id).ToList();
            int perRow    = 1; // one node per line — up/down navigates directly

            if (key.Key == ConsoleKey.UpArrow)
                { _nodeGoToCursor = Math.Max(0, _nodeGoToCursor - 1); return null; }
            if (key.Key == ConsoleKey.DownArrow)
                { _nodeGoToCursor = Math.Min(goToNodes.Count - 1, _nodeGoToCursor + 1); return null; }

            if (key.Key == ConsoleKey.Enter && goToNodes.Count > 0)
            {
                string nk  = NodeKey(goToNodes[_nodeGoToCursor].Id);
                string msg = ExecuteGoToNode(nk);
                _session.LogNote(msg);
                _nodeGoToInput = false;
                _overlay = OverlayMode.None;
                return null;
            }

            // Digit keys 1–9: jump to the Nth node in the list (1-based)
            if (key.KeyChar >= '1' && key.KeyChar <= '9')
            {
                int pick = key.KeyChar - '1';   // 0-based
                if (pick < goToNodes.Count)
                {
                    string nk  = NodeKey(goToNodes[pick].Id);
                    string msg = ExecuteGoToNode(nk);
                    _session.LogNote(msg);
                    _nodeGoToInput = false;
                    _overlay = OverlayMode.None;
                }
                return null;
            }
            return null;
        }

        if (key.Key == ConsoleKey.Escape || key.Key == ConsoleKey.Backspace || key.KeyChar == '0') { _overlay = OverlayMode.None; return null; }

        Node node    = _session.System.GetNode(_session.Persona.CurrentNodeId);
        var  actions = GetNodeActions(node);

        // Arrow keys move the cursor (controller D-pad / keyboard)
        if (key.Key == ConsoleKey.UpArrow)
            { _nodeActionCursor = Math.Max(0, _nodeActionCursor - 1); return null; }
        if (key.Key == ConsoleKey.DownArrow)
            { _nodeActionCursor = Math.Min(Math.Max(0, actions.Count - 1), _nodeActionCursor + 1); return null; }

        // Resolve chosen index: Enter uses cursor; digit keys also sync cursor
        int choice = -1;
        if (key.Key == ConsoleKey.Enter)
        {
            choice = _nodeActionCursor + 1;
        }
        else if (char.IsDigit(key.KeyChar))
        {
            choice = key.KeyChar - '0';
            if (choice >= 1 && choice <= actions.Count)
                _nodeActionCursor = choice - 1;   // keep cursor in sync
        }

        if (choice >= 1 && choice <= actions.Count)
        {
            (string? msg, bool ended) = ExecuteNodeAction(choice - 1, node);
            if (msg is not null) _session.LogNote(msg);
            // Keep overlay open when GoToNode sets _nodeGoToInput (text-input sub-mode)
            if (!_nodeGoToInput) _overlay = OverlayMode.None;
            if (ended)
            {
                TryAwardRunReward();
                _gameState.ActiveSession = null;
                return new MatrixSessionEndScreen(_session, _systemNumber, "crashed", _gameState);
            }
            return null;
        }

        return null;
    }

    // Returns (logMessage, sessionEnded)
    private (string? msg, bool ended) ExecuteNodeAction(int index, Node node)
    {
        switch (node.Type)
        {
            case NodeType.CPU: return ExecuteCpuAction(index, node);
            case NodeType.DS:  return ExecuteDsAction(index);
            case NodeType.SM:  return ExecuteSmAction(node);
            default: return (null, false);
        }
    }

    private (string? msg, bool ended) ExecuteCpuAction(int index, Node node)
    {
        switch (index)
        {
            case 0: _nodeGoToInput = true; _nodeGoToCursor = 0; return (null, false);
            case 1:
                var cr = _session.InitiateNodeAction(NodeAction.CancelAlert);
                string crMsg = cr.Success ? "Alert cancelled - system reset to Normal." : $"Cancel alert failed: {cr.ErrorReason}";
                if (cr.Success) _session.LogSuccess(crMsg); else _session.LogFailure(crMsg);
                return (null, false);
            case 2:
                var cs = _session.InitiateNodeAction(NodeAction.CrashSystem);
                string csMsg = cs.Success ? "CPU destroyed - building systems crash offline." : $"Crash failed: {cs.ErrorReason}";
                if (cs.Success) _session.LogSuccess(csMsg); else _session.LogFailure(csMsg);
                return (null, cs.Success);
            default: return (null, false);
        }
    }

    private (string? msg, bool ended) ExecuteDsAction(int index)
    {
        switch (index)
        {
            case 0:
                var xfer = _session.PerformDataTransfer();
                string xferMsg;
                bool xferOk;
                if (!xfer.Success)      { xferMsg = $"Transfer failed: {xfer.ErrorReason}"; xferOk = false; }
                else if (xfer.DownloadedFile is not null)
                {
                    xferMsg = $"Downloaded: \"{xfer.DownloadedFile.Name}\" ({xfer.DownloadedFile.SizeInMp}Mp)";
                    if (xfer.IsObjectiveTransfer) xferMsg += " [OBJECTIVE MET]";
                    xferOk = true;
                }
                else if (xfer.IsObjectiveTransfer) { xferMsg = "Objective transfer complete."; xferOk = true; }
                else                               { xferMsg = "No usable data found in this node."; xferOk = false; }
                if (xferOk) _session.LogSuccess(xferMsg); else _session.LogFailure(xferMsg);
                return (null, false);   // already logged above
            case 1:
                var er = _session.InitiateNodeAction(NodeAction.Erase);
                string erMsg = er.Success ? "Data erased - storage node wiped." : $"Erase failed: {er.ErrorReason}";
                if (er.Success) _session.LogSuccess(erMsg); else _session.LogFailure(erMsg);
                return (null, false);
            default: return (null, false);
        }
    }

    private (string? msg, bool ended) ExecuteSmAction(Node node)
    {
        var sm = _session.InitiateNodeAction(NodeAction.TurnOffNode);
        string smMsg = sm.Success ? $"Slave module \"{node.Label}\" taken offline." : $"Shutdown failed: {sm.ErrorReason}";
        if (sm.Success) _session.LogSuccess(smMsg); else _session.LogFailure(smMsg);
        return (null, false);
    }

    private string ExecuteGoToNode(string keyInput)
    {
        string sysId = _session.System.Id, targetId = $"{sysId}-{keyInput}";
        if (!_session.System.Nodes.ContainsKey(targetId)) return $"Node \"{keyInput}\" not found.";
        var r = _session.InitiateNodeAction(NodeAction.GoToNode, targetId);
        return r.Success ? $"Teleported to node [{keyInput}]." : $"GoToNode failed: {r.ErrorReason}";
    }

    private static List<(string Label, string Desc)> GetNodeActions(Node node) => node.Type switch
    {
        NodeType.CPU => [("GoToNode","Teleport to any node"),("CancelAlert","Reset alert"),("CrashSystem","Destroy CPU")],
        NodeType.DS  => [("TransferData","Download/upload/search"),("Erase","Delete data")],
        NodeType.SM  => [("TurnOff","Take module offline")],
        // IOP (Terminal), SPU, SAN — passthrough nodes, no special actions
        _            => []
    };

    // ── DeckStatus ────────────────────────────────────────────────────────────

    private IScreen? HandleDeckInput(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.Escape) { _overlay = OverlayMode.None; _deckMsg = null; return null; }
        var deck = _session.Decker.Deck;

        switch (_deckPage)
        {
            case DeckPage.Main:
                if (key.Key == ConsoleKey.Backspace) { _overlay = OverlayMode.None; return null; }
                // Arrow keys / D-pad move the cursor over the 3 sub-menu items
                if (key.Key == ConsoleKey.UpArrow)   { _deckMainCursor = Math.Max(0, _deckMainCursor - 1); return null; }
                if (key.Key == ConsoleKey.DownArrow) { _deckMainCursor = Math.Min(2, _deckMainCursor + 1); return null; }
                // Enter confirms cursor; number shortcuts still work and sync cursor
                if (key.Key == ConsoleKey.Enter)
                {
                    if      (_deckMainCursor == 0) { _deckPage = DeckPage.Installed;  _deckCursor = 0; _deckMsg = null; }
                    else if (_deckMainCursor == 1) { _deckPage = DeckPage.Loaded;     _deckCursor = 0; _deckMsg = null; }
                    else                           { _deckPage = DeckPage.Datastore;  _deckCursor = 0; _deckMsg = null; }
                }
                if (key.KeyChar == '2') { _deckMainCursor = 0; _deckPage = DeckPage.Installed;  _deckCursor = 0; _deckMsg = null; }
                if (key.KeyChar == '3') { _deckMainCursor = 1; _deckPage = DeckPage.Loaded;     _deckCursor = 0; _deckMsg = null; }
                if (key.KeyChar == '4') { _deckMainCursor = 2; _deckPage = DeckPage.Datastore;  _deckCursor = 0; _deckMsg = null; }
                break;
            case DeckPage.Installed:
                if (key.Key == ConsoleKey.Backspace) { _deckPage = DeckPage.Main; _deckMsg = null; return null; }
                if (key.Key == ConsoleKey.UpArrow)   { _deckCursor = Math.Max(0, _deckCursor - 1); return null; }
                if (key.Key == ConsoleKey.DownArrow) { _deckCursor = Math.Min(Math.Max(0, deck.Programs.Count - 1), _deckCursor + 1); return null; }
                if (key.Key == ConsoleKey.Enter && deck.Programs.Count > 0)
                    { _deckSelProg = Math.Min(_deckCursor, deck.Programs.Count - 1); _deckPage = DeckPage.InstalledAction; _deckMsg = null; }
                break;
            case DeckPage.InstalledAction:
                if (key.Key == ConsoleKey.Backspace) { _deckPage = DeckPage.Installed; _deckMsg = null; return null; }
                if (_deckSelProg < deck.Programs.Count)
                {
                    var p = deck.Programs[_deckSelProg];
                    // Arrow keys move between Load / Unload
                    if (key.Key == ConsoleKey.UpArrow || key.Key == ConsoleKey.LeftArrow)
                        { _deckActionCursor = 0; return null; }
                    if (key.Key == ConsoleKey.DownArrow || key.Key == ConsoleKey.RightArrow)
                        { _deckActionCursor = 1; return null; }
                    // Enter confirms the highlighted action
                    bool doLoad  = key.KeyChar is 'l' or 'L' || (key.Key == ConsoleKey.Enter && _deckActionCursor == 0);
                    bool doUnload= key.KeyChar is 'u' or 'U' || (key.Key == ConsoleKey.Enter && _deckActionCursor == 1);
                    if (doLoad)
                    {
                        if (p.IsLoaded) _deckMsg = "Already loaded.";
                        else { var r = deck.LoadProgram(p, midSession: true); _deckMsg = r.IsFailure ? r.Error : $"Loading {p.Spec.Name}..."; }
                    }
                    else if (doUnload)
                    {
                        if (!p.IsLoaded) _deckMsg = "Not loaded.";
                        else
                        {
                            int slot = -1;
                            for (int i = 0; i < deck.LoadedSlots.Count; i++) if (ReferenceEquals(deck.LoadedSlots[i], p)) { slot = i; break; }
                            var r = slot < 0 ? Shadowrun.Matrix.Core.Result.Fail("Slot lost.") : deck.UnloadProgram(slot);
                            _deckMsg = r.IsFailure ? r.Error : $"Unloaded {p.Spec.Name}.";
                        }
                    }
                }
                break;
            case DeckPage.Loaded:
                if (key.Key == ConsoleKey.Backspace) { _deckPage = DeckPage.Main; _deckMsg = null; return null; }
                var live = deck.LoadedSlots.Select((p, i) => (p, i)).Where(t => t.p is not null).ToList();
                if (key.Key == ConsoleKey.UpArrow)   { _deckCursor = Math.Max(0, _deckCursor - 1); return null; }
                if (key.Key == ConsoleKey.DownArrow) { _deckCursor = Math.Min(Math.Max(0, live.Count - 1), _deckCursor + 1); return null; }
                if (key.Key == ConsoleKey.Enter && live.Count > 0 && _deckCursor < live.Count)
                {
                    var (p, si) = live[_deckCursor];
                    var r = deck.UnloadProgram(si);
                    _deckMsg = r.IsFailure ? r.Error : $"Unloaded {p!.Spec.Name}.";
                }
                break;
            case DeckPage.Datastore:
                if (key.Key == ConsoleKey.Backspace || key.Key == ConsoleKey.Escape) { _deckPage = DeckPage.Main; _deckMsg = null; return null; }
                if (key.Key == ConsoleKey.UpArrow)   { _deckCursor = Math.Max(0, _deckCursor - 1); return null; }
                if (key.Key == ConsoleKey.DownArrow) { _deckCursor = Math.Min(Math.Max(0, deck.DataFiles.Count - 1), _deckCursor + 1); return null; }
                if (key.Key == ConsoleKey.Enter && deck.DataFiles.Count > 0)
                {
                    _deckSelFile = Math.Min(_deckCursor, deck.DataFiles.Count - 1);
                    _deckPage    = DeckPage.DatastoreDelete;
                    _deckMsg     = null;
                }
                break;
            case DeckPage.DatastoreDelete:
                if (key.Key == ConsoleKey.Backspace) { _deckPage = DeckPage.Datastore; _deckMsg = null; return null; }
                // Arrow keys toggle Yes / No
                if (key.Key == ConsoleKey.UpArrow   || key.Key == ConsoleKey.LeftArrow)  { _deckDeleteCursor = 0; return null; }
                if (key.Key == ConsoleKey.DownArrow || key.Key == ConsoleKey.RightArrow) { _deckDeleteCursor = 1; return null; }
                bool confirmDelete = key.KeyChar is 'y' or 'Y' || (key.Key == ConsoleKey.Enter && _deckDeleteCursor == 0);
                bool cancelDelete  = key.KeyChar is 'n' or 'N' || (key.Key == ConsoleKey.Enter && _deckDeleteCursor == 1);
                if (confirmDelete)
                {
                    if (_deckSelFile < deck.DataFiles.Count)
                    {
                        var file = deck.DataFiles[_deckSelFile];
                        try { deck.RemoveDataFile(file); _deckMsg = $"Deleted \"{file.Name}\"."; }
                        catch (Exception ex) { _deckMsg = $"Delete failed: {ex.Message}"; }
                        _deckPage = DeckPage.Datastore;
                        _deckCursor = Math.Min(_deckCursor, Math.Max(0, deck.DataFiles.Count - 1));
                    }
                    else { _deckPage = DeckPage.Datastore; }
                }
                if (cancelDelete) { _deckPage = DeckPage.Datastore; _deckMsg = null; }
                break;
        }
        return null;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // OVERLAY RENDERERS  (drawn over the main screen via SetCursorPosition)
    // ═══════════════════════════════════════════════════════════════════════════

    // ── Travel ────────────────────────────────────────────────────────────────

    private void DrawTravelOverlay(int w, int h)
    {
        var curr   = _session.System.GetNode(_session.Persona.CurrentNodeId);
        var adjIds = curr.AdjacentNodeIds;

        // Build content lines first so we can measure width
        var lines = new List<(string text, ConsoleColor col)>();
        for (int i = 0; i < adjIds.Count; i++)
        {
            Node d     = _session.System.GetNode(adjIds[i]);
            string key = NodeKey(d.Id);
            string typ = d.Type.ToString()[..Math.Min(3, d.Type.ToString().Length)];
            string sr  = $"SR{d.SecurityRating}";
            string lbl = RenderHelper.Truncate(d.Label, 12);

            string status;
            ConsoleColor fg;
            if (d.IsConquered) { status = "CLEAR"; fg = ConsoleColor.Green; }
            else
            {
                var live = d.GetLiveIce().ToList();
                if (live.Count > 0)
                {
                    var primary = live[0];
                    bool revealed = DevSettings.DevMode || _session.IsIceRevealed(primary);
                    status = revealed
                        ? $"ICE:{primary.Spec.Type.ToString()[..Math.Min(6, primary.Spec.Type.ToString().Length)]} R{primary.EffectiveRating}"
                        : "ICE: detected";
                    fg = ConsoleColor.Red;
                }
                else if (d.IceInstances.Count > 0) { status = "ICE gone"; fg = ConsoleColor.DarkGray; }
                else { status = "No ICE"; fg = ConsoleColor.Gray; }
            }

            string line = $" [{i+1}] {key,-3} {typ,-3} {sr,-4}  {lbl,-12}  {status}";
            lines.Add((line, fg));
        }

        if (adjIds.Count == 0) lines.Add((" No adjacent nodes.", ConsoleColor.Gray));

        string title = " Travel ";
        string footer = $" Select 1-{adjIds.Count}, [0]=cancel ";

        int contentW = lines.Count > 0 ? lines.Max(l => l.text.Length) : 20;
        contentW = Math.Max(contentW, Math.Max(title.Length, footer.Length));
        int ow = contentW + 2;   // +2 for ║ on each side

        string topBorder = "\u2554" + new string('\u2550', contentW) + "\u2557";
        string midBorder = "\u2560" + new string('\u2550', contentW) + "\u2563";
        string botBorder = "\u255a" + new string('\u2550', contentW) + "\u255d";

        int totalRows = 3 + 1 + lines.Count + 1 + 1; // top+title+mid + lines + bot + footer
        int ox = Math.Max(0, (w - ow) / 2);
        int oy = Math.Max(0, (h - totalRows) / 2);

        int row = oy;
        WriteOvLine(ox, row++, topBorder);
        WriteOvCentre(ox, row++, contentW, title, ConsoleColor.White);
        WriteOvLine(ox, row++, midBorder);
        for (int li = 0; li < lines.Count; li++)
        {
            var (text, col) = lines[li];
            if (li == _travelCursor)
                WriteOvContentHighlighted(ox, row++, contentW, text);
            else
                WriteOvContent(ox, row++, contentW, text, col);
        }
        WriteOvLine(ox, row++, botBorder);
        WriteOvCentre(ox, row++, ow, footer, ConsoleColor.DarkGray);
    }

    // ── NodeAction ────────────────────────────────────────────────────────────

    private void DrawNodeActionOverlay(int w, int h)
    {
        Node node  = _session.System.GetNode(_session.Persona.CurrentNodeId);
        string key = NodeKey(node.Id);
        var avail  = GetNodeActions(node);

        if (_nodeGoToInput)
        {
            DrawGoToNodeOverlay(w, h);
            return;
        }

        var lines = new List<(string text, ConsoleColor col)>();
        foreach (var ((lbl, desc), i) in avail.Select((a, i) => (a, i)))
            lines.Add(($" [{i+1}] {lbl,-14} {desc}", ConsoleColor.Gray));
        if (lines.Count == 0)
            lines.Add((" No special actions.", ConsoleColor.DarkGray));

        string title  = $" {node.Type} \"{RenderHelper.Truncate(node.Label, 14)}\" ";
        string footer = avail.Count > 0 ? $" Select 1-{avail.Count}, [0]=cancel " : " [0] cancel ";

        DrawCompactOverlay(title, footer, lines, w, h, _nodeGoToInput ? -1 : _nodeActionCursor);
    }

    private void DrawGoToNodeOverlay(int w, int h)
    {
        var nodes   = _session.System.Nodes.Values.OrderBy(n => n.Id).ToList();
        var lines   = new List<(string text, ConsoleColor col)>();

        for (int i = 0; i < nodes.Count; i++)
        {
            var n  = nodes[i];
            string nk  = NodeKey(n.Id);
            string ty  = n.Type.ToString()[..Math.Min(3, n.Type.ToString().Length)];
            string lbl = RenderHelper.Truncate(n.Label, 18);
            string conquered = n.IsConquered ? " ✓" : "  ";
            string line = $" {nk,-4} {ty,-4}{conquered} {lbl}";
            lines.Add((line, ConsoleColor.Gray));
        }

        if (lines.Count == 0)
            lines.Add((" No nodes found.", ConsoleColor.DarkGray));

        int clamp = Math.Max(0, Math.Min(_nodeGoToCursor, lines.Count - 1));
        _nodeGoToCursor = clamp;

        DrawCompactOverlay(" Go To Node ",
            " [↑↓]=select  [Enter]=go  [B/Esc]=back ", lines, w, h, clamp);
    }

    // ── DeckStatus ────────────────────────────────────────────────────────────

    private void DrawDeckOverlay(int w, int h)
    {
        var deck = _session.Decker.Deck;

        switch (_deckPage)
        {
            case DeckPage.Main:
            {
                var lines = new List<(string, ConsoleColor)>
                {
                    ($" MPCP:{deck.Stats.Mpcp}  Hard:{deck.Stats.Hardening}  Resp:{deck.Stats.Response}", ConsoleColor.Gray),
                    ($" Mem: {deck.UsedMemory()}/{deck.Stats.MemoryMax}Mp  Stor:{deck.UsedStorage()}/{deck.Stats.StorageMax}Mp", ConsoleColor.Gray),
                    ($" LoadIO:{deck.Stats.LoadIoSpeed}  Bod:{deck.Stats.Bod}  Ev:{deck.Stats.Evasion}  Mas:{deck.Stats.Masking}  Sen:{deck.Stats.Sensor}", ConsoleColor.Gray),
                    ($" -------", ConsoleColor.DarkGray),
                    ($" [2] Installed programs", ConsoleColor.White),
                    ($" [3] Loaded slots",       ConsoleColor.White),
                    ($" [4] Datastore ({deck.DataFiles.Count} files)", ConsoleColor.White),
                };
                // Lines 4-6 are the selectable items; cursor offset = _deckMainCursor + 4
                DrawCompactOverlay($" {RenderHelper.Truncate(deck.Name, 18)} ",
                    " [↑↓/Enter]=open  [Esc]=close ", lines, w, h, _deckMainCursor + 4);
                break;
            }
            case DeckPage.Installed:
            {
                var lines = new List<(string, ConsoleColor)>();
                for (int i = 0; i < deck.Programs.Count; i++)
                {
                    var p   = deck.Programs[i];
                    bool sel = i == _deckCursor;
                    string st = p.IsLoaded ? $"L{p.LoadProgress:P0}" : "---";
                    string ln = $" {(sel ? ">" : " ")}{p.Spec.Name,-12} Lv{p.Spec.Level} {p.Spec.SizeInMp}Mp {st}";
                    lines.Add((ln, sel ? ConsoleColor.Yellow : ConsoleColor.Gray));
                }
                if (lines.Count == 0) lines.Add((" No programs installed.", ConsoleColor.DarkGray));
                DrawCompactOverlay(" Installed Programs ", " [\u2191\u2193] select  [Enter] open  [Bksp] back ", lines, w, h);
                break;
            }
            case DeckPage.InstalledAction:
            {
                if (_deckSelProg < deck.Programs.Count)
                {
                    var p = deck.Programs[_deckSelProg];
                    string st = p.IsLoaded ? $"Loaded ({p.LoadProgress:P0})" : "Not loaded";
                    var lines = new List<(string, ConsoleColor)>
                    {
                        ($" {p.Spec.Name} Lv{p.Spec.Level}  {p.Spec.SizeInMp}Mp", ConsoleColor.White),
                        ($" Status: {st}", ConsoleColor.Gray),
                        ($" Mem free: {deck.FreeMemory()}Mp  Slots: {deck.FreeSlotCount()} free", ConsoleColor.Gray),
                        ($" -------", ConsoleColor.DarkGray),
                        ($" [L] Load program into memory", ConsoleColor.White),
                        ($" [U] Unload program from slot", ConsoleColor.White),
                    };
                    if (_deckMsg is not null) lines.Add(($" {_deckMsg}", ConsoleColor.Yellow));
                    // Lines 4-5 are the selectable Load/Unload items; cursor offset = _deckActionCursor + 4
                    DrawCompactOverlay(" Program Action ",
                        " [↑↓/Enter]=select  [Bksp]=back ", lines, w, h, _deckActionCursor + 4);
                }
                break;
            }
            case DeckPage.Loaded:
            {
                var live = deck.LoadedSlots.Select((p, i) => (p, i)).Where(t => t.p is not null).ToList();
                Ice? ai  = _session.System.GetNode(_session.Persona.CurrentNodeId).GetActiveIce();
                var lines = new List<(string, ConsoleColor)>();
                for (int i = 0; i < live.Count; i++)
                {
                    var (p, si) = live[i];
                    bool sel    = i == _deckCursor;
                    string rdy  = p!.LoadProgress < 1f ? $"{p.LoadProgress:P0}" : "Ready";
                    string ch   = (ai is not null && p.IsReadyToRun) ? $" {_session.Persona.ComputeSuccessChance(p, ai):P0}" : "";
                    string ln   = $" {(sel ? ">" : " ")}[{si+1}] {p.Spec.Name,-10} L{p.Spec.Level} {rdy,-8}{ch}";
                    lines.Add((ln, sel ? ConsoleColor.Yellow : ConsoleColor.Gray));
                }
                if (lines.Count == 0) lines.Add((" No programs loaded.", ConsoleColor.DarkGray));
                if (_deckMsg is not null) lines.Add(($" {_deckMsg}", ConsoleColor.Yellow));
                DrawCompactOverlay(" Loaded Slots ", " [\u2191\u2193] select  [Enter]=unload  [Bksp]=back ", lines, w, h);
                break;
            }
            case DeckPage.Datastore:
            {
                var files = deck.DataFiles;
                var lines = new List<(string, ConsoleColor)>();
                for (int i = 0; i < files.Count; i++)
                {
                    var f   = files[i];
                    bool sel = i == _deckCursor;
                    string plot = f.IsPlotRelevant ? " \u26a0" : "";
                    string ln   = $" {(sel ? ">" : " ")}{f.Name}{plot}  {f.NuyenValue}\u00a5  ({f.SizeInMp}Mp)";
                    lines.Add((ln, sel ? ConsoleColor.Yellow : (f.IsPlotRelevant ? ConsoleColor.DarkYellow : ConsoleColor.Gray)));
                }
                if (lines.Count == 0) lines.Add((" Datastore empty.", ConsoleColor.DarkGray));
                if (_deckMsg is not null) lines.Add(($" {_deckMsg}", ConsoleColor.Cyan));
                DrawCompactOverlay(" Datastore ", " [\u2191\u2193] browse  [Enter]=delete  [Bksp]=back ", lines, w, h);
                break;
            }
            case DeckPage.DatastoreDelete:
            {
                var lines = new List<(string, ConsoleColor)>();
                if (_deckSelFile < deck.DataFiles.Count)
                {
                    var f = deck.DataFiles[_deckSelFile];
                    lines.Add(($" File:  {f.Name}", ConsoleColor.White));
                    lines.Add(($" Value: {f.NuyenValue}\u00a5  Size: {f.SizeInMp}Mp", ConsoleColor.Gray));
                    if (f.IsPlotRelevant) lines.Add((" \u26a0  Plot-relevant — delete anyway?", ConsoleColor.Yellow));
                    lines.Add(("", ConsoleColor.Gray));
                }
                lines.Add(($" [Y] Yes — delete permanently", ConsoleColor.Red));
                lines.Add(($" [N] No  — cancel",             ConsoleColor.White));
                // Yes is last-2, No is last-1; cursor 0=Yes, 1=No → index = lines.Count - 2 + _deckDeleteCursor
                DrawCompactOverlay(" Confirm Delete ",
                    " [↑↓/Enter]=select  [Bksp]=cancel ", lines, w, h,
                    lines.Count - 2 + _deckDeleteCursor);
                break;
            }
        }
    }

    // ── Generic compact overlay helper ────────────────────────────────────────

    private static void DrawCompactOverlay(
        string title,
        string footer,
        List<(string text, ConsoleColor col)> lines,
        int w, int h,
        int highlightLine = -1)
    {
        int contentW = lines.Count > 0 ? lines.Max(l => l.text.Length) : 4;
        contentW = Math.Max(contentW, Math.Max(title.Length, footer.Length));
        int ow = contentW + 2;

        string topBorder = "\u2554" + new string('\u2550', contentW) + "\u2557";
        string midBorder = "\u2560" + new string('\u2550', contentW) + "\u2563";
        string botBorder = "\u255a" + new string('\u2550', contentW) + "\u255d";

        int totalRows = 3 + lines.Count + 1 + 1; // top+title+mid + lines + bot + footer
        int ox = Math.Max(0, (w - ow) / 2);
        int oy = Math.Max(0, (h - totalRows) / 2);

        int row = oy;
        WriteOvLine(ox, row++, topBorder);
        WriteOvCentre(ox, row++, contentW, title, ConsoleColor.White);
        WriteOvLine(ox, row++, midBorder);
        for (int li = 0; li < lines.Count; li++)
        {
            var (text, col) = lines[li];
            if (li == highlightLine)
                WriteOvContentHighlighted(ox, row++, contentW, text);
            else
                WriteOvContent(ox, row++, contentW, text, col);
        }
        WriteOvLine(ox, row++, botBorder);
        WriteOvCentre(ox, row++, ow, footer, ConsoleColor.DarkGray);

        // Reset cursor below overlay so it doesn't blink in weird spot
        try { VC.SetCursorPosition(0, Math.Min(h - 1, oy + totalRows + 1)); } catch { }
    }

    // ── Low-level overlay drawing helpers ─────────────────────────────────────

    private static void WriteOvLine(int x, int y, string text)
    {
        try
        {
            VC.SetCursorPosition(x, y);
            VC.Write(text);
        }
        catch { /* ignore out-of-bounds */ }
    }

    private static void WriteOvCentre(int x, int y, int innerW, string text, ConsoleColor fg)
    {
        try
        {
            VC.SetCursorPosition(x, y);
            VC.ForegroundColor = fg;
            // For top/bot border rows the full ow is passed; for title we write ║ manually
            string content = text.Length >= innerW ? text[..innerW] : text.PadRight(innerW);
            int pad = innerW - text.Length;
            int lp  = pad > 0 ? pad / 2 : 0;
            int rp  = pad > 0 ? pad - lp : 0;
            VC.Write("\u2551" + new string(' ', lp) + text + new string(' ', rp) + "\u2551");
            VC.ResetColor();
        }
        catch { }
    }

    private static void WriteOvContent(int x, int y, int contentW, string text, ConsoleColor fg)
    {
        try
        {
            VC.SetCursorPosition(x, y);
            VC.Write("\u2551");
            VC.ForegroundColor = fg;
            string padded = text.Length >= contentW ? text[..contentW] : text.PadRight(contentW);
            VC.Write(padded);
            VC.ResetColor();
            VC.Write("\u2551");
        }
        catch { }
    }

    /// <summary>Like <see cref="WriteOvContent"/> but with a yellow highlight for the cursor row.</summary>
    private static void WriteOvContentHighlighted(int x, int y, int contentW, string text)
    {
        try
        {
            VC.SetCursorPosition(x, y);
            VC.Write("\u2551");
            VC.BackgroundColor = ConsoleColor.DarkYellow;
            VC.ForegroundColor = ConsoleColor.Black;
            string padded = text.Length >= contentW ? text[..contentW] : text.PadRight(contentW);
            VC.Write(padded);
            VC.ResetColor();
            VC.Write("\u2551");
        }
        catch { }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Returns true when the current node has at least one live ICE instance.</summary>
    private bool HasLiveIce() =>
        _session.System.GetNode(_session.Persona.CurrentNodeId).GetLiveIce().Any();

    private static void RenderHeader(MatrixSystem system, Node current, string nodeKey, int w)
    {
        int inner = w - 2;
        RenderHelper.DrawWindowOpen($"MATRIX \u2014 {system.CorporationName ?? system.Name} [{system.Difficulty.ToUpper()}]", w);
        string alertLabel = AlertLabel(system.AlertState), nodeLabel = $"({nodeKey}) {current.Type}";
        const string ap = "  ALERT: ", np = "   NODE: "; const int aw = 12;
        int fixedLen = ap.Length + aw + np.Length;
        int avail    = inner - fixedLen;
        if (nodeLabel.Length > avail) nodeLabel = nodeLabel[..Math.Max(0, avail)];
        int pad = inner - fixedLen - nodeLabel.Length;
        VC.Write("\u2551"); VC.Write(ap);
        VC.ForegroundColor = AlertColor(system.AlertState); VC.Write($"{alertLabel,-12}"); VC.ResetColor();
        VC.Write(np); VC.ForegroundColor = ConsoleColor.Cyan; VC.Write(nodeLabel); VC.ResetColor();
        if (pad > 0) VC.Write(new string(' ', pad));
        VC.WriteLine("\u2551");
    }

    private void RenderSplitPane(MatrixSystem system, Persona persona, Node current, int w)
    {
        int inner = w - 2;
        bool wide = w >= 150;
        int infoW = wide ? inner * 25 / 100 : inner * 42 / 100;
        int mapW  = inner - infoW - 1;   // -1 for divider

        // Fixed rows outside the split pane:
        //   DrawWindowOpen(3) + header body(1) + 4×dividers(4) + RUN LOG(7) + Deck(2) + Actions(2) + Close(1) = 20
        int fixedRows = 20;
        int rows = Math.Max(4, _lastH - fixedRows);

        var layout   = GetOrComputeLayout(system);
        var nodeInfo = BuildNodeInfoLines(current, system, persona, infoW);

        // Build character/colour buffer for entire map grid
        var visibleNodes = DevSettings.DevMode ? null : _session.VisitedNodes;
        var (ch, co, bW, bH) = BuildMapBuffer(system, persona, layout, visibleNodes);

        // Centre viewport on current node
        int minGx = layout.Count > 0 ? layout.Values.Min(p => p.gx) : 0;
        int minGy = layout.Count > 0 ? layout.Values.Min(p => p.gy) : 0;
        layout.TryGetValue(persona.CurrentNodeId, out var cPos);
        int centerBx = (cPos.gx - minGx) * GW + 3;
        int centerBy = (cPos.gy - minGy) * GH + 1;
        int startX   = Math.Max(0, Math.Min(centerBx - mapW / 2,  Math.Max(0, bW - mapW)));
        int startY   = Math.Max(0, Math.Min(centerBy - rows / 2,  Math.Max(0, bH - rows)));

        for (int row = 0; row < rows; row++)
        {
            VC.Write("\u2551");

            // Map area — display-column-aware rendering.
            // Wide node symbols (⛁△□○◇) occupy 2 terminal columns; we must
            // track display columns (dispCols) separately from buffer index (bufX)
            // so the map region always fills exactly mapW display columns.
            int by = startY + row;
            ConsoleColor lastCol = ConsoleColor.DarkGray;
            VC.ForegroundColor = lastCol;
            int dispCols = 0;
            int bufX     = 0;
            while (dispCols < mapW)
            {
                int bx = startX + bufX++;
                char  mc;
                ConsoleColor nc;
                if (bx >= 0 && bx < bW && by >= 0 && by < bH)
                { mc = ch[by, bx]; nc = co[by, bx]; }
                else
                { mc = ' '; nc = ConsoleColor.DarkGray; }

                int dw = RenderHelper.VisualCharWidth(mc);
                if (dispCols + dw > mapW)
                {
                    // Wide char won't fit in remaining columns — pad with spaces.
                    VC.ForegroundColor = ConsoleColor.DarkGray;
                    while (dispCols < mapW) { VC.Write(' '); dispCols++; }
                    break;
                }
                if (nc != lastCol) { VC.ForegroundColor = nc; lastCol = nc; }
                VC.Write(mc);
                dispCols += dw;
            }
            VC.ResetColor();

            // Divider
            VC.Write(wide ? "\u2502" : "\u2502");

            // Node info area — use visual-width helpers so symbols like ⚔ (2-wide)
            // don't push the closing ║ off position.
            if (row < nodeInfo.Count)
            {
                VC.ForegroundColor = nodeInfo[row].Color;
                string rt = RenderHelper.VisualPadRight(
                                RenderHelper.VisualTruncate(nodeInfo[row].Text, infoW), infoW);
                VC.Write(rt);
                VC.ResetColor();
            }
            else VC.Write(new string(' ', infoW));

            VC.WriteLine("\u2551");
        }
    }

    // ── Map grid layout (BFS, assign 2D grid coords) ─────────────────────────

    private Dictionary<string, (int gx, int gy)> GetOrComputeLayout(MatrixSystem sys)
    {
        if (_mapLayout is not null && _mapLayoutSysId == sys.Id) return _mapLayout;
        _mapLayout      = TryBuildHardcodedLayout(sys) ?? ComputeMapLayout(sys);
        _mapLayoutSysId = sys.Id;
        return _mapLayout;
    }

    /// <summary>
    /// Builds a layout from the hardcoded per-system grid coordinates extracted from the
    /// original Shadowrun Genesis walkthrough maps.  Returns null for procedural systems
    /// (number >= 25) that have no hardcoded data.
    /// </summary>
    private static Dictionary<string, (int gx, int gy)>? TryBuildHardcodedLayout(MatrixSystem sys)
    {
        if (!int.TryParse(sys.Id, out int sysNum)) return null;
        if (!_hardcodedLayouts.TryGetValue(sysNum, out var keyMap)) return null;

        var layout = new Dictionary<string, (int gx, int gy)>();
        foreach (var (key, pos) in keyMap)
        {
            string nodeId = $"{sys.Id}-{key}";
            if (sys.Nodes.ContainsKey(nodeId))
                layout[nodeId] = pos;
        }
        // Fall back to BFS if we ended up with too few nodes (should not happen for valid data)
        return layout.Count >= sys.Nodes.Count ? layout : null;
    }

    // ── Hardcoded map layouts (grid positions extracted from the Genesis walkthrough maps) ─
    // Each entry maps node key → (gx, gy). Positions match the ASCII-art schemas exactly.
    private static readonly Dictionary<int, Dictionary<string, (int gx, int gy)>>
        _hardcodedLayouts = new()
        {
            [0]  = new() { ["1"]=(7, 3), ["2"]=(9, 3), ["3"]=(6, 4), ["4"]=(8, 4), ["5"]=(10, 4), ["6"]=(6, 5), ["7"]=(8, 6) },
            [1]  = new() { ["1"]=(6, 3), ["2"]=(9, 4), ["3"]=(6, 5), ["4"]=(8, 5), ["5"]=(7, 6), ["6"]=(9, 6) },
            [2]  = new() { ["1"]=(4, 2), ["2"]=(7, 2), ["3"]=(9, 2), ["4"]=(6, 4), ["5"]=(8, 4), ["6"]=(10, 4), ["7"]=(12, 4), ["8"]=(9, 5), ["9"]=(8, 6) },
            [3]  = new() { ["1"]=(5, 3), ["2"]=(7, 3), ["3"]=(6, 4), ["4"]=(8, 4), ["5"]=(11, 4), ["6"]=(9, 5), ["7"]=(6, 6), ["8"]=(8, 6) },
            [4]  = new() { ["1"]=(7, 2), ["2"]=(7, 4), ["3"]=(9, 4), ["4"]=(6, 5), ["5"]=(10, 5), ["6"]=(8, 6), ["7"]=(7, 7) },
            [5]  = new() { ["1"]=(6, 0), ["2"]=(4, 2), ["3"]=(6, 2), ["4"]=(8, 2), ["5"]=(10, 3), ["6"]=(6, 4), ["7"]=(8, 4), ["8"]=(11, 5), ["9"]=(5, 6), ["A"]=(7, 6), ["B"]=(10, 6), ["C"]=(12, 6), ["D"]=(7, 8) },
            [6]  = new() { ["1"]=(5, 1), ["2"]=(10, 1), ["3"]=(4, 2), ["4"]=(6, 2), ["5"]=(10, 3), ["6"]=(12, 3), ["7"]=(7, 4), ["8"]=(9, 4), ["9"]=(8, 5), ["A"]=(8, 7) },
            [7]  = new() { ["1"]=(2, 0), ["2"]=(4, 0), ["3"]=(6, 0), ["4"]=(8, 0), ["5"]=(5, 1), ["6"]=(7, 1), ["7"]=(13, 1), ["8"]=(6, 2), ["9"]=(10, 2), ["A"]=(14, 2), ["B"]=(1, 3), ["C"]=(4, 3), ["D"]=(9, 3), ["E"]=(13, 3), ["F"]=(2, 4), ["G"]=(7, 4), ["H"]=(1, 5), ["I"]=(8, 5), ["J"]=(12, 5), ["K"]=(4, 6), ["L"]=(9, 6), ["M"]=(3, 7), ["N"]=(7, 7), ["O"]=(4, 8), ["P"]=(6, 8), ["Q"]=(8, 8), ["R"]=(10, 8), ["S"]=(7, 9), ["T"]=(9, 9) },
            [8]  = new() { ["1"]=(8, 1), ["2"]=(7, 2), ["3"]=(9, 2), ["4"]=(8, 3), ["5"]=(5, 4), ["6"]=(6, 5), ["7"]=(10, 6), ["8"]=(3, 7), ["9"]=(5, 7), ["A"]=(9, 7), ["B"]=(11, 7), ["C"]=(4, 8), ["D"]=(8, 8) },
            [9]  = new() { ["1"]=(7, 1), ["2"]=(9, 3), ["3"]=(6, 4), ["4"]=(7, 5), ["5"]=(9, 5), ["6"]=(7, 7) },
            [10] = new() { ["1"]=(8, 1), ["2"]=(10, 2), ["3"]=(8, 3), ["4"]=(9, 4), ["5"]=(5, 5), ["6"]=(6, 6), ["7"]=(9, 6), ["8"]=(5, 7), ["9"]=(10, 7), ["A"]=(9, 9), ["B"]=(5, 9) },
            [11] = new() { ["1"]=(7, 2), ["2"]=(5, 3), ["3"]=(7, 4), ["4"]=(9, 4), ["5"]=(6, 5), ["6"]=(10, 5), ["7"]=(7, 7), ["8"]=(10, 7) },
            [12] = new() { ["1"]=(6, 1), ["2"]=(9, 2), ["3"]=(3, 3), ["4"]=(5, 3), ["5"]=(12, 3), ["6"]=(9, 4), ["7"]=(3, 5), ["8"]=(11, 5), ["9"]=(13, 5), ["A"]=(9, 6), ["B"]=(4, 7), ["C"]=(6, 7), ["D"]=(8, 7), ["E"]=(11, 7) },
            [13] = new() { ["1"]=(4, 1), ["2"]=(8, 1), ["3"]=(6, 2), ["4"]=(11, 2), ["5"]=(4, 3), ["6"]=(10, 3), ["7"]=(6, 4), ["8"]=(8, 4), ["9"]=(12, 4), ["A"]=(10, 5), ["B"]=(10, 7) },
            [14] = new() { ["1"]=(1, 0), ["2"]=(4, 0), ["3"]=(6, 0), ["4"]=(2, 1), ["5"]=(7, 1), ["6"]=(6, 2), ["7"]=(8, 2), ["8"]=(10, 2), ["9"]=(7, 3), ["A"]=(9, 4), ["B"]=(11, 4), ["C"]=(13, 4), ["D"]=(3, 5), ["E"]=(7, 5), ["F"]=(9, 5), ["G"]=(10, 6), ["H"]=(11, 7), ["I"]=(14, 7), ["J"]=(10, 8), ["K"]=(13, 8), ["L"]=(12, 9), ["M"]=(14, 9) },
            [15] = new() { ["1"]=(6, 1), ["2"]=(8, 1), ["3"]=(10, 1), ["4"]=(11, 2), ["5"]=(4, 3), ["6"]=(6, 3), ["7"]=(9, 3), ["8"]=(10, 5), ["9"]=(13, 5), ["A"]=(7, 6), ["B"]=(9, 6), ["C"]=(3, 7), ["D"]=(5, 7), ["E"]=(11, 7), ["F"]=(13, 7), ["G"]=(5, 9), ["H"]=(11, 9) },
            [16] = new() { ["1"]=(8, 0), ["2"]=(2, 1), ["3"]=(5, 1), ["4"]=(7, 1), ["5"]=(10, 2), ["6"]=(2, 3), ["7"]=(4, 3), ["8"]=(9, 3), ["9"]=(14, 3), ["A"]=(1, 4), ["B"]=(10, 4), ["C"]=(13, 4), ["D"]=(2, 5), ["E"]=(5, 5), ["F"]=(7, 5), ["G"]=(3, 6), ["H"]=(6, 6), ["I"]=(10, 6), ["J"]=(12, 6), ["K"]=(2, 7), ["L"]=(14, 7), ["M"]=(1, 8), ["N"]=(5, 8), ["O"]=(7, 8), ["P"]=(11, 8), ["Q"]=(13, 8), ["R"]=(3, 9), ["S"]=(9, 9), ["T"]=(10, 9), ["U"]=(12, 9) },
            [17] = new() { ["1"]=(1, 1), ["2"]=(3, 2), ["3"]=(4, 2), ["4"]=(6, 2), ["5"]=(2, 3), ["6"]=(7, 3), ["7"]=(9, 3), ["8"]=(12, 3), ["9"]=(10, 4), ["A"]=(1, 5), ["B"]=(6, 5), ["C"]=(8, 5), ["D"]=(14, 5), ["E"]=(1, 6), ["F"]=(3, 6), ["G"]=(5, 6), ["H"]=(9, 6), ["I"]=(4, 8), ["J"]=(6, 8) },
            [18] = new() { ["1"]=(8, 2), ["2"]=(12, 3), ["3"]=(3, 4), ["4"]=(7, 4), ["5"]=(11, 4), ["6"]=(13, 5), ["7"]=(5, 6), ["8"]=(8, 6), ["9"]=(11, 6) },
            [19] = new() { ["1"]=(4, 1), ["2"]=(11, 1), ["3"]=(10, 2), ["4"]=(2, 3), ["5"]=(4, 3), ["6"]=(8, 3), ["7"]=(11, 3), ["8"]=(8, 5), ["9"]=(11, 5), ["A"]=(13, 5), ["B"]=(4, 6), ["C"]=(6, 6), ["D"]=(3, 7), ["E"]=(11, 7), ["F"]=(4, 8), ["G"]=(12, 8) },
            [20] = new() { ["1"]=(1, 0), ["2"]=(5, 0), ["3"]=(12, 0), ["4"]=(2, 1), ["5"]=(4, 1), ["6"]=(7, 1), ["7"]=(1, 2), ["8"]=(3, 2), ["9"]=(12, 2), ["A"]=(14, 2), ["B"]=(2, 3), ["C"]=(5, 3), ["D"]=(9, 3), ["E"]=(4, 4), ["F"]=(8, 4), ["G"]=(10, 4), ["H"]=(12, 4), ["I"]=(5, 5), ["J"]=(7, 5), ["K"]=(6, 7), ["L"]=(7, 7), ["M"]=(9, 7), ["N"]=(11, 7), ["O"]=(7, 8), ["P"]=(11, 8), ["Q"]=(9, 9) },
            [21] = new() { ["1"]=(5, 0), ["2"]=(14, 0), ["3"]=(3, 1), ["4"]=(6, 1), ["5"]=(8, 1), ["6"]=(12, 1), ["7"]=(14, 1), ["8"]=(7, 2), ["9"]=(11, 2), ["A"]=(4, 3), ["B"]=(5, 4), ["C"]=(9, 4), ["D"]=(2, 5), ["E"]=(4, 5), ["F"]=(7, 5), ["G"]=(12, 5), ["H"]=(1, 6), ["I"]=(5, 6), ["J"]=(8, 6), ["K"]=(11, 6), ["L"]=(14, 6), ["M"]=(2, 7), ["N"]=(13, 7), ["O"]=(7, 8), ["P"]=(9, 8), ["Q"]=(11, 8), ["R"]=(6, 9), ["S"]=(8, 9), ["T"]=(13, 9) },
            [22] = new() { ["1"]=(4, 0), ["2"]=(2, 1), ["3"]=(11, 1), ["4"]=(13, 1), ["5"]=(14, 1), ["6"]=(4, 2), ["7"]=(7, 2), ["8"]=(9, 2), ["9"]=(12, 3), ["A"]=(10, 4), ["B"]=(13, 4), ["C"]=(7, 5), ["D"]=(12, 5), ["E"]=(2, 6), ["F"]=(5, 6), ["G"]=(8, 6), ["H"]=(10, 6), ["I"]=(14, 6), ["J"]=(1, 7), ["K"]=(13, 7), ["L"]=(6, 8), ["M"]=(8, 8), ["N"]=(12, 8), ["O"]=(4, 9), ["P"]=(13, 9) },
            [23] = new() { ["1"]=(1, 0), ["2"]=(6, 0), ["3"]=(13, 0), ["4"]=(2, 1), ["5"]=(3, 1), ["6"]=(5, 1), ["7"]=(14, 1), ["8"]=(12, 2), ["9"]=(3, 3), ["A"]=(4, 3), ["B"]=(7, 3), ["C"]=(9, 3), ["D"]=(13, 3), ["E"]=(2, 4), ["F"]=(14, 4), ["G"]=(3, 5), ["H"]=(11, 5), ["I"]=(1, 6), ["J"]=(8, 6), ["K"]=(10, 6), ["L"]=(12, 6), ["M"]=(13, 6), ["N"]=(2, 7), ["O"]=(5, 7), ["P"]=(7, 7), ["Q"]=(14, 7), ["R"]=(3, 8), ["S"]=(10, 8), ["T"]=(4, 9), ["U"]=(6, 9), ["V"]=(11, 9) },
            [24] = new() { ["1"]=(2, 0), ["2"]=(7, 0), ["3"]=(13, 0), ["4"]=(3, 1), ["5"]=(7, 1), ["6"]=(12, 1), ["7"]=(14, 1), ["8"]=(2, 2), ["9"]=(4, 2), ["A"]=(8, 2), ["B"]=(14, 2), ["C"]=(6, 3), ["D"]=(7, 4), ["E"]=(12, 4), ["F"]=(14, 4), ["G"]=(2, 5), ["H"]=(11, 5), ["I"]=(14, 5), ["J"]=(3, 6), ["K"]=(5, 6), ["L"]=(6, 7), ["M"]=(13, 7), ["N"]=(2, 8), ["O"]=(7, 8), ["P"]=(12, 8), ["Q"]=(14, 8), ["R"]=(3, 9), ["S"]=(5, 9), ["T"]=(6, 9), ["U"]=(8, 9) },
        };

    private static Dictionary<string, (int gx, int gy)> ComputeMapLayout(MatrixSystem sys)
    {
        var layout   = new Dictionary<string, (int, int)>();
        var occupied = new HashSet<(int, int)>();
        var queue    = new Queue<string>();

        layout[sys.SanNodeId] = (0, 0);
        occupied.Add((0, 0));
        queue.Enqueue(sys.SanNodeId);

        // Prefer: right, down, left, up
        (int dx, int dy)[] dirs = [(1, 0), (0, 1), (-1, 0), (0, -1)];

        while (queue.Count > 0)
        {
            string cur   = queue.Dequeue();
            var (px, py) = layout[cur];
            foreach (string adj in sys.GetNode(cur).AdjacentNodeIds.OrderBy(x => x))
            {
                if (layout.ContainsKey(adj)) continue;
                bool placed = false;
                foreach (var (dx, dy) in dirs)
                {
                    var candidate = (px + dx, py + dy);
                    if (occupied.Contains(candidate)) continue;
                    layout[adj] = candidate;
                    occupied.Add(candidate);
                    queue.Enqueue(adj);
                    placed = true;
                    break;
                }
                // Spiral search if all neighbours taken
                for (int r = 2; r <= 8 && !placed; r++)
                for (int gx = px - r; gx <= px + r && !placed; gx++)
                for (int gy = py - r; gy <= py + r && !placed; gy++)
                {
                    if (Math.Abs(gx - px) + Math.Abs(gy - py) != r) continue;
                    if (occupied.Contains((gx, gy))) continue;
                    layout[adj] = (gx, gy);
                    occupied.Add((gx, gy));
                    queue.Enqueue(adj);
                    placed = true;
                }
            }
        }
        return layout;
    }

    // Grid constants: 6 cols wide, 2 rows tall per grid cell
    // Adjacent horizontal nodes get 4 dashes between them; vertical get 1 pipe row.
    private const int GW = 6;   // grid cell width  (chars)
    private const int GH = 2;   // grid cell height (rows)

    // Each grid cell is GW chars wide × GH rows tall.
    // Buffer origins:  BX(gx) = (gx-minGx)*GW + 3,  BY(gy) = (gy-minGy)*GH + 1
    private static (char[,] ch, ConsoleColor[,] co, int bW, int bH)
        BuildMapBuffer(MatrixSystem sys, Persona persona,
                       Dictionary<string, (int gx, int gy)> layout,
                       IReadOnlySet<string>? visibleNodes = null)
    {
        if (layout.Count == 0)
            return (new char[4, 12], new ConsoleColor[4, 12], 12, 4);

        int minGx = layout.Values.Min(p => p.gx);
        int minGy = layout.Values.Min(p => p.gy);
        int maxGx = layout.Values.Max(p => p.gx);
        int maxGy = layout.Values.Max(p => p.gy);

        int bW = (maxGx - minGx + 1) * GW + 8;
        int bH = (maxGy - minGy + 1) * GH + 4;

        var ch = new char[bH, bW];
        var co = new ConsoleColor[bH, bW];
        for (int y = 0; y < bH; y++)
        for (int x = 0; x < bW; x++) { ch[y, x] = ' '; co[y, x] = ConsoleColor.DarkGray; }

        void Put(int bx, int by, char c, ConsoleColor col)
        {
            if (bx >= 0 && bx < bW && by >= 0 && by < bH)
            { ch[by, bx] = c; co[by, bx] = col; }
        }
        char Peek(int bx, int by) =>
            (bx >= 0 && bx < bW && by >= 0 && by < bH) ? ch[by, bx] : ' ';

        // Convert grid coord to buffer coord (node centre)
        int BX(int gx) => (gx - minGx) * GW + 3;
        int BY(int gy) => (gy - minGy) * GH + 1;

        // Write a horizontal run of dashes, replacing existing '|' with '+'
        void DrawH(int x1, int x2, int by, ConsoleColor col)
        {
            for (int ex = x1; ex <= x2; ex++)
            {
                char prev = Peek(ex, by);
                Put(ex, by, prev == '|' ? '+' : '-', col);
            }
        }
        // Write a vertical run of pipes, replacing existing '-' or '+' with '+'
        void DrawV(int bx, int y1, int y2, ConsoleColor col)
        {
            for (int ey = y1; ey <= y2; ey++)
            {
                char prev = Peek(bx, ey);
                Put(bx, ey, (prev == '-' || prev == '+') ? '+' : '|', col);
            }
        }
        // Place a junction/corner (always +)
        void Corner(int bx, int by, ConsoleColor col) => Put(bx, by, '+', col);

        // ── Edges first (nodes will overdraw) ────────────────────────────────
        var drawn = new HashSet<(string, string)>();
        foreach (var (id, (gx, gy)) in layout)
        {
            foreach (string adjId in sys.GetNode(id).AdjacentNodeIds)
            {
                if (!layout.TryGetValue(adjId, out var aPos)) continue;
                string k1 = string.Compare(id, adjId, StringComparison.Ordinal) <= 0 ? id : adjId;
                string k2 = k1 == id ? adjId : id;
                if (!drawn.Add((k1, k2))) continue;

                // Fog of war: only draw edges where both endpoints are visible
                if (visibleNodes is not null &&
                    !visibleNodes.Contains(id) && !visibleNodes.Contains(adjId)) continue;

                int ax = aPos.gx, ay = aPos.gy;
                int bx1 = BX(gx),  by1 = BY(gy);
                int bx2 = BX(ax),  by2 = BY(ay);
                const ConsoleColor EC = ConsoleColor.DarkGray;

                if (ay == gy) // ── purely horizontal ──────────────────────────
                {
                    int lx = Math.Min(bx1, bx2) + 1;
                    int rx = Math.Max(bx1, bx2) - 1;
                    if (lx <= rx) DrawH(lx, rx, by1, EC);
                }
                else if (ax == gx) // ── purely vertical ───────────────────────
                {
                    int ty = Math.Min(by1, by2) + 1;
                    int bo = Math.Max(by1, by2) - 1;
                    if (ty <= bo) DrawV(bx1, ty, bo, EC);
                }
                else // ── L-shape: horizontal leg then 90° corner then vertical ──
                {
                    // Horizontal leg runs from source to target column, at source row
                    int cornerX = bx2;           // corner is directly above/below target
                    int lx = Math.Min(bx1 + 1, cornerX);
                    int rx = Math.Max(bx1 + 1, cornerX);
                    if (lx <= rx) DrawH(lx, rx - 1, by1, EC);
                    Corner(cornerX, by1, EC);   // 90° bend

                    // Vertical leg from corner down/up to target
                    int ty = Math.Min(by1 + 1, by2 - 1);
                    int bo = Math.Max(by1 + 1, by2 - 1);
                    if (ty <= bo) DrawV(cornerX, ty, bo, EC);
                }
            }
        }

        // ── Nodes (drawn over edges) ──────────────────────────────────────────
        // Build the set of nodes adjacent to ANY visited position for fog-of-war.
        // These are shown as a dim label so the player can see exits without full info,
        // and they remain visible even after the player moves away.
        var neighborSet = visibleNodes is not null
            ? new HashSet<string>(
                visibleNodes
                    .SelectMany(vid => sys.GetNode(vid).AdjacentNodeIds)
                    .Where(id => !visibleNodes.Contains(id)))
            : null;

        foreach (var (id, (gx, gy)) in layout)
        {
            int bx = BX(gx), by = BY(gy);

            if (visibleNodes is not null && !visibleNodes.Contains(id))
            {
                // Unvisited — render real key label (dim) if adjacent to current node
                if (neighborSet!.Contains(id))
                {
                    string nk = NodeKey(id);
                    Put(bx,     by, nk.Length > 0 ? nk[0] : '?', ConsoleColor.DarkGray);
                    if (nk.Length > 1) Put(bx + 1, by, nk[1], ConsoleColor.DarkGray);
                }
                continue;
            }

            var  node      = sys.GetNode(id);
            bool isCurrent = id == persona.CurrentNodeId;
            bool hasIce    = node.GetLiveIce().Any();
            bool conquered = node.IsConquered;
            ConsoleColor nc = isCurrent ? ConsoleColor.Yellow
                            : conquered ? ConsoleColor.DarkGreen
                            : hasIce    ? ConsoleColor.Red
                            : ColorForNode(node.Color);

            string key = NodeKey(id);

            // Unicode type symbol: CPU=⬡ DS=⛁ IOP=△ SAN=□ SM=○ SPU=◇
            char sym = node.Type switch
            {
                NodeType.CPU => '\u2B21', // ⬡ Hexagon
                NodeType.DS  => '\u26C1', // ⛁ White Draughts King
                NodeType.IOP => '\u25B3', // △ Triangle
                NodeType.SAN => '\u25A1', // □ Square
                NodeType.SM  => '\u25CB', // ○ Circle
                NodeType.SPU => '\u25C7', // ◇ Diamond
                _            => key.Length > 0 ? key[0] : '?'
            };

            // Layout: [*]sym key  — asterisk (current), symbol, then key label for navigation
            if (isCurrent)
            {
                Put(bx - 1, by, '*',  ConsoleColor.Yellow);
                Put(bx,     by, sym,  nc);
                Put(bx + 1, by, key.Length > 0 ? key[0] : ' ', ConsoleColor.Yellow);
                if (key.Length > 1) Put(bx + 2, by, key[1], ConsoleColor.Yellow);
            }
            else
            {
                Put(bx,     by, sym,  nc);
                Put(bx + 1, by, key.Length > 0 ? key[0] : ' ', ConsoleColor.DarkGray);
                if (key.Length > 1) Put(bx + 2, by, key[1], ConsoleColor.DarkGray);
            }
        }

        return (ch, co, bW, bH);
    }

    // Node info lines for right pane
    private List<CL> BuildNodeInfoLines(Node node, MatrixSystem system, Persona persona, int w)
    {
        bool inCombat = persona.CombatState == CombatState.Active;
        char spin     = SpinnerChars[_spinnerIndex];

        // Header row: "NODE INFO" with spinner on right when visible
        string header  = " NODE INFO";
        if (inCombat && w > header.Length + 2)
        {
            int pad  = w - header.Length - 2;
            header   = header + new string(' ', pad) + spin + " ";
        }
        var lines = new List<CL> { new(header, inCombat ? ConsoleColor.Cyan : ConsoleColor.DarkGray) };
        string key = NodeKey(node.Id);
        lines.Add(new CL(Trunc($" ({key}) {node.Type} {node.Color} SR:{node.SecurityRating}", w), ColorForNode(node.Color)));
        lines.Add(new CL(Trunc($" \"{node.Label}\"", w), ColorForNode(node.Color)));
        var ice = node.GetLiveIce().ToList();
        if (ice.Count == 0 && node.IceInstances.Count == 0) lines.Add(new CL(" ICE: None", ConsoleColor.Green));
        else if (ice.Count == 0) lines.Add(new CL(" ICE: Defeated", ConsoleColor.DarkGray));
        else
        {
            // Only show hidden Tar ICE once the primary ICE has been defeated —
            // it lurks invisibly until then.
            Ice? primary      = node.GetPrimaryIce();
            bool primaryAlive = primary is not null && primary.IsAlive;

            foreach (var i in ice)
            {
                if (i.Spec.IsHidden && primaryAlive) continue;

                bool revealed  = DevSettings.DevMode || _session.IsIceRevealed(i);
                string iceName = revealed ? i.Spec.Type.ToString() : "Detected";
                string hpBar   = revealed ? $"[{Bar(i.CurrentHealth, i.MaxHealth, 6)}]" : "[??????]";
                string probe   = revealed && i.Spec.IsTraceType && i.ProbePosition.HasValue
                                    ? $" [{ProbeBar(i.ProbePosition.Value, 4)}]" : "";
                string rating  = revealed ? $" R{i.EffectiveRating}" : "";
                lines.Add(new CL(Trunc($" ICE: {iceName}{rating}{hpBar}{probe}", w),
                                 revealed ? IceTypeColor(i.Spec.Type) : ConsoleColor.Red));

                // ICE cooldown bar — shown when ICE is actively in combat
                if (i.IsInCombat)
                {
                    var (cdRem, cdMax) = _session.GetIceAttackCooldown(i);
                    bool cdReady = cdRem <= 0f;
                    float cdFilled = cdMax > 0f ? 1f - (cdRem / cdMax) : 1f;
                    string cdLabel = cdReady ? " ATK[READY" : $" ATK[{cdRem:F1}s";
                    string iceBar  = Trunc($"{cdLabel} {Bar(cdFilled, 1.0f, 5)}]", w);
                    ConsoleColor iceBarColor = i.Spec.Type == IceType.BlackIce
                        ? (cdReady ? ConsoleColor.Red : ConsoleColor.DarkRed)
                        : (cdReady ? ConsoleColor.Yellow : ConsoleColor.DarkYellow);
                    lines.Add(new CL(iceBar, iceBarColor));
                }
            }
        }
        var nd = SystemCatalog.FindNodeDef(_systemNumber, key);
        if (nd?.TodoSecondaryIce is not null)
            lines.Add(new CL(Trunc($" +{nd.TodoSecondaryIce.Type} R{nd.TodoSecondaryIce.BaseRating}", w), ConsoleColor.DarkGray));
        lines.Add(new CL(
            Trunc(persona.CombatState == CombatState.Active ? " \u2694 COMBAT" : " CLEAR", w),
            persona.CombatState == CombatState.Active ? ConsoleColor.Red : ConsoleColor.Green));
        return lines;
    }

    private void RenderCombatLog(int w)
    {
        int inner    = w - 2;
        int maxLines = 6;
        VC.Write("\u2551"); VC.ForegroundColor = ConsoleColor.White;
        VC.Write(RenderHelper.VisualPadRight("  RUN LOG", inner)); VC.ResetColor(); VC.WriteLine("\u2551");

        var log = _session.SessionLog;

        var rows = new List<(string text, ConsoleColor color)>();

        for (int i = 0; i < log.Count; i++)
        {
            var  evt   = log[i];
            bool isNew = i >= _lastLogCount;
            ConsoleColor col = isNew ? EventFgColor(evt.Type) : DimEventColor(evt.Type);

            // Compute prefix and its TRUE display width (icon may be 2 columns wide).
            string prefix         = $"  {LogIcon(evt.Type)} {evt.Timestamp:HH:mm:ss}  ";
            int    prefixDispW    = RenderHelper.VisualWidth(prefix);
            int    continuationW  = prefixDispW; // same indent width for wrapped lines
            int    textW          = Math.Max(1, inner - prefixDispW);

            var chunks = WrapTextVisual(evt.Description, textW);

            for (int ci = 0; ci < chunks.Count; ci++)
            {
                string indent = ci == 0 ? prefix : new string(' ', continuationW);
                string line   = indent + chunks[ci];
                // Pad/truncate by display width so the closing ║ always lands correctly.
                rows.Add((RenderHelper.VisualPadRight(line, inner), col));
            }
        }

        // Show the last maxLines rows
        int start = Math.Max(0, rows.Count - maxLines);
        int shown = 0;
        for (int i = start; i < rows.Count; i++, shown++)
        {
            VC.Write("\u2551"); VC.ForegroundColor = rows[i].color;
            VC.Write(rows[i].text);
            VC.ResetColor(); VC.WriteLine("\u2551");
        }
        for (int i = shown; i < maxLines; i++) RenderHelper.DrawWindowBlankLine(w);
    }

    /// <summary>
    /// Splits <paramref name="text"/> into lines whose DISPLAY width does not
    /// exceed <paramref name="maxCols"/>, breaking at word boundaries where possible.
    /// </summary>
    private static List<string> WrapTextVisual(string text, int maxCols)
    {
        if (maxCols <= 0) return [string.Empty];
        if (RenderHelper.VisualWidth(text) <= maxCols) return [text];

        var chunks = new List<string>();
        while (text.Length > 0)
        {
            // Find the furthest char index whose display width <= maxCols
            int cols = 0, splitAt = 0;
            for (int i = 0; i < text.Length; i++)
            {
                int cw = RenderHelper.VisualCharWidth(text[i]);
                if (cols + cw > maxCols) break;
                cols += cw;
                splitAt = i + 1;
            }
            if (splitAt == 0) splitAt = 1; // safety: always advance

            // Prefer breaking on a word boundary
            string chunk = text[..splitAt];
            int wordBreak = chunk.LastIndexOf(' ');
            if (wordBreak > 0 && splitAt < text.Length)
            {
                splitAt = wordBreak;
                chunk   = text[..splitAt];
            }

            chunks.Add(chunk.TrimEnd());
            text = text[splitAt..].TrimStart();
        }
        return chunks.Count > 0 ? chunks : [string.Empty];
    }

    private void RenderDeck(Persona persona, int w)
    {
        int inner = w - 2;
        var deck = persona.Deck;
        var slots = deck.LoadedSlots;
        bool runMode = _overlay == OverlayMode.RunProgram;

        // ── Split geometry (same ratio as the map/node split above) ──────────
        int leftW = inner * 43 / 100;
        int rightW = inner - leftW - 1;   // -1 for the │ divider

        // ── Build slot strings ────────────────────────────────────────────────
        var slotStrs = new string[5];
        for (int i = 0; i < 5; i++)
        {
            var p = slots[i];
            if (p is null) slotStrs[i] = "[___]";
            else
            {
                string nm = p.Spec.Name.ToString(); nm = nm[..Math.Min(3, nm.Length)];
                string rd = p.IsReadyToRun ? "" : $"{p.LoadProgress:P0}";
                slotStrs[i] = rd.Length > 0 ? $"[{nm}{p.Spec.Level}\u2026{rd}]" : $"[{nm}{p.Spec.Level}]";
            }
        }

        // ── RIGHT: energy/health bar + load bar values (computed once) ──────────
        int barW = Math.Max(4, (rightW - 18) / 2);   // half-width bar
        bool isFightingBlackIce = _session.Persona.IsFightingBlackIce;
        bool inCombat = _session.Persona.CombatState == CombatState.Active;

        // Energy bar OR physical health bar (red) when fighting Black ICE
        ConsoleColor ec;
        string energyBar;
        if (isFightingBlackIce)
        {
            ec = ConsoleColor.Red;
            energyBar = $" HP [{Bar(_session.Decker.PhysicalHealth, _session.Decker.PhysicalHealthMax, barW)}] {_session.Decker.PhysicalHealth:F0}/{_session.Decker.PhysicalHealthMax:F0}";
        }
        else
        {
            float ratio = persona.EnergyMax > 0 ? persona.Energy / persona.EnergyMax : 0f;
            ec = ratio > 0.6f ? ConsoleColor.Green : ratio > 0.3f ? ConsoleColor.Yellow : ConsoleColor.Red;
            energyBar = $" EN [{Bar(persona.Energy, persona.EnergyMax, barW)}] {persona.Energy:F0}/{persona.EnergyMax:F0}";
        }

        var loading = deck.LoadedSlots.FirstOrDefault(p => p is not null && p.LoadProgress < 1.0f);
        string? loadBar = null;
        if (loading is not null)
        {
            string nm = loading.Spec.Name.ToString(); nm = nm[..Math.Min(4, nm.Length)];
            loadBar = $" LD [{Bar(loading.LoadProgress, 1.0f, barW)}] {nm} {loading.LoadProgress:P0}";
        }

        // Cooldown bar (only in combat, only when not loading)
        string? cdBar = null;
        if (inCombat && loadBar is null)
        {
            float filled = _playerCooldownMax > 0f ? 1f - (_playerCooldown / _playerCooldownMax) : 1f;
            bool ready = _playerCooldown <= 0f;
            string cdLabel = ready ? " CD [READY" : $" CD [{_playerCooldown:F1}s";
            cdBar = $"{cdLabel} {Bar(filled, 1.0f, barW)}]";
        }

        // ── ROW 1: slot bar (left) | energy bar (right) ───────────────────────
        {
            // Left: "DECK  [slot][slot]..."
            string prefix = "  DECK  ";
            int slotTotal = slotStrs.Sum(s => s.Length);
            // Build left content string
            var leftSB = new System.Text.StringBuilder(prefix);
            // We'll write it manually for cursor-highlight support, so measure first
            int leftUsed = prefix.Length + slotTotal;
            int leftPad = Math.Max(0, leftW - leftUsed);

            VC.Write("\u2551");
            VC.ForegroundColor = ConsoleColor.Cyan;
            VC.Write(prefix);
            VC.ResetColor();
            for (int i = 0; i < 5; i++)
            {
                if (runMode && i == _programCursor)
                { VC.BackgroundColor = ConsoleColor.DarkYellow; VC.ForegroundColor = ConsoleColor.Black; VC.Write(slotStrs[i]); VC.ResetColor(); }
                else
                { VC.ForegroundColor = runMode ? ConsoleColor.DarkGray : ConsoleColor.Cyan; VC.Write(slotStrs[i]); VC.ResetColor(); }
            }
            if (leftPad > 0) VC.Write(new string(' ', leftPad));
            VC.Write("\u2502");

            // Right: energy bar
            string right1 = energyBar.Length > rightW ? energyBar[..rightW] : energyBar.PadRight(rightW);
            VC.ForegroundColor = ec;
            VC.Write(right1);
            VC.ResetColor();
            VC.WriteLine("\u2551");
        }

        // ── ROW 2: mem/stor (left) | cooldown bar (combat, no loading) or load bar (loading) or blank ──
        {
            string memStor = $"  Mem:{deck.UsedMemory()}/{deck.Stats.MemoryMax}Mp  Stor:{deck.UsedStorage()}/{deck.Stats.StorageMax}Mp";
            if (memStor.Length > leftW) memStor = memStor[..leftW];

            VC.Write("\u2551");
            VC.ForegroundColor = ConsoleColor.DarkGray;
            VC.Write(memStor.PadRight(leftW));
            VC.ResetColor();
            VC.Write("\u2502");

            if (loadBar is not null)
            {
                // Loading in progress — show load bar, cooldown is hidden
                string right2 = loadBar.Length > rightW ? loadBar[..rightW] : loadBar.PadRight(rightW);
                VC.ForegroundColor = ConsoleColor.Cyan;
                VC.Write(right2);
                VC.ResetColor();
            }
            else if (cdBar is not null)
            {
                // No loading — show player cooldown bar
                bool ready = _playerCooldown <= 0f;
                VC.ForegroundColor = ready ? ConsoleColor.Green : ConsoleColor.DarkYellow;
                string cdLine = cdBar.Length > rightW ? cdBar[..rightW] : cdBar.PadRight(rightW);
                VC.Write(cdLine);
                VC.ResetColor();
            }
            else
            {
                VC.Write(new string(' ', rightW));
            }
            VC.WriteLine("\u2551");
        }
    }

    private void RenderActions(int w)
    {
        int inner = w - 2;
        if (_overlay == OverlayMode.RunProgram)
        {
            string hint = "  [\u2190\u2192] Select slot   [Enter] Execute   [Esc] Cancel";
            VC.Write("\u2551"); VC.ForegroundColor = ConsoleColor.Yellow;
            VC.Write(hint.PadRight(inner).Length > inner ? hint[..inner] : hint.PadRight(inner)); VC.ResetColor(); VC.WriteLine("\u2551");
            var p = _session.Decker.Deck.LoadedSlots[_programCursor];
            Ice? ai = _session.System.GetNode(_session.Persona.CurrentNodeId).GetActiveIce();
            string ice = (ai is not null && p?.IsReadyToRun == true) ? $"  vs ICE:{_session.Persona.ComputeSuccessChance(p, ai):P0}" : "";
            string info = p is null ? $"  Slot {_programCursor + 1}: [empty]"
                : $"  Slot {_programCursor + 1}: {p.Spec.Name} L{p.Spec.Level}  {(p.IsReadyToRun ? "READY" : $"Loading {p.LoadProgress:P0}")}{ice}";
            VC.Write("\u2551"); VC.ForegroundColor = ConsoleColor.Yellow;
            VC.Write(info.PadRight(inner).Length > inner ? info[..inner] : info.PadRight(inner)); VC.ResetColor(); VC.WriteLine("\u2551");
        }
        else
        {
            // 2×3 action grid — highlighted cell tracks _mainCursor for controller navigation.
            // Travel [2] is dimmed when live ICE is present AND the node hasn't been bypassed.
            // Node Action [3] is dimmed whenever live ICE is present (bypass doesn't unlock actions).
            bool hasIce    = !DevSettings.DevMode && HasLiveIce();
            bool bypassed  = _session.IsNodeBypassed(_session.Persona.CurrentNodeId);
            int  cellW     = inner / 3;
            int  rem       = inner - cellW * 3;   // give any leftover cols to the last cell

            string[] labels = { "[1] Run Program", "[2] Travel", "[3] Node Action",
                                 "[4] Jack Out",    "[5] Run Info", "[6] Deck Status" };

            for (int row = 0; row < 2; row++)
            {
                VC.Write("\u2551");
                for (int col = 0; col < 3; col++)
                {
                    int    idx  = row * 3 + col;
                    bool   sel  = (idx == _mainCursor);
                    bool   dim  = (idx == 1 && hasIce && !bypassed)   // Travel: dim only if not bypassed
                               || (idx == 2 && hasIce)                 // Node Action: always dim if ICE present
                               || (idx == 4 && _session.ActiveRun is null);
                    int    cw   = (col == 2) ? cellW + rem : cellW;
                    string cell = (" " + labels[idx]).PadRight(cw);
                    if (cell.Length > cw) cell = cell[..cw];

                    if (sel) { VC.BackgroundColor = ConsoleColor.DarkYellow; VC.ForegroundColor = ConsoleColor.Black; }
                    else if (dim)  VC.ForegroundColor = ConsoleColor.DarkGray;
                    else           VC.ForegroundColor = ConsoleColor.White;
                    VC.Write(cell);
                    VC.ResetColor();
                }
                VC.WriteLine("\u2551");
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ACTION HANDLERS
    // ═══════════════════════════════════════════════════════════════════════════

    private IScreen DoJackOut()
    {
        TryAwardRunReward();
        _session.JackOut();
        // Physical health replenishes after disconnecting from the Matrix
        _session.Decker.HealPhysical(_session.Decker.PhysicalHealthMax);
        _gameState.ActiveSession = null;
        return new MatrixSessionEndScreen(_session, _systemNumber, "jack_out", _gameState);
    }

    /// <summary>
    /// Builds the appropriate end-of-session screen and performs all cleanup.
    /// Called both from Tick (auto-transition) and HandleInput (fallback).
    /// </summary>
    private IScreen BuildSessionEndScreen()
    {
        _overlay = OverlayMode.None;
        _gameState.ActiveSession = null;

        if (_session.EndReason == "decker_dead")
            return new GameOverScreen(_session, _gameState);

        // All other endings: heal physical health on disconnect
        _session.Decker.HealPhysical(_session.Decker.PhysicalHealthMax);
        TryAwardRunReward();
        return new MatrixSessionEndScreen(_session, _systemNumber, _session.EndReason, _gameState);
    }

    private void TryAwardRunReward()
    {
        var run = _session.ActiveRun;
        if (run is null || !run.ObjectiveAchieved || run.RewardClaimed) return;
        int nuyen = run.ComputePay(_session.Decker.NegotiationSkill), karma = run.KarmaReward;
        _session.Decker.AddNuyen(nuyen); _gameState.Karma += karma;
        run.MarkRewardClaimed(); _gameState.PendingReward = RunCompletionResult.Ok(nuyen, karma);
    }

    private IScreen? ShowRunInfo()
    {
        if (_session.ActiveRun is null) { _pendingError = "Free run \u2014 no contract active."; return null; }
        return new RunInfoScreen(_session.ActiveRun, _gameState, _session.Decker);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ═══════════════════════════════════════════════════════════════════════════

    private static string Trunc(string s, int max) => RenderHelper.VisualTruncate(s, max);

    private static string Bar(float c, float m, int w) { if (m <= 0) return new string('\u2591', w); int f = (int)(w * Math.Clamp(c / m, 0f, 1f)); return new string('\u2588', f) + new string('\u2591', w - f); }
    private static string ProbeBar(float pos, int w) { int p = (int)(w * Math.Clamp(pos, 0f, 1f)); var b = Enumerable.Repeat('\u2500', w).ToArray(); if (p < w) b[p] = '\u25ba'; return new string(b); }
    private static string NodeKey(string id) { int d = id.IndexOf('-'); return d >= 0 ? id[(d + 1)..] : id; }

    private static ConsoleColor ColorForNode(NodeColor c) => c switch { NodeColor.Blue => ConsoleColor.Cyan, NodeColor.Green => ConsoleColor.Green, NodeColor.Orange => ConsoleColor.Yellow, NodeColor.Red => ConsoleColor.Red, _ => ConsoleColor.Gray };
    private static ConsoleColor IceTypeColor(IceType t) => t switch { IceType.BlackIce => ConsoleColor.DarkRed, IceType.Killer => ConsoleColor.Red, IceType.Blaster => ConsoleColor.Magenta, IceType.TraceAndBurn => ConsoleColor.Red, IceType.TraceAndDump => ConsoleColor.Yellow, IceType.TarPit => ConsoleColor.DarkYellow, IceType.TarPaper => ConsoleColor.DarkYellow, IceType.Barrier => ConsoleColor.Cyan, IceType.Access => ConsoleColor.DarkCyan, _ => ConsoleColor.Gray };
    private static ConsoleColor AlertColor(AlertState a) => a switch { AlertState.Normal => ConsoleColor.Green, AlertState.Passive => ConsoleColor.Yellow, AlertState.Active => ConsoleColor.Red, _ => ConsoleColor.Gray };
    private static string AlertLabel(AlertState a) => a switch { AlertState.Normal => "NORMAL", AlertState.Passive => "\u26a0 PASSIVE", AlertState.Active => "\U0001f534 ACTIVE", _ => a.ToString().ToUpper() };

    private static string LogIcon(SessionEventType t) => t switch
    {
        SessionEventType.PersonaDamaged => "\u26a1", SessionEventType.PersonaDumped => "\u2620", SessionEventType.DeckDamaged => "\u2762",
        SessionEventType.IceDefeated => "\u2713", SessionEventType.NodeConquered => "\u2605", SessionEventType.RunObjectiveMet => "\u2605",
        SessionEventType.ProgramRun => "\u25b6", SessionEventType.ProgramFailed => "\u2717", SessionEventType.CombatEngaged => "\u2694",
        SessionEventType.CombatMiss => "\u25e6",
        SessionEventType.AlertEscalated => "\u26a0", SessionEventType.AlertCancelled => "\u2713", SessionEventType.NodeEntered => "\u2192",
        SessionEventType.TarEffectTriggered => "\u26a0", SessionEventType.JackOutAttempted => "\u2190", SessionEventType.JackOutSucceeded => "\u2190",
        SessionEventType.DataFileFound => "\u25a1", SessionEventType.DataFileTransferred => "\u2191",
        SessionEventType.NodeActionResult => "\u00b7",
        SessionEventType.NodeActionSuccess => "\u2713",
        SessionEventType.NodeActionFailure => "\u2717",
        _ => "\u00b7"
    };
    private static ConsoleColor EventFgColor(SessionEventType t) => t switch
    {
        SessionEventType.PersonaDamaged or SessionEventType.PersonaDumped or SessionEventType.DeckDamaged => ConsoleColor.Red,
        SessionEventType.AlertEscalated or SessionEventType.TarEffectTriggered => ConsoleColor.Yellow,
        SessionEventType.IceDefeated or SessionEventType.NodeConquered or SessionEventType.RunObjectiveMet or SessionEventType.AlertCancelled => ConsoleColor.Green,
        SessionEventType.NodeActionSuccess => ConsoleColor.Green,
        SessionEventType.NodeActionFailure => ConsoleColor.Red,
        SessionEventType.ProgramRun or SessionEventType.NodeEntered or SessionEventType.DataFileFound or SessionEventType.DataFileTransferred => ConsoleColor.Cyan,
        SessionEventType.CombatMiss => ConsoleColor.DarkGray,
        SessionEventType.NodeActionResult => ConsoleColor.Gray,
        SessionEventType.ProgramFailed => ConsoleColor.Yellow, SessionEventType.CombatEngaged => ConsoleColor.Magenta, _ => ConsoleColor.White
    };
    private static ConsoleColor DimEventColor(SessionEventType t) => t switch
    {
        SessionEventType.PersonaDamaged or SessionEventType.PersonaDumped or SessionEventType.DeckDamaged => ConsoleColor.DarkRed,
        SessionEventType.AlertEscalated or SessionEventType.TarEffectTriggered => ConsoleColor.DarkYellow,
        SessionEventType.IceDefeated or SessionEventType.NodeConquered => ConsoleColor.DarkGreen,
        SessionEventType.NodeActionSuccess => ConsoleColor.DarkGreen,
        SessionEventType.NodeActionFailure => ConsoleColor.DarkRed,
        SessionEventType.CombatMiss or SessionEventType.NodeActionResult => ConsoleColor.DarkGray,
        SessionEventType.ProgramRun => ConsoleColor.DarkCyan, SessionEventType.ProgramFailed => ConsoleColor.DarkYellow,
        SessionEventType.CombatEngaged => ConsoleColor.DarkMagenta, _ => ConsoleColor.DarkGray
    };
}
