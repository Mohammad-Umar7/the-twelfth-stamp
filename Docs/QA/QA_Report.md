# The Twelfth Stamp: QA report

Build 0.9.0, Windows x64 (Unity 6000.6.2f1, URP, Mono), tested on the development PC: Intel Core Ultra 9 275HX, NVIDIA GeForce RTX 5080 Laptop GPU, Windows 11. Last updated 18 Oct 2026.

Every claim below names its evidence. "Automated" means the in-build AutoTest drove the real game systems (the same calls the input layer makes) in a desktop window. It does not judge feel, comfort, audio quality or VR rendering.

## Evidence by category

| Category | Status | Evidence |
|---|---|---|
| Compiles and builds | Yes: 0 errors, 10 warnings (final combined build, 18 Oct 03:25) | `C:\Dev\12th-Stamp-builds\Logs\build_final_1018_0325.log` |
| Unit tests (EditMode) | 19 / 19 passed | Unity Test Runner, batch mode: `Logs\editmode_results.xml` |
| Desktop, automated full playthrough | See "Latest automated run" below | `Docs/QA/run8/autotest_all.md` + screenshots. Earlier runs: `C:\Dev\12th-Stamp-builds\QA-archive` |
| Desktop, played by a person | **Not done** | n/a |
| XR device simulator | **Not used** | n/a |
| Quest 3 headset, played by a person | **Not done. Needs Awaiz** (checklist below) | n/a |
| Audio listened to by a person | **Not done** | Loudness and pronunciation were measured: `Audio/AudioManifest.md` |

## Latest automated run

`TheTwelfthStamp.exe --mode=desktop --autotest=all` (one process, real build, about 6 minutes). Reports and screenshots are in `C:\Dev\12th-Stamp-builds\QA-archive`, newest first below. `Docs/QA/run8` keeps eight key screenshots from an earlier run in git.

