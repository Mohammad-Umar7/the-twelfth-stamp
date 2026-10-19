# The Twelfth Stamp: status and handoff

Last updated 17 Oct 2026. Evidence for every "verified" line is in `Docs/QA/QA_Report.md`.

## Where things are

| What | Where |
|---|---|
| Unity project | `Game/` (Unity 6000.6.2f1, URP). The scene is empty: the game builds its world from code when it starts. |
| Game code | `Game/Assets/Scripts` (Core, Cells, Story, Passport, Passenger, Player, Interaction, Rendering, UI, Audio, Input) |
| Shaders | `Game/Assets/Shaders` (tear stencil, pocket-world lighting, light shafts, particles, sky) |
| Editor and build scripts | `Game/Assets/Editor` (`ProjectSetup.cs`, `BuildScript.cs`) |
| Generated textures, audio, narration | `Game/Assets/Resources` (generators in `Tools/textures`, `Tools/audio`) |
| Windows build | `C:\Dev\12th-Stamp-builds\Builds\Windows\TheTwelfthStamp.exe` (outside the repo and outside OneDrive) |
| Shortcut installer and player README | `Tools/install` (copied next to the .exe by every build) |
| Release ZIP (not uploaded) | `C:\Dev\12th-Stamp-builds\Release\TheTwelfthStamp_v0.9.0_Windows.zip`, 130.2 MB, SHA-256 `4EB8E6D76B1E60AE0698921A2AC9018D6B94B223B6CE61C5F0D8FE1AC143592E` (the combined build of 18 Oct 03:25). Rebuild it with `Tools/install/Package-Release.ps1`. |
| Desktop shortcuts | "The Twelfth Stamp - Quest 3" and "The Twelfth Stamp - Desktop" on your Desktop, pointing at the installed copy in `C:\Users\awaiz\Games\TheTwelfthStamp` (a rebuild doesn't change it until that copy is refreshed) |
| Automated test reports and screenshots | `Docs/QA/QA_Report.md`, final run in `Docs/QA/run8/`, earlier runs in `C:\Dev\12th-Stamp-builds\QA-archive` |
| References, credits, pitch | `Art/References/REFERENCES.md`, `Docs/Credits_and_AI_Disclosure.md`, `Docs/Pitch_and_Demo.md` |

## Build, test, install

All commands run from any folder. Close the Unity editor first if it has the project open.

Keep builds, the installed copy and Unity's `Library` out of `AppData`. The Claude desktop app is an MSIX package, so anything a Claude session (or a Unity or game process it starts) writes under `C:\Users\<you>\AppData` really lands in the app's private storage (`AppData\Local\Packages\Claude_pzs8sxrjxfjjc\LocalCache\...`), where Explorer, Unity Hub and the desktop shortcuts can't see it. That is why builds, the ZIP and test reports live in `C:\Dev\12th-Stamp-builds`, the installed copy in `C:\Users\awaiz\Games\TheTwelfthStamp`, and `Game\Library` is a normal folder.

```bash
"C:\Program Files\Unity\Hub\Editor\6000.6.2f1\Editor\Unity.exe" -batchmode -nographics -quit -projectPath "C:\Dev\12th-Stamp\Game" -executeMethod TwelfthStamp.EditorTools.BuildScript.SetupAndBuild -logFile build.log
```

```bash
"C:\Dev\12th-Stamp-builds\Builds\Windows\TheTwelfthStamp.exe" --mode=desktop --autotest=all --report-dir="C:\Dev\12th-Stamp\Docs\QA\latest" -screen-fullscreen 0 -screen-width 1600 -screen-height 900
```

`--autotest` accepts `demo`, `egypt`, `story`, `countries`, `finale` or `all`. The test drives the real game systems, writes `autotest_<name>.md` plus screenshots, and exits with code 0 only if every check passed. It never writes to your save. `--autotest=models` and `--autotest=shift` are separate art-review passes, not part of `all`: `models` frames every Blender model and landmark, and `shift` frames the glasses moment, the dimension shift and the Between. `--autotest=guide` visits every place and checks its walkthrough (step banner, marker, arrival briefing) and its minimap and full map, and plays Egypt through. It checks that every Blender model is in place and frames each one for a screenshot.

```bash
"C:\Users\awaiz\Games\TheTwelfthStamp\Install Shortcuts.cmd"
```

The installer creates "The Twelfth Stamp - Quest 3" and "The Twelfth Stamp - Desktop" on your Desktop, pointing at the game in that folder.

## Story flow (all eleven countries are playable)

Marrakech shop prologue → Abu Dhabi homecoming → the glasses go on and the room comes apart (dimension shift) → the Between: run from the ghost along a causeway over a red void, grab three pieces of the home stamp, reach the seal → the Margin copy of the apartment (UAE tutorial: first tear, first seal, first threat) → Egypt, France, USA, Japan, Saudi Arabia, India, Brazil, UK, Morocco (any order after the tutorial; Egypt unlocks sunlight, New York the noisemaker, Marrakech the calibration insert) → Berlin finale (validate eleven seals, expose the forged doors with sunlight, stamp VOID, open the true return on the blank wall) → near miss and the virtual glasses removal → real home, break the glasses, bathroom, epilogue → results → credits → menu.

## Needs you (cannot be done or verified by me)

1. **Headset check on the Quest 3** (about 15 minutes). Run "The Twelfth Stamp - Quest 3" with Link connected and Meta Horizon Link as the active OpenXR runtime. Check each item and tell me what failed:
   - Both eyes render the same world, including the view through an open tear (stereo).
   - Controller labels on the in-game Controls page match the buttons you press.
   - Grab (grip), use (trigger), passport (X), destination (B), seal (hold A), recall (Y), pause (Menu).
   - Teleport and snap turn are comfortable. Smooth movement works when chosen.
   - Taking the headset off pauses the game.
   - The judges' demo runs from start to finish.
2. **Deadline and rubric.** The jam FAQ says the deadline is announced during the weekend. Tell me the exact time and timezone, and whether the on-site rubric in the brief is final.
3. **Listening pass.** Nobody has listened to the narration, music or effects on headphones or the Quest speakers yet.
4. **Commit and submission.** The work is not committed yet, and nothing has been pushed or uploaded. Submission to itch.io is yours to approve.

## Known gaps

- VR has been compiled and its start-up fallback path exists, but no person has played it in the headset.
- New York and London are very dark on a laptop screen. Raise Brightness in Settings, or ask me to lift those scenes.
- Blender models (`Game/Assets/Resources/Models`, documented in `Art/Models/MODELS.md`) are used for these:
  - the hand props (glasses, case, closed passport, camera, phone, festival token);
  - the VR controllers;
  - the Passenger's body;
  - the apartment furniture and suitcase;
  - the country props (Paris desk and street lamps, Egypt reflector, noisemaker, hand lantern, optical panel, garden lamp, VOID stamp);
  - the seven landmarks.

  Each one replaces only a procedural mesh: colliders and gameplay positions are unchanged, and a missing file falls back to the procedural version. The full list and the places where a model had to be adapted are in `Docs/QA/QA_Report.md` ("Blender models in the game"). Two stay procedural on purpose. The open passport in hand keeps its procedural pages, because its overlays would not line up with the model's curled pages. The bathroom door is 1.9 m wide and closes in the ending, so the 0.9 m door model is used for the front door instead.
- The landmarks stand in procedural surroundings built for each cell's view (`World/Vista.cs`, called from `World/Landmarks.cs`): ground, buildings with lit windows, trees, lamps, bridges, water and mist, under a gentler haze than the cell's fog. The skylines seen from New York, Riyadh, Rio, the menu and the apartment use the same buildings. Their ground, walls, quays and bridges have colliders. Each country's play area still has to be opened out to its landmark (polish session); `Landmarks.TryStampSpot` offers a stamp spot there only after the cell calls `Landmarks.MarkReachable`.
- The glasses moment (`Story/HomecomingChapter.cs`, `DimensionShift.cs`, `BetweenRun.cs`, `World/StampPortal.cs`) has not been seen in a headset. In VR the glasses stop short of the face, there is no streak tunnel and the debris circles slower. The dimension shift (props tearing loose, pages, the destination's light through the cracks, the inhale, the arrival) runs on every jump between places.
- The controller models have not been seen in a headset yet. Add "do the controllers sit right in your hands?" to the headset check.
- No Android module is installed, so there is no standalone Quest APK. The game runs on Quest 3 as PC VR over Link.
