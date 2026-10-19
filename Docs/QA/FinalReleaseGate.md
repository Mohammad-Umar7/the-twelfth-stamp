# The Twelfth Stamp: final release gate

Audited 18 Oct 2026, from 06:40, and updated for the **release candidate, commit 86469f2**. It was built at 07:28 from
exactly the tree that commit records: the working-tree fingerprint was the same before the build and after every test.
It is installed in `C:\Users\awaiz\Games\TheTwelfthStamp` and packaged as
`C:\Dev\12th-Stamp-builds\Release\TheTwelfthStamp_v0.9.0_Windows.zip` (132.6 MB, SHA-256
`89EA57DD98C6A2128B2FE794C6BD7CA181BC710D2682CC7A62E371E5B6003CEB`). Unless noted, report paths are under
`C:\Dev\12th-Stamp-builds\QA-archive`; the release candidate's runs are `rc_1018_0728_*`.

"Automated" means the in-build AutoTest, which drives the real game systems in a desktop window. It does not judge feel,
comfort, audio quality or VR rendering.

**Verdict: not release-ready yet.** Gates 8, 9 and 10 need a person (real-input play, the Quest 3 headset, a listening pass),
and gate 3 is partly unverified until the same session. Everything a machine can check passes on the release candidate.

| # | Gate | Status | Evidence |
|---|---|---|---|
| 1 | Project compiles; the build comes from current source and is traceable | **PASS** | Built at 07:28 with 0 errors and 10 warnings: `C:\Dev\12th-Stamp-builds\Logs\build_rc_1018_0728.log`. The tree fingerprint was the same before and after, and that tree is commit 86469f2. The ZIP's 204 files are identical to that build (gate 12). |
| 2 | Desktop and Quest 3 shortcuts: target, start-in, mode, icon, launch | **PASS (desktop) / UNVERIFIED (Quest launch)** | Both .lnk target `C:\Users\awaiz\Games\TheTwelfthStamp\TheTwelfthStamp.exe` with `--mode=desktop` / `--mode=vr`, start in that folder, and use its `TheTwelfthStamp.ico`. The Desktop shortcut was opened through Explorer: the process started from explorer.exe with `--mode=desktop`, the window "The Twelfth Stamp" stayed responsive and closed normally, with no headset needed. The Quest shortcut needs the headset (gate 9). |
| 3 | Menu, Story, Continue, Judges' Demo, Controls, Settings, Pause, Credits, Quit, Results | **PARTIAL** | Automated PASS (`rc_1018_0728_all`, 192/192): main menu, Controls and Back, the Judges' Demo button, pause, Main menu, Retry from checkpoint, demo results and campaign results. Two-launch save test PASS (`rc_1018_0728_persist1` 16/16, `persist2` 17/17): settings survive a restart, and Continue is offered and resumes at the saved Paris checkpoint. **Unverified:** that Settings visibly and audibly change the game, the Credits page, the Quit button, and that control labels match real key presses. These are to be checked with real input in the capture session. |
| 4 | Story: the Marrakech purchase, home, the glasses on, the corrupted reality | **PASS (automated)** | `rc_1018_0728_story` 49/49. Design note: by Awaiz's direction the story opens at home in Abu Dhabi, and the purchase is a playable flashback. You walk the souk, the shop paints itself in, you pay with the festival token, and the memory folds away. Back home he puts the glasses on, and the room turns into the Margin. |
| 5 | All 11 countries are playable, not just title cards | **PASS (automated)** | `rc_1018_0728_all` (the countries flow), `rc_1018_0728_explore` 98/98 (the expanded areas in all 11), `rc_1018_0728_guide` 96/96 (walkthrough, briefing, step banner, marker and map in all 11). |
| 6 | Passport, slit/window/door tears, destinations, borrowed light and sound, sealing, verification, the Passenger | **PASS (automated)** | The demo, Egypt and countries flows in `rc_1018_0728_all` (192/192). The Passenger's threat, repel, pause-freeze and retry checks are included. No human verification yet (gate 8). |
| 7 | Berlin finale and the return home | **PASS (automated)** | `rc_1018_0728_all`: daylight exposes the forged HOME doors, VOID cancels the forged stamp, the near miss is survived, the glasses come off, then the bathroom, the epilogue, campaign complete and results. Saeed's face is lit in the bathroom mirror (luminance 0.243). |
| 8 | Real keyboard/mouse play, including mistakes, restart, save/continue, tutorial skip, fail/retry | **UNVERIFIED** | Only Awaiz's informal play so far (he got stuck in Egypt, which led to the step-by-step guide). Planned: a real-input capture session on the installed build. |
| 9 | Quest 3: stereo, controllers, world-space UI, locomotion, interactions, audio, pause | **UNVERIFIED** | PC side ready: Meta Horizon Link is installed and its service (OVRServer) is running, Meta's `oculus_openxr_64.json` is the active OpenXR runtime (read, not changed), and `OculusMirror.exe` is present. In code, the HUD switches to a world-space canvas in VR (`UI/Hud.cs`), menus are 3D panels (`UI/Ui3D.cs`), and the passport is held in the hand. **Needs Awaiz in the headset.** |
| 10 | Sound and dialogue listened to | **UNVERIFIED** | Measured only (`Promo/Production/QA/game_audio_levels.tsv`): none of the 63 voice, music and ambience files clip; voice sits at -21.5 to -17.5 LUFS, music at -24 LUFS and ambience at -27.9 to -30.4 LUFS. **Needs a human listening pass** on headphones and in the headset. |
| 11 | Cover, logo and icon at startup, in menus, on shortcuts, the executable and packaging | **PASS (desktop)** | By Awaiz's choice (18 Oct), the intro and welcome screen show the approved Bloodied Metallic title logo, byte-identical to its source, and not the cover picture. Automated: "the approved title logo is in the build", "intro shows the approved title logo" and "welcome screen shows the title logo beside the menu card". The icon extracted from the executable is the key art (`Promo/Production/QA/exe_icon_extracted.png`). Both shortcuts use the bundled multi-size `.ico`, and the ZIP carries the same exe and `.ico`. Branding in VR is part of gate 9. |
| 12 | The packaged build runs from a short new path; the archive matches the tested build | **PASS** | The release-candidate ZIP was extracted to `C:\TS_zipcheck`: all 204 files are byte-identical to `Builds\Windows`. That copy ran `--autotest=demo` 34/34 with 0 errors. The Desktop shortcut was relaunched through Explorer on the new install and works. |
| 13 | Title, credits, licences, AI disclosure, team, no false claims | **FIXED** | `Docs/Credits_and_AI_Disclosure.md` was wrong in three places, now fixed. It now lists the team as the game credits it, discloses the ChatGPT-made key art, logo and icons that ship in the game, and no longer says there is no insignia (the key art shows a stylised emblem). The in-game credits line is corrected in `UI/CreditsText.cs` and ships in this build. No sponsor, award or standalone-Quest claims were found. Whether a stylised national emblem on the key art is acceptable under the jam rules is Awaiz's call. |

## Fixed in the release candidate

These four UI overlaps are fixed (a separate session; layout probe at four resolutions, 61/61):

- the arrival briefing no longer covers long country titles;
- the step banner moves off the open passport;
- the VR trigger callout sits clear of the controller model;
- full-map headers no longer stack, and the minimap label is a single line.

Still open, cosmetic: at 2560x1080 the minimap's "M map" hint touches the top of the HUD passport card.

## Needs Awaiz

1. **Quest 3 session (about 15 minutes).** Link connected, headset on. The capture plan and steps are given in chat.
2. **Listening pass (about 10 minutes)** on headphones: the menu, the story opening, one country and the ending.
3. Optional: **two minutes of desktop play of the Judges' Demo**, so the uncut gameplay clip is human play rather than scripted input.
