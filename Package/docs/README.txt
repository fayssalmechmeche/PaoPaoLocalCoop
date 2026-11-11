=====================================================================
LOCAL CO-OP FOR PAO PAO  (unofficial mod)  -  version 0.3.2
=====================================================================

Play with two players on the SAME PC using two gamepads: the screen is split in two
(one game window on the left, one on the right), or one screen per player if you have two.

REQUIREMENTS
  - PAO PAO (full game) installed in Steam, with Steam running and signed in.
  - Windows 10 or 11 (64-bit).
  - Two Xbox-compatible gamepads (XInput), plugged in BEFORE starting.
  - Nothing else to install.

INSTALL AND PLAY
  1. Extract the WHOLE zip into a folder (it does not need to be in the game folder).
  2. Double-click  PaoPaoLocalCoop.exe
  3. On first run it offers to install the mod in the game folder (click Yes), then prepares
     the game for 1 to 3 minutes: a window opens and closes by itself, don't touch anything.
     Then the two game windows open on the menu.
  4. To play again: double-click PaoPaoLocalCoop.exe again.

HOW TO PLAY TOGETHER
  - In one window: LOCAL CO-OP > HOST, then pick a world (or "+" for a new world), like in solo.
  - In the other window: LOCAL CO-OP > JOIN. Order doesn't matter: "Join" waits until the game
    is open, then enters by itself (Cancel button to stop).
  - The normal (online) CO-OP button is unchanged.

TWO SCREENS
  With two screens or more, the launcher asks at start:
  - Split screen: both windows side by side on the main screen.
  - One screen per player: each window fills its own screen (full picture, normal text size).
    Choose which player goes on which screen; "Identify screens" shows each screen's number.
  Tick "Don't ask again" to keep your choice. To change it later: PaoPaoLocalCoop.exe --screens

CONTROLS
  - Each player uses their own gamepad. First gamepad plugged in = left window,
    second = right window (whoever hosts).
    With ONE gamepad only: it controls the right window; the left window uses keyboard/mouse.
  - Tool wheel: hold LB, aim with the RIGHT STICK, release LB.
  - The left window also accepts keyboard and mouse while it is in front.

SAVES
  The world belongs to the host: it is the same file as in solo.
  The joining player is stored inside that world (as "Player 2").
  Back up your saves before trying (folder  %USERPROFILE%\AppData\LocalLow\ArtDock\PaoPao).

OPTIONS  (options.ini file, next to the launcher)
  - Taskbar: hidden while playing, or visible (default).
  - Automatic start (auto_start=yes): skipping the menu, the left window loads the last world
    played and the right window joins it by itself.
  - Screens: split screen or one screen per player, and whether to ask at start.
  - Game folder, if automatic detection cannot find it.

TROUBLESHOOTING
  - The game must be closed before starting the launcher.
  - If the taskbar stays hidden (option enabled): run  PaoPaoLocalCoop.exe --show-taskbar
  - Logs: %LOCALAPPDATA%\PaoPaoLocalCoop\  and, in the game folder, BepInEx\LogOutput.log
  - After a game update, the setup (1 to 3 min) may run once again.
  - A game update can break the mod: it relies on an internal test tool from the developers.
    It may stop working at any time.

UNINSTALL
  Run  PaoPaoLocalCoop.exe --uninstall  (close the game first). Your saves are not touched.

IMPORTANT
  Unofficial fan mod, not affiliated with the developers (ArtDock). Use at your own risk.
  No game file is modified: files are added to the game folder.
  Play with the game you bought. This mod is not a way to play without owning the game.
  See LICENSES.txt for the components used (BepInEx, etc.).
