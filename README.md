# Installation:

A. Install with the [Crustacean Installer](https://github.com/o7Moon/Crustacean/blob/main/README.md) make sure to install qol-core too!!! it will not work otherwise

B. Manual installation instructions below:
- Download [Bepinex](https://builds.bepinex.dev/projects/bepinex_be/577/BepInEx_UnityIL2CPP_x64_ec79ad0_6.0.0-be.577.zip).
- Open your game folder (In steam, right click crab game, Manage > Browse local files).
- extract Bepinex so that all of it's files are in the game folder.
- run the game once. This will take some time.
- close the game.
- download `qol-core.dll` from [Releases](https://github.com/o7Moon/qol-core/releases).
- move `qol-core.dll` to `(Game Folder from step 2)/BepInEx/plugins/qol-core.dll`.
- download `hostutils.dll` from [Releases](https://github.com/o7Moon/CrabGame.HostUtils/releases).
- move `hostutils.dll` to `(Game Folder from step 2)/BepInEx/plugins/hostutils.dll`.

# How to use:
- type `/help` to see an ingame list of commands and syntax instructions. the hostutils commands will only work for lobbies you host, and only for you.
- open your game folder, double click bepinex, then config, then hostutils.cfg. edit that file in notepad and then save it to change settings.
## short description of commands:
- `/forcenextmap`: force the game to pick a certain map
- `/forcenextmode`: force the game to pick a certain mode
- `/vaporize`: explode a player
- `/steamid`: get a player's steam id
- `/restart`: return to the lobby
- `/start`: force the game to start
- `/time`: set the time remaining for the current gamemode
- `/rename`: change the lobby name