| Run | Result | Notes |
|---|---|---|
| fit2, `--autotest=guide` at 1600 x 900 and 2560 x 1080, `--autotest=all` at 1600 x 900 | guide **101 passed, 0 failed** at both sizes; all **192 passed, 0 failed**; 0 errors or exceptions logged | Fixes the 2560 x 1080 item that overlapfix left, plus the guide marker over the Tab panel (see "Known issues and gaps"). Built in a private copy: the repo as of 07:38 on 18 Oct plus `Guide/MapView.cs`, `Guide/Guide.cs`, `UI/Hud.cs` (a read-only `PassportCard` accessor) and `Core/AutoTestGuide.cs`. AutoTestGuide adds five checks, so guide goes from 96 to 101. At 2560 x 1080 the minimap, its label and its hint end 12 px above the passport card (g_passport_card) and 11 px above a tear's taller card (g_tear_card). With Tab open, a marker held at the left edge sits beside the objectives panel (g_marker_tab). The same checks passed at 1920 x 1200 and 1280 x 1024 in fit1. That run's window was hidden, so its screenshots are black and its `all` failures don't count. `...\fit2_guide_1600x900`, `...\fit2_guide_2560x1080`, `...\fit2_all_1600x900` |
| overlapfix, `--autotest=all`, `guide` and `models` | all **191 passed, 0 failed**; guide **95 passed, 0 failed**; models **83 passed, 0 failed**; 0 errors or exceptions logged | The UI overlaps found in the final_1018_0325 shots, fixed (see "Known issues and gaps"). Built in a private copy, `C:\Dev\12th-Stamp-overlapbuild`: the repo as of 06:42 on 18 Oct plus `UI/Hud.cs`, `Guide/Guide.cs`, `Guide/MapView.cs`, `Passport/PassportController.cs` and `Player/ControllerModel.cs`. Window 1600 x 900. In the fresh shots the title card sits clear of the briefing (30_newyork, 39_london, 50_berlin, g_Egypt_briefing, g_Paris_briefing). The step banner is at the top left while the passport is open (10c_egypt_mummy_close, L4_chase), and both trigger callouts sit beside the controller (m10_controller_left, m10_controller_right). The full map's header and the minimap's place label fit (g_*_map, 30_newyork). A probe build, which adds one private test that is not in the repo, measured these layouts in screen pixels at 1600 x 900, 1280 x 1024, 1920 x 1200 and 2560 x 1080: 61 of 61 checks passed at each size. The title clears the briefing by 32 to 91 px. The banner never meets the projected passport or the Tab panel. `...\overlapfix_all`, `...\overlapfix_guide`, `...\overlapfix_models`, `...\overlapfix_probe_*` |
| final_1018_0325, `--autotest=all` and `--autotest=models` | all **190 passed, 0 failed**; models **83 passed, 0 failed**; 0 errors or exceptions logged | The combined build: one build of the main project at 03:25 on 18 Oct, with every session's work in the working tree. That is the polish session's commit 1b66fca plus the uncommitted guide, minimap and Saeed model. Window 1600 x 900: frame time median 1.3 ms, p95 2.0 ms, worst 278 ms (desktop window, includes loading spikes; **not a VR measurement**). This build is installed in `C:\Users\awaiz\Games\TheTwelfthStamp`, which both desktop shortcuts launch, and packaged as the Release ZIP. All 100 screenshots were reviewed: everything renders, including Saeed in the bathroom mirror (20b, 20c), the first-person arms (02b–02d) and Saeed's model (m29–m31). The overlaps found are listed under "Known issues and gaps". `...\final_1018_0325_all`, `...\final_1018_0325_models` |
| guide4, `--autotest=guide` | **87 passed, 0 failed**, 0 errors or exceptions logged | The maps now show the polish session's postcards and lamps. Each uncollected postcard is a small card icon. Lamps are dim dots that become a warm glow once lit; the Passenger won't enter a lit lamp's light. The minimap shows this place's count ("postcards 0/3"), and the full map shows here and in all ("0 / 3, all 0 / 33"). The new checks pass in the three places that have postcards so far (the Margin, Egypt, Marrakech). `...\guide4` |
| guide3, `--autotest=guide`; guide2, `--autotest=all` | guide **84 passed, 0 failed**; all **189 passed, 0 failed**; 0 errors or exceptions logged | The new guidance (see "Knowing what to do: steps, markers and maps"). The guide pass visits all eleven places. In each it checks the walkthrough, the arrival briefing, the step banner, the marker on the current step's target and a minimap of the play area, and it opens the full map in five of them. It then plays Egypt through: a reflector angle puts the beam on the false shadow, the guide moves on to the trace, verifying moves it on to the stamp, and the marker sits on the stamp. `all` ran on the same build (the repo as of 01:41 with my files overlaid), so the read-only getters, the prompt fix and the Map key broke nothing. `...\guide3`, `...\guide2all` |
| saeed5, `--autotest=models` | **83 passed, 0 failed**, 0 errors or exceptions logged | Saeed, the player (`Saeed.fbx`, built by `Tools/blender/build_saeed.py` from the user's turnaround): the repo with the model, its textures in `Resources/Art/Saeed` and `Models.SaeedSlots()`. The checks: the file loads; the armature has the 19 bones the mirror drives; one skinned mesh; six slots on the baked textures, untiled; 33,270 triangles; feet on the origin and 1.83 m to the top of the hair; the face toward +Z; turning the Head bone 35 degrees moves the nose 7 cm and leaves the feet. Shots `m29_saeed_front`, `m30_saeed_side` and `m31_saeed_face` (Egypt's daylight, a few steps ahead of the arrival point) were reviewed. This build rebakes the skin to the first-person hands' colour (`HandMesh.SkinColor`, sRGB 0.70/0.52/0.40) with lighter ambient occlusion, so the mirror and the hands agree: in `saeed_skin.png` the hands, forehead and cheeks now read sRGB 175/130/100, 173/129/99 and 174/124/97 (the hands' colour is 178/133/102), and the whole texture's median (with the darker scalp and stubble) rose from 104/73/53 to 141/101/81. It keeps the polish session's notes on the turnaround from saeed4: the pouch rests slightly behind the left hip, both slider buckles are on the chest run and the placket is buttoned. saeed1 to saeed4 were the runs before it: in saeed1 the triangle count read the mesh's indices, which a player build does not allow (0 triangles, 1 error), and the court spot put the cameras inside a wall; saeed2 framed him from the arrival point and counted from the index counts; saeed3 checked a lighter skin tone; saeed4 added the turnaround notes on the darker skin. `...\QA-archive\saeed5` |
| shift6, `--autotest=shift` | **35 passed, 0 failed**, 0 errors or exceptions logged | The audio helper's shift clips are in. The run used shift_rumble, shift_reverse_swell, shift_whoosh, shift_tear and shift_arrive, each lined up by its peak in the audio manifest (`AudioDirector.PlayToPeak`, started on the shift's own clock). Play() now draws from a pool of 48 sources, so a skip only fades a cue that is still playing its own clip. Levels follow the audio helper's suggestion: about 0.85, and 0.5 on Gentle. No missing clips logged. `...\shift6` |
| shift5, `--autotest=shift` and `--autotest=all` | shift **35 passed, 0 failed**; all **172 passed, 0 failed**; 0 errors or exceptions logged | The more dramatic dimension shift (see "The glasses moment and the Between"), on the repo as of 22:40 with my files overlaid. The shift pass now frames 17 moments, adding the props tearing loose, the inhale, the Between and the Margin assembling, a 1.8 s tear-travel shift into Egypt with Egypt assembling, and a Gentle shift. In it 16 props (20 pieces) tore loose, 22 sheets peeled, 7 cracks let the Between's light in, the inhale took the room to 6% of its light and 26 pieces of debris settled in the Between. Every prop was back in place afterwards. The audio helper's shift clips were not in the repo yet, so the older clips stood in. `all` covers the polish session's stamp and tear travel, which use the same shift. shift3 and shift4 were the review passes before it. In shift3 the frames were too subtle: the cracks' light read as more red, and most of the action was behind the camera. shift4 aimed the action into view and strengthened the light. Frame timing in shift5: median 0.8 ms, p95 1.3 ms on desktop. `...\shift5`, `...\shift5all` |
| walk7, `--autotest=models` | **75 passed, 0 failed**, 0 errors or exceptions logged | Japan's outer precinct gate is now a real passage, 3 m wide with 3.4 m headroom, on the pagoda's sight line where the street's new lane arrives: white plaster framed by vermilion posts and beams, on stone plinths. Japan's own walk check (capsule casts along the route) now finds it clear, so Japan opens itself and its stamp spot is offered; Riyadh's route is open too. Precinct walls and the gate sit off the Floor layer, so VR cannot teleport onto them. New shot `m20b_japan_gate`. `...\QA-archive\walk6`, `...\walk7` |
| walk5, `--autotest=models` | **75 passed, 0 failed**, 0 errors or exceptions logged | The repo as of 20:50, with the polish session's opened routes (Paris and Agra call `Landmarks.MarkReachable` themselves) and the dunes it added in Egypt. The stamp-spot check now accepts countries that open themselves: the spot is offered where a route exists, stays off elsewhere, and MarkReachable turns it off and on in all seven. Egypt is sampled every 25 cm: 0 gaps, largest rise 18 cm (a dune near the court; the player steps 30 cm). New shot `m25b_agra_tank` shows the tank's steps and the paths round it. Walk4, the same repo with a 1 m sampling version of the Egypt check, also ran `shift` (21 / 21) and `all` (168 / 171). The three `all` failures are the polish session's Paris sudden-death checks: in that run the Passenger never reached the player at all. `...\QA-archive\walk4`, `...\walk5` |
| walk3, `--autotest=models` | **68 passed, 0 failed**, 0 errors or exceptions logged | Adds the first opened country. Egypt has solid ground every metre along the 117 m from the court's sand floor to the stamp spot by the pyramids: 0 gaps, the largest step is the 6 cm edge of the court floor, and it is sand underfoot. The vista ground now carries footstep surfaces (sand in Egypt, stone elsewhere). The same build passed `--autotest=shift` 21 / 21. Its `all` run scored 161 / 165, and none of the four failures is in this work. One is the souk check above. The other three are the polish session's sudden-death checks in Paris: the log shows the Passenger does catch the player, but after the test's 4 s window. Run alone, `--autotest=lifecycle` failed the same way. Those checks passed in walk1 with the same Paris and Passenger code, so the timing is fragile; reported to the polish session. `...\QA-archive\walk3` |
| walk2, `--autotest=models` | **66 passed, 0 failed**, 0 errors or exceptions logged | The models gallery plus two new checks for each of the seven landmarks: the ground under its stamp spot is solid, and the spot is offered only once the cell opens its play area (`Landmarks.TryStampSpot` / `MarkReachable`). Private copy `%LOCALAPPDATA%\TheTwelfthStamp\ShiftCheck` (repo as of 19:50, plus these files). `...\QA-archive\walk2\models` |
| walk1, `--autotest=all` | **164 passed, 1 failed**, 0 errors or exceptions logged | The full game with the new glasses moment and the walkable landmark ground. "Glasses put on" passed after 2.4 s and "the Margin copy in place" after 9.7 s (the Between runs itself in test mode). The one failure is the polish session's new souk check ("the memory starts on solid ground at the far end of the souk"), added while its helper is still building that floor. `...\walk1\all` |
| shift2, `--autotest=shift` and `--autotest=story` | shift **21 passed, 0 failed**; story **34 passed, 0 failed**; 0 errors | The art-review pass of the glasses moment (see "The glasses moment and the Between"), with its 11 screenshots reviewed, and the story flow through it. In walk1 the shift pass scored 20 / 21: its one failure was the polish session's intro-cover check, which passed in walk1's `all` on the same build (a timing flake in that check). `...\shift2` |
| vista4, `--autotest=all` | **162 passed, 0 failed**, 0 errors or exceptions logged | The landmarks in their new surroundings (see "Landmarks and their surroundings"), with the rest of the game as it stood in the repo at 19:31 (it now has 162 checks, including the polish session's lifecycle flow). Built in a private copy of the project, `%LOCALAPPDATA%\TheTwelfthStamp\ModelsCheck`. Window 1600 x 900: frame time median 1.0 ms, p95 1.6 ms, worst 147 ms. Desktop window only, **not a VR measurement**. `C:\Dev\12th-Stamp-builds\QA-archive\vista4\all` |
| vista4, `--autotest=models` | **52 passed, 0 failed**, 0 errors or exceptions logged | The models gallery, now with a check that each of the seven landmarks stands in its vista. Same build. `...\vista4\models` |
| vista1 to vista3 | Models 52 / 52 each time. `all`: 160 / 162, then 150 / 162 and 159 / 162 | Earlier rounds of the same pass. Every failure was in the polish session's lifecycle checks (sudden death, retry, pause, recall, stalking) while it was still writing them. vista2 also stalled 75 minutes on one wait. vista4 passes them all. |
| checkpoint2_1639, `--autotest=all` | **108 passed, 0 failed**, 0 errors or exceptions logged | After the story rework (it now has 108 checks) and with all the Blender models in. `C:\Dev\12th-Stamp-builds\QA-archive\checkpoint2_1639\all` |
| checkpoint2_1639, `--autotest=models` | **44 passed, 0 failed**, 0 errors or exceptions logged | Art-review gallery of the Blender models (see "Blender models in the game"). Same build. `...\checkpoint2_1639\models` |
| run8 (final) | **109 passed, 0 failed**, 0 errors or exceptions logged | New HUD and menus, modelled glasses, case and Passenger. Ultra preset, window 1600 x 900. Frame time median 1.1 ms, p95 1.7 ms, worst 45 ms. Desktop window only, **not a VR measurement**. |
| run5 | 108 passed, 0 failed | Before the UI redesign. Window 2560 x 1494: median 1.7 ms, p95 2.5 ms. |
| Final build, Egypt only | 7 passed, 0 failed | After the last mummy-wrap tweak. |
| Release ZIP, judges' demo | 18 passed, 0 failed | Unzipped to a fresh folder and run from there. |

What the run covers:

- **Menu:** the menu cell loads and the main menu opens.
- **Judges' demo (Paris):** the Passenger guards the desk and blocks the trace. Open the passport, select Egypt, target the desk seam, open a slit (Egyptian sunlight flows through), widen to a window, sunlight drives the Passenger off the desk, a hand reaches through, seal the tear, verify France, the counterfeit HOME stamp is revealed, demo results appear, and the story save is untouched.
- **Egypt:** reflector puzzle, trace revealed, seal verified, sunlight unlocked.
- **Story:** shop prologue (shopkeeper, payment, glasses into the case), travel home, unpack, voicemail, open the case, wear the glasses, the apartment turns into the Margin copy, first tear, genuine note revealed, UAE verified, tutorial tear sealed, first threat, doorway escape to Egypt.
- **Countries:**
  - USA: breaker, double-arrival screen, noisemaker lure, gate clears.
  - Japan: lantern on the correct hook.
  - Saudi Arabia: optical panel aligned, honest arch.
  - India: lamp on the correct pedestal.
  - Brazil: three stamps on the drum beat.
  - UK: tear opened under the chime.
  - Morocco: counter rebuilt, calibration insert, optician's notes.
  - Each country ends with its seal verified.
- **Finale:** Berlin plinth accepts eleven seals, the mimic appears, noisemaker, sunlight exposes the forged doors, VOID stamp, the resource tear is sealed, the true return opens on the blank wall, near miss, virtual glasses removal, real home, glasses broken, bathroom, epilogue, results.
- **Art review screenshots** (not pass/fail): close-ups of the Passenger in Egypt (the mummy rising behind you), New York and Berlin.

**VR start-up without a headset** (run by hand): `--mode=vr` with no headset connected. The OpenXR loader did not start, and the game logged its plain-language fallback ("The OpenXR runtime did not start. Open Meta Horizon Link…, then choose Retry"). The Retry / Desktop Mode / Quit menu comes from the same code path. Its on-screen appearance was not captured.

Not covered by the automated run: failing and retrying from a checkpoint, pausing, settings changes, saving and continuing across a restart, and the Passenger draining the player in Marrakech (switched off for that puzzle check).

## Blender models in the game

Every FBX in `Game/Assets/Resources/Models` (built by the scripts in `Tools/blender`, described in `Art/Models/MODELS.md`) is now used, except `Passport_Open.fbx`. Each one replaces only a procedural mesh. Gameplay colliders, sockets, interactables and anchors stay on the procedural objects, and the procedural mesh comes back if a model file is missing (`Models.Spawn` in `Game/Assets/Scripts/World/Models.cs`).

| Where | Models |
|---|---|
| Hand props | glasses (open, folded), glasses case, closed passport, vintage camera, phone, festival token |
| VR | left and right controllers; the onboarding callouts and highlights attach to the model's buttons |
| Apartment (both copies) | sofa, dining table, four chairs, kitchen run, fridge, bathroom vanity and mirror, front door, open suitcase |
| Countries | Paris festival desk and its three street lamps, Egypt reflector, New York noisemaker (also the one carried to Berlin), Japan hand lantern, Riyadh optical panel, Agra garden lamp, Berlin VOID stamp |
| Landmarks | lattice tower, statue, pagoda, arch tower, domed pavilion, clock tower, column gate. Each has its own light and haze and stands in surroundings built for its cell (see "Landmarks and their surroundings"). |
| Passenger | body parts (since run8) |
| Player | Saeed (`Saeed.fbx`): the rigged body for the bathroom mirror and the ending, 53 bones with Unity names, one skinned mesh on six baked textures (`Models.SaeedSlots()`); see `Art/Models/MODELS.md` |

Evidence: `--autotest=models` (52 checks in vista4) confirms each expected model in its cell, all six controls on both controllers with their callouts, the reflector's mirror turning with the puzzle, world-scale tiling on the kitchen, and each landmark standing in its vista. Its screenshots were reviewed one by one. Landmarks are shot twice from their own ground: once as the game shows them, and once with their haze switched off. `--autotest=all` passed 108/108 with 0 errors on the build that first wired the models in (checkpoint2_1639), and 162/162 in vista4. The opening shot (`20_home_bag`), the Paris demo (`02_paris_arrival`) and the real apartment seen through the home tear (`53_home_tear`) all show the models.

Where a model did not fit a fixed gameplay position, the visual was adapted, not the gameplay:

- **Suitcase:** the story rests the camera, passport and case at 0.2 m, on the bag's collider (also the "Go through your bag" target). The model's open shell is 13 cm deep, so it is packed with folded clothes up to that height.
- **Paris desk:** the model is a 0.76 m desk, but the checkpoint's colliders and the ledger trace sit at 1.01 m. The desk body is stretched to that height. The ledger, ink pad, stamp and lamp go where the procedural ones were, and the lamp light is unchanged.
- **Dining table:** raised 1.5 cm to the procedural top, where the phone and the table spots rest.
- **Egypt reflector:** scaled 4 % so its mirror centre meets the puzzle pivot. Its yoke and mirror turn with the pivot; the base stays still.
- **Riyadh optical panel:** the model turns about its vertical axis, but the puzzle spins the pattern about the disc's axis. The lens stays still and the calibration pattern turns just in front of the glass.
- **Fridge notes:** the French-door model has its handles on the centre seam, which hid the genuine UAE note (found in the gallery screenshot). With the model, the four notes sit on the left door, the genuine one directly under the UAE trace, which has not moved.
- **Not used:** the open passport in hand stays procedural, because its seal, portrait and text overlays are flat quads that would not line up with the model's angled, curled pages. The bathroom door stays procedural, because its leaf and collider are 1.9 m wide and it closes in the ending, while `Door.fbx` is a 0.9 m door; that model is the front door instead.
- **Through a tear:** a model that mixes glass with opaque parts now hides only its glass when its cell is seen through a tear. Before, any glass hid the whole object.
- **Texture scale:** an import rule (`TwelfthStampModelRules` in `ProjectSetup.cs`) records each mesh's UV density, so big meshes keep about one texture repeat per metre (the kitchen tiles at 8x instead of stretching one tile over 4 m).

### Landmarks and their surroundings

Before this pass, the landmarks read as flat, fog-coloured cut-outs hanging in the haze. The backdrop shader's fog hid all of anything 60 to 230 m away, and nothing stood under them. Each landmark now stands in a vista built for its cell's view (`Game/Assets/Scripts/World/Vista.cs`, used by `Landmarks.cs`):

- **Light and haze.** Each landmark material is a copy of the cell's backdrop material with its own light, and a gentler haze that grows with distance (1 - e^(-rate * d)). At the landmark's distance the haze replaces 22 % (the Berlin gate) to 42 % (the Riyadh tower) of its colour. Night landmarks are floodlit from below:
  - the lattice tower in gold;
  - the statue's copper in green, with a glowing torch;
  - the pagoda's walls warm against dark tiled roofs;
  - Westminster's stone in gold, its four clock faces lit, with hands at one minute to midnight.

  Day and dusk landmarks take the cell's sun.
- **Around each landmark** (cell coordinates, outside the playable area):
  - **Paris:** the Seine runs on north between stone quays, whose top meets the cell's footbridge at x 54. Along it: Haussmann blocks with lit windows and zinc roofs, plane trees, quay lamps reflected in the river, the Pont d'Iéna, and the Champ de Mars with a pool of light under the tower.
  - **New York:** Liberty Island in the harbour, with a seawall, lamps, lawns, trees and two low buildings, plus Manhattan's lit waterfront under the skyline. The statue now faces the player (the old spawn turned it 180 degrees, away from the player).
  - **Japan:** a temple precinct laid out on the pagoda's sight line: a lantern-lined approach, red maples, cedars, white walls, a one-storey outer gate and the main hall with lit shoji. It follows the pagoda, which the polish session moved to x -10 so its upper tiers show above the street's end gate. A two-storey gate on that line hid the pagoda completely, so the gate is one storey.
  - **Riyadh:** a city grid at dusk: avenues and streets with sodium lamps and median palms, low blocks with lit windows, glass mid-rise towers round the landmark, and haze at street level.
  - **Agra:** the cell's garden carries on to the tomb. The channel and marble walks run through a raised tank, with cypress rows like the cell's own, broadleaf trees, red sandstone walls with domed corner towers, and red-and-white side halls. A great gate stands behind the player, and dawn mist lies at the platform.
  - **London:** the Thames runs on past the cell's water. On the far embankment are lamps, plane trees and the floodlit Gothic palace: its river front, pavilions and pinnacles, plus the central and Victoria towers. The palace stops short of the sight line, and a low wing joins the clock tower so its faces stay clear. Lit buildings stand beyond, and Westminster Bridge has green arches and two rows of lamps.
  - **Berlin:** the blocks along Pariser Platz and the two houses flanking the gate. The road runs on through the Tiergarten to a victory column, seen through the central passage, with city blocks fading in the haze beyond.
  - **Egypt** (procedural pyramids): desert past the cell's sand floor, three queens' pyramids, a field of mastaba tombs, low dunes and heat haze. The third pyramid moved inside the 320 m sky dome; it had stood 323 m out.
- **Skylines** (`Landmarks.Skyline`, seen from New York, Riyadh, Rio, the menu and the apartment window) now have facades of windows, with lit windows at night, setbacks, spires and red beacons, under the same haze. Towers that would land on a landmark's site are left out. Berlin's skyline call was removed by the polish session, because the vista builds that city.
- **Lights and mist.** Lamps are soft additive halos with reflections on the water and pools of light on the ground. Mist banks sit at the landmarks' feet. Window textures are drawn by code at start-up with fixed seeds.
- **Cost.** Everything is merged into one mesh per material, with no colliders and no shadows. Per cell that is 21 (New York) to 118 (Riyadh) scenery meshes. vista4's frame time, a 1.0 ms median and 1.6 ms p95 in a desktop window, matches run8's (1.1 ms and 1.7 ms).

Reviewed in vista4:
- the seven landmark shots, with and without haze;
- the arrival views of every country with a landmark (`02`, `10`, `30`, `32`, `34`, `36`, `39`, `50`);
- the menu, Rio and apartment skylines (`01`, `38`, `m04`).

Limits:
- The surroundings are scenery only: no colliders, so the player cannot walk to a landmark.
- Seen through a tear, they take the cell's ordinary heavy pocket fog.
- None of this has been seen in a headset.

Not verified: how the controller models sit in the hand in a headset. They follow MODELS.md: origin at the grip, no extra rotation under the grip pose.

## The glasses moment and the Between

Putting the glasses on used to fade straight into the corrupted apartment. Now (`Story/HomecomingChapter.cs`, `Story/DimensionShift.cs`, `Story/BetweenRun.cs`, `World/StampPortal.cs`):

1. **The glasses go on.** They rise from the open case to the eyes as an object and turn to face the way the player looks. They settle with a click, the rims and bridge framing the view for a moment on desktop; in VR they stop a hand's width from the face.
2. **Subtraction, then the shift.** The fridge stops, the voicemail starts repeating, and the room comes apart in five beats. The same shift carries the player on every jump between places (stamps and tear travel too), with the destination's own light.
   - *Rumble.* The light sinks into a slow red pulse, the things around the player rattle where they stand and the first crack splits a wall.
   - *Tearing loose.* Up to 16 props and pieces of furniture near the player, most of them in view, lift off and circle him, faster and faster. They are stand-in copies: the real ones are hidden and come back at the swap. Debris tears up from the floor with ember-lit edges, and the wall surface peels away in large sheets that turn over into passport pages and postcards and spiral up. Cracks split the walls one after another. Through every crack and bared patch shows the light of where he is going (here the Between's fire; Egypt's sun, Paris's rain-light and so on for the countries), in shafts that pool on the floor. On desktop a streak tunnel in that light opens for the last 1.5 s.
   - *Inhale.* For 0.4 s everything slows and hangs in the air, the room's light drops to 6% and the sound drains away. Only the cracks blaze.
   - *Slam.* The cracks burst as one dark fade closes. Behind it the colour crosses from blood red to a dark shade of the destination's light.
   - *Arrival.* The new place assembles: the debris that came through falls and sinks into its floor, dust drifts down through its light, a ring of that light runs out across the ground, and its lamps come up over about a second.
3. **The Between.** A stone causeway with glowing edges hangs over a red abyss. Pieces of the apartment drift past, and the landmarks of the countries ahead (the lattice tower, the statue, the pagoda, the arch tower, the domed pavilion, the clock tower, the column gate) hang huge and tilted against a red horizon. Running footsteps sound behind, and the runner's shadow slides past before the runner itself (a smoked-glass body) sprints out of a red glow. The player runs, grabs three pieces of the home stamp, and the seal at the far end opens.
4. **Caught** means thrown back to where you landed, keeping the pieces collected. After two catches the runner slows. This runner is not the Passenger: there is no drain and no death here.
5. **The seal** shifts the player into the Margin apartment, standing where they put the glasses on. The UAE tutorial runs as before; its first threat stays a slow stalk (`AllowChase = false`).

Comfort rules from the brief, as built:
- The camera and rig are never moved, shaken or turned. The player sees the runner by turning, or by its shadow, and the only forced move is the fade-through when caught.
- The red pulses at 0.7 Hz and there are no flashes: the inhale is one 0.4 s dim, the slam one fade, the arrival one rise.
- Props, debris and pages stay out of arm's reach (props circle 1.4 m or more out), circle both ways round so the view never swirls as one, and move at 60% speed in VR. Sheets that pass close to the eye fade out.
- The streak tunnel lasts 1.5 s at most, is world-fixed, keeps the room visible through it, and is off in VR, with Reduced effects and on Gentle. Reduced effects also drop the rattle and thin everything out.
- Gentle halves the build-up, tears loose under half as much (no whispers, no tunnel), shortens and softens the inhale and the grade, and keeps the runner below walking pace, so it never catches anyone who walks.
- Once a full shift has been seen, the Skip input jumps straight through.
- If the sequence is abandoned (quit to the menu), a guard puts the lights, grade and fade back and removes everything it built.

For the polish session's stamp flow, `DimensionShift.Run(swap, buildUp, into, glimpse)` is the travel transition. `into` (the destination's `CellId`) picks the light that shows through the cracks (`DimensionShift.GlimpseOf`), or `glimpse` sets it. The inhale, the slam and the arrival add about 2.3 s to the build-up; the swap comes about 0.95 s after it. The audio helper's shift clips (`Tools/audio/sfx_shift.py`) are lined up by the peaks in the audio manifest (`AudioDirector.PlayToPeak`, started on the shift's own clock so a pause holds them back): the rumble cuts out as the inhale begins, and the reversed swell and the whoosh land on the slam. Stamp and tear travel pass the destination, so the cracks show each country's light. `StampPortal.Spawn(parent, worldPos, country, onCollected, portal)` places a collectible stamp (portals can wait, dimmed, until `Ready`). `Landmarks.TryStampSpot(cell, out pos)` gives a spot near each landmark on ground that now has colliders, but only after the cell calls `Landmarks.MarkReachable(cell)`.

