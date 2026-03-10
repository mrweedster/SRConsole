## Shadowrun.Matrix.Console
This is an AI port of the Matrix game of Shadowrun on Genesis / Mega Drive.

It is a pure terminal/console game without graphics, animations or sound.

Multiplatform support if the required dotnet is installed.

Tested on Windows, Mac and Linux. 

It has built-in controller support (Tested with PS5 and Xbox360)

### Description & Introduction
You are a Decker who wants to become the best. Start by giving your character a name.

Now you should first select the black market and get appropriate programs for
your first run. Attack and deception are the best choices, level your attack to 2 minimum.
After the purchase you have to load the programs in the decker -> cyberdeck menu.

Now you're set for your first run. Go to the run menu and chose the first one to
crash the CPU. Confirm the dialogue and start the run. You will see the matrix main screen
with a map and enemy node info in the middle, the log and cyberdeck infos below, and at the
bottom the action menu. With the action menu you can travel to different nodes.
When ICE is present you first have to defeat the ICE to operate the node or travel.

Go to the CPU node and defeat the ICE. Hit 1 and chose the attack program slot either
with keyboard or with the cursor and return. This may take some attempts but you won't die
if the ICE drops your energy below 0 and you can redo the run without penalties.

After you've defeated the ICE you can now either use the node function goto to jump
to any node you wish. The interesting ones are DS nodes where you can steal data and
later sell it in the black market. The GoTo jump only works from CPU nodes. 
Jump to the DS node and defeat the ICE. Operate the node and do 'transfer data' until
you fetch something. Per run only one datafile can be extracted from a DS.

After you went back to the CPU use the node function 'crash system' to crash the CPU
and finish the run. Collect your reward and redo the run once more to have enough
Karma to increase your computer skill to level 5. 

The stolen data can then be sold in the black market and you can upgrade your deck.
From there on you should know enough to become the best of the best. The game
has no real winning / ending condition except if you are killed by black ICE. 

Have a lot of fun

## Requires
  * dotnet10

### Build from source
```
git clone https://github.com/mrweedster/SRConsole.git
```
### Run
```
cd <where the sln file resides>
dotnet run --project Shadowrun.Matrix.Console
```

### Installer
None

### Issues
- There are surely issues left which i'm unaware of. Let me know by issuing an issue on github

### Fixes

## Legal:
This project is in no way affiliated with Microsoft or any legal holders
