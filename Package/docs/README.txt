COOP LOCALE POUR PAO PAO  (mod non officiel)  -  version 0.3.0
=====================================================================
English version below / Version anglaise plus bas.

Joue a deux sur le MEME PC avec deux manettes : l'ecran est coupe en deux
(une fenetre de jeu a gauche, une a droite).

CE QU'IL FAUT
  - PAO PAO (jeu complet) installe dans Steam, et Steam ouvert avec ton compte.
  - Windows 10 ou 11 (64 bits).
  - Deux manettes compatibles Xbox (ou Xbox mode XInput), branchees AVANT de lancer.
  - Rien d'autre a installer.

INSTALLATION ET LANCEMENT
  1. Decompresse TOUT le zip dans un dossier (pas besoin d'etre dans le dossier du jeu).
  2. Double-clique sur  PaoPaoLocalCoop.exe
  3. Au premier lancement, il propose d'installer le mod dans le dossier du jeu (clique Oui),
     puis prepare le jeu pendant 1 a 3 minutes : une fenetre s'ouvre et se ferme toute seule,
     ne touche a rien. Ensuite les deux fenetres de jeu s'ouvrent sur le menu.
  4. Pour rejouer : double-clique a nouveau sur PaoPaoLocalCoop.exe.

COMMENT JOUER A DEUX
  - Dans une fenetre : COOP LOCALE > HEBERGER, puis choisis un monde (ou "+" pour un nouveau
    monde), comme en solo.
  - Dans l'autre fenetre : COOP LOCALE > REJOINDRE. L'ordre n'a pas d'importance : "Rejoindre"
    attend que la partie soit ouverte, puis entre tout seul (bouton Annuler pour arreter).
  - Le bouton COOP normal (en ligne) n'est pas modifie.

COMMANDES
  - Chaque joueur utilise sa manette. La premiere manette branchee = fenetre de gauche,
    la deuxieme = fenetre de droite (quel que soit celui qui heberge).
    Avec UNE seule manette : elle commande la fenetre de droite, celle de gauche se joue au clavier/souris.
  - Roue des outils : maintiens LB, oriente le STICK DROIT vers l'outil, relache LB.
  - La fenetre de gauche accepte aussi le clavier et la souris quand elle est au premier plan.

SAUVEGARDES
  Le monde appartient a celui qui heberge : c'est le meme fichier qu'en solo.
  Le joueur qui rejoint est enregistre dans ce monde (sous le nom "Player 2").
  Fais une copie de tes sauvegardes avant d'essayer (dossier  %USERPROFILE%\AppData\LocalLow\ArtDock\PaoPao).

OPTIONS  (fichier options.ini, a cote du lanceur)
  - Barre des taches : cachee pendant la partie, ou visible (par defaut).
  - Demarrage automatique (auto_start=yes) : sans passer par le menu, la fenetre de gauche
    reprend le dernier monde joue et celle de droite la rejoint toute seule.
  - Dossier du jeu si la detection automatique ne le trouve pas.

SI QUELQUE CHOSE NE VA PAS
  - Le jeu doit etre ferme avant de lancer le lanceur.
  - Si la barre des taches reste cachee (option activee) : lance  PaoPaoLocalCoop.exe --show-taskbar
  - Journaux : %LOCALAPPDATA%\PaoPaoLocalCoop\  et, dans le dossier du jeu, BepInEx\LogOutput.log
  - Apres une mise a jour du jeu, la preparation (1 a 3 min) peut se refaire une fois.
  - Une mise a jour du jeu peut casser le mod : le mod utilise un outil de test interne des
    developpeurs. Il peut ne plus marcher du jour au lendemain.

DESINSTALLER
  Lance :  PaoPaoLocalCoop.exe --uninstall   (ferme le jeu avant). Tes sauvegardes ne sont pas touchees.
  Dans une console : cd dans le dossier, puis  PaoPaoLocalCoop.exe --uninstall

IMPORTANT
  Mod de fan, non officiel, sans lien avec les developpeurs (ArtDock). Utilisation a tes risques.
  Aucun fichier du jeu n'est modifie : des fichiers sont ajoutes dans son dossier.
  Joue avec le jeu que tu as achete. Ce mod n'est pas un moyen de jouer sans le posseder.
  Voir LICENSES.txt pour les composants utilises (BepInEx, etc.).


=====================================================================
LOCAL CO-OP FOR PAO PAO  (unofficial mod)  -  version 0.3.0
=====================================================================

Play with two players on the SAME PC using two gamepads: the screen is split in two
(one game window on the left, one on the right).

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