Evidence: `--autotest=shift` covers the glasses rising and worn, the shift tearing loose, building, at its peak and in the inhale, the Between assembling, from the start and from the side, the runner behind and closing in, a stamp piece, the open seal, the Margin assembling and after, a 1.8 s tear-travel shift into Egypt and Egypt assembling, and a Gentle shift. It checks that props tear loose with their originals hidden and all come back, that pages peel and cracks let the destination's light in, that the inhale drains the light before the slam, that debris settles in the new place and nothing is left behind. It also checks that the swap happens at the peak, input unlocks, the runner gains on a standing player, catching throws the player back, the seal stays shut until all three pieces are held, the player ends in the Margin where they stood, a short shift still has every layer, and Gentle does less. `--autotest=story` runs it end to end.

Not verified: any of it in a headset (VR stops the glasses short of the face, has no tunnel and slows the debris, but nobody has seen it), Reduced-effects mode on screen, the Skip path, and how the shift clips sound in the game (shift6 shows they play, lined up by their peaks, but nobody has listened to them yet).

## Knowing what to do: steps, markers and maps

Playtest: in Egypt the player aimed the reflector's beam at the mummy and got stuck. Nothing said the beam belongs on the shadow, or that the trace it reveals is then pressed with the passport. The objectives panel existed but stayed hidden until Tab, and VR had no way to open it at all. Now (`Guide/Guide.cs`, `Guide/GuideBook.cs`, `Guide/MapView.cs`):
- **A walkthrough for every place**: the Margin apartment, Egypt, Paris, New York, Japan, Riyadh, Agra, Rio, London, Marrakech and Berlin. Each lists its steps in order, read live from the encounter's state through one-line read-only getters in the encounter classes.
- **The step banner**, always on screen (bottom left; top right in VR), shows:
  - the place and "step 2 of 5", then what to do, with the right keys;
  - a meter where a step has one: Egypt's burn, Rio's three beats, London's countdown to the chime, Marrakech's three items;
  - a hint after 40 s on the same step;
  - a red line when the Passenger is close or chasing.
