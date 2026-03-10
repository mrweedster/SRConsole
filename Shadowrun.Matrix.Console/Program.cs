// Shadowrun Genesis — Matrix Console Game
using Shadowrun.Matrix.Data;
using Shadowrun.Matrix.Persistence;
using Shadowrun.Matrix.Models;
using Shadowrun.Matrix.UI;
using Shadowrun.Matrix.UI.Screens;
using Shadowrun.Matrix.ValueObjects;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.Title          = "Shadowrun Genesis — Matrix";

// ── Load contracts ────────────────────────────────────────────────────────────

var availableRuns = MatrixRunCatalog.Build();

// ── Load saved game or prompt for hacker name on a fresh start ───────────────

var saved = SaveGameManager.Load();

ConsoleGame game;
if (saved.HasValue)
{
    // Existing save — go straight to Main Menu
    game = new ConsoleGame(saved.Value.decker, availableRuns, saved.Value.state);
}
else
{
    // No save — show the name-entry screen first, then Main Menu
    game = new ConsoleGame(availableRuns, new NewGameNameScreen(availableRuns));
}

game.Run();
