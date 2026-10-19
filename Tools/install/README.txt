THE TWELFTH STAMP
"You travelled alone. Something came back with you."

A first-person survival-puzzle horror game for Meta Quest 3 (as PC VR) and for keyboard and mouse.
Made for the SHIFT Game Jam, 17-18 October 2026 (theme: Festival of Passports). Version 0.9.0.

CONTENT WARNING: horror for adults. Loud jump scares and screams, growls, breathing close behind you, bone
cracks and drowning sounds, a figure that stalks and chases you, dark scenes and film grain. No visual gore.
For fewer, quieter scares and no jump scares, set Scare intensity to Gentle (first-run setup or Settings).


1. START THE GAME
-----------------
Double-click "Install Shortcuts.cmd" in this folder once. It puts two shortcuts on your Desktop:

    The Twelfth Stamp - Quest 3    (runs TheTwelfthStamp.exe --mode=vr)
    The Twelfth Stamp - Desktop    (runs TheTwelfthStamp.exe --mode=desktop)

The shortcuts point at the game in this folder. If you move the folder, run "Install Shortcuts.cmd" again.
To remove them, run:  powershell -NoProfile -ExecutionPolicy Bypass -File Install-Shortcuts.ps1 -Remove

You can also start it from a terminal:
    TheTwelfthStamp.exe --mode=desktop
    TheTwelfthStamp.exe --mode=vr
With no --mode argument the game starts in desktop mode.


2. QUEST 3 (PC VR) SETUP
------------------------
- Install the Meta Horizon Link app on this PC and connect the Quest 3 with a Link cable or Air Link.
- In the Meta Horizon Link app, make sure Meta Horizon Link is the active OpenXR runtime
  (Settings > General > OpenXR Runtime). The game never changes this setting for you.
- Start "The Twelfth Stamp - Quest 3".
- If VR cannot start, the desktop window explains why and offers Retry, Desktop Mode or Quit.
  The game never claims VR is running when it is not.


3. CONTROLS
-----------
The in-game Controls page (main menu and pause menu) is generated from the live bindings and is the
authority. Default layout:

Keyboard and mouse
    Move ........................ W A S D or arrow keys
    Run ......................... Hold Shift while moving forward (you can outrun the Passenger,
                                  but not by walking)
    Look ........................ Mouse
    Use / grab / put down ....... E
    Passport (open / close) ..... F
    Choose destination .......... Mouse wheel, or [ and ]
    Open / widen a tear ......... Hold left mouse button (aim at a seam with the passport open)
    Seal the tear ............... Hold right mouse button
    Recall carried item ......... R
    Objectives and steps ........ Tab (the place's step checklist; the current step is always shown)
    Map ......................... M (the minimap stays in a corner)
    Take the glasses off early .. Hold G for 2 seconds (a story choice: the curse pulls you back to
                                  the apartment; your verified seals are kept)
    Skip / continue ............. Space or Enter
    Pause ....................... Esc

Quest 3 controllers (right-handed; left-handed mode swaps them in Settings)
    Teleport .................... Push the left thumbstick, release (smooth movement is an option)
    Run (smooth movement) ....... Push the left thumbstick fully forward
    Turn ........................ Right thumbstick left / right (snap turn by default)
    Grab ........................ Grip (either hand)
    Use ......................... Trigger (either hand)
    Passport .................... X
    Next destination ............ B, or right thumbstick up / down
    Open a tear ................. Grip the seam and pull apart, or hold the right trigger
    Seal the tear ............... Hold A
    Recall carried item ......... Y
    Pause ....................... Menu button (left controller)
    Steps and map ............... The current step and the minimap stay in view (no full map in VR)

While you play
    The current step of each place is always on screen, and Tab lists them all.
    Every country hides three postcards (the map counts them). Lamps you light make a pool the
    Passenger will not enter: light them along your way before you run for a stamp.


4. SAFETY AND COMFORT
---------------------
- You can pause at any time, quit from the pause menu, and take the real headset off at any time.
  The real headset is never part of the story. The "glasses" in the story are a virtual object.
- Comfort options: teleport or smooth movement, snap or smooth turning, snap angle, seated mode,
  left-handed mode, haptics strength, reduced effects, subtitles (size and background), and a gentler
  scare intensity. Audio has separate Master, Music, Ambience, Effects, Voice and Interface volumes.
- The game contains darkness, sudden sounds, a figure that chases you (if it reaches you, you restart from
  the last checkpoint) and mild body horror (a reaching hand). No gore, no flashing lights, and the game
  never moves or shakes your view to scare you.


5. SAVES
--------
Progress is saved at checkpoints to:
    %USERPROFILE%\AppData\LocalLow\Awaiz Ahmed\The Twelfth Stamp\


6. TROUBLESHOOTING
------------------
- VR window stays on "VR could not start": open the Meta Horizon Link app, check that the headset shows
  as connected and that Link is the active OpenXR runtime, then press Retry.
- Black or frozen headset view: take the headset off, press Esc on the PC or quit with Alt+F4, and
  start the Quest 3 shortcut again.
- If unzipping fails, extract to a short folder such as C:\Games (Windows limits path length).
- The game writes a log to %USERPROFILE%\AppData\LocalLow\Awaiz Ahmed\The Twelfth Stamp\Player.log


7. CREDITS AND AI DISCLOSURE
----------------------------
See the Credits page in the game. This game was made with AI assistance: Claude Code wrote the code and the
scripts that generate the textures, models, sound effects, ambience and music; the narration voices come from
the Kokoro-82M text-to-speech model; ChatGPT image generation made the key art and the title logo (approved
by Awaiz Ahmed) and concept reference sheets.
The full asset and tool ledger is in the project's Docs folder.