- **A marker on the target**: on screen with its name and distance (held at the screen edge when the target is behind you), plus a soft beacon of light in the world, which is what VR uses. Where nothing in the world gives the answer away, the marker shows it: Japan's middle hook, Agra's middle pedestal, Riyadh's honest arch.
- **An arrival briefing** the first time you reach each place: the idea of the place, every step, and the danger. Space (or use) dismisses it.
- **Tab** shows the whole checklist. **M** opens the full map.
- **The minimap** (top right; lower left in VR) is drawn on arrival from the place's walkable floors (paving, sand, tile and wood in their own colours) and its walls, framed on the play area. It turns with your view and shows you, the next objective, the stamp when it is out, the tear seams, the landmark, and the Passenger when it is near. **The full map** (M) shows the same thing north up, with a legend and the checklist.
- **Postcards and lamps** (the polish session's `Postcard` and `SwitchLamp`) show on both maps: a card icon for each postcard not yet found, and a warm glow for each lit lamp, a safe zone. The minimap counts this place's postcards; the full map counts here and in all (out of 33).
- Fixed on the way: cells set custom prompts ("Turn the reflector", "Hang the lantern"), but the rotator's and the socket's `Awake` replaced them with "Turn" and "Place" on first activation. `Interaction/Props.cs` now keeps a prompt that was set.

Evidence: `--autotest=guide` (84 / 84) and `--autotest=all` (189 / 189) on the same build.

Not verified: the guide and minimap in a headset. The VR layout puts the banner at the top right and the minimap at the lower left of the view. VR has no full map, because every controller button is taken.

## Fixed this session (found by the automated run or by reviewing its screenshots)

| Problem | Fix |
|---|---|
| The dimension shift's streak tunnel was built from the world origin rather than the player: a couple of metres off in the apartment, and out of view in the countries, where players stand far from the origin. | It is built round the player, so it opens in front of them everywhere. |
| Berlin: sunlight could never expose the forged doors (the 10 m beam was 10.4 m from the target). Every later finale step failed. | The light seam was moved to face the middle door 6.9 m away. Exposure is now sampled in front of the door at three heights. |
| Berlin: the three "HOME" decoy doors were built facing into the wall, so their glow and label were hidden and mirrored. | Rotated to face the room. |
| After finishing the story or quitting during the prologue, tears and threats stayed disabled for the next run and the judges' demo. | Chapter gates reset at every campaign or demo start and on return to the menu. |
| A finished story was still offered as "Continue". | A completed save no longer counts as continuable. |
| Glossy glass under point lights bloomed into screen-sized white blobs (the Passenger in New York, London and Berlin). | Bloom threshold 1.0 and clamp 8. |
| Standing inside a light shaft (the Marrakech counter lamp, sunlight pouring past you) filled the view with bright panels. Rain streaks smeared across the lens. | Light shafts and particles fade within about 2 m of the camera. |
| HUD: control prompts were drawn across the middle of the screen over the passport, the aim dot sat on the text, the destination label collided with the prompts, and the "verified" tick and tutorial checkboxes were missing from the font (shown as boxes). | HUD redesigned: key-cap prompts beside the reticle, one passport/tear card on the right with a tear gauge and contamination meter, subtitles and toasts sized to their text, a vitality bar that hides when full, and only glyphs the fonts contain. |
| The passport was unreadable in dark scenes. | The pages glow faintly. The passport moved lower left to keep the centre clear. |
| Menus: on desktop the paper card was off to one side and skewed, its title and bottom buttons were clipped or drawn behind the paper, and the disabled "Continue" was unreadable. | Desktop menus are centred and square to the camera. The draw order is fixed, the paper has proper margins, buttons are ink on paper with a hover wash, and toggles show their value on the right. |
| Disguise pieces hung on bones (Egypt's wraps, the Paris umbrella, hair, lens rig, petals) were never removed, so the Passenger wore every previous country's disguise at once. | Pieces are tracked per disguise and removed on every change. Verified in the Berlin close-up. |
| Egypt's mummy read as bronze rings on glass. | Overlapping stained grave linen, a bound head with only the photo oval showing, and long trailing strips. |

## Known issues and gaps

- **VR is unverified.** The VR rig, controller labels, teleport, snap turn, stereo tear rendering and pause on headset removal are compiled but have not been run in a headset.
- At start-up, Unity's OpenXR plugin asks the active runtime (Meta Horizon Link) which GPU drives the headset. With no headset connected, it writes an `XR_ERROR_FORM_FACTOR_UNAVAILABLE` diagnostic report to `Player.log`, even in desktop mode. Desktop play is unaffected.
- New York, London and the corrupted apartment are very dark on a laptop panel. The Brightness setting compensates.
- Some rooms (the shop, the apartment, the Berlin pavilion) are plain box geometry, and the shopkeeper is a simple mannequin.
- 3D models from Blender are used for the hand props, the controllers, the apartment furniture, the country props and the landmarks (see "Blender models in the game"), each only when its file is in `Game/Assets/Resources/Models`. Rooms, streets, buildings, the landmark surroundings, the passport in hand, the bathroom door and the disguises are procedural. The landmark surroundings now have colliders (ground, walls, quays, bridges and the island), so they can be walked once a country's play area is opened to them. That bounds-and-paths work belongs to the polish session, and `Landmarks.TryStampSpot` stays off until then.
- The glasses moment (the dimension shift and the Between) has not been seen in a headset.
- The controller models have not been seen in a headset.
- The hand that reaches through a tear is still the older procedural hand, not the modelled one.
- Saeed's model is checked in a build (`--autotest=models`, saeed5). The bathroom mirror is the polish session's: `MirrorAvatar` dresses him in its own materials on the same `Resources/Art/Saeed` textures, and that session reports its story run at 47/47 with him in the mirror; the mirror is not re-checked here. Its skin material is untinted (`_BaseColor` white) since the saeed5 bake took the hands' colour; the earlier lift of 1.52/1.55/1.6 for the darker saeed4 bake is gone. Not seen in a headset.
- **Fixed in overlapfix (see the run table): guide panels over other UI** (found in the final_1018_0325 shots). On desktop the title card now slides along its line until it is clear of the arrival briefing, the Tab objectives panel, the passport card and the minimap's label. It shrinks only if the space left is too narrow. While the passport is out, the step banner waits at the top left under the toast line, or beside the objectives panel when Tab has it open. The full map now hides the briefing and the title card. Its header no longer runs into the steps, and the minimap's place label stays on one line. Measured at 1600 x 900, 1280 x 1024, 1920 x 1200 and 2560 x 1080. In VR the briefing now waits until the title card has gone, because the VR HUD has no room for both. That has not been seen in a headset.
- **Fixed in overlapfix: the VR trigger callout** ("Use", or "Use / Widen (hold)" on the dominant hand) now sits beside the handle on its outer side, clear of the body (m10_controller_left, m10_controller_right). Not seen in a headset.
- **Fixed in fit2 (see the run table): at 2560 x 1080 the minimap's "M map" hint touched the top edge of the passport card.** Both sit on the right, and a 21:9 canvas is shorter. A tear's card is taller still and also reached the minimap's label. On desktop the minimap, its label and its hint now shrink toward the top-right corner while the card would reach them. At 2560 x 1080 they shrink to about 91% with the passport and about 78% with a tear. They are unchanged at 16:9, 16:10 and 5:4.
- **Fixed in fit2: the guide marker over the Tab panel.** When the target is off screen, the marker waits at the edge with its name on screen, clear of the Tab objectives panel, the step banner, the passport card and the minimap. Before, with Tab open and the target to the left, the marker sat on the panel's text (overlapfix_probe_2560x1080, o_NewYork_passport_tab).
- **Fixed in fit2: the full map's postcard symbol.** The Inter font has no U+25AD, so TMP drew a hollow square and logged a warning every time the map opened. The legend now uses U+25A0.

## Headset checklist (for Awaiz, about 15 minutes)

Run "The Twelfth Stamp - Quest 3" with Link connected and Meta Horizon Link active.

1. Main menu appears in the headset (not only on the PC window). Point and click with either controller.
2. Controls page: each label matches the button you press. Raise each controller to your face: the controller model should sit the right way round in your hand, and its labels should point at the right buttons.
3. Judges' demo start to finish: passport on X, Egypt on B, tear by gripping the seam and pulling (or holding the right trigger), seal on A.
4. Both eyes show the same view through the tear (close one eye at a time).
5. Teleport (left stick) and snap turn (right stick) are comfortable. Try smooth movement in Settings.
6. Take the headset off during play: the game pauses.
7. Pause with the left Menu button. Quit from the pause menu.

Report anything that fails, with what you saw.
