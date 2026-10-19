# Environment Audit — The Twelfth Stamp

Audited 17 Oct 2026, 12:20–12:40 local time (Arabian Standard Time, UTC+04:00). Every entry below was checked on this machine during that window. Unverified items are marked as such.

## Machine
| Item | Verified value |
|---|---|
| OS | Windows 11 Home 10.0.26200 (build 26200) |
| CPU / RAM | Intel Core Ultra 9 275HX (24 cores) / 31.4 GB |
| GPU | NVIDIA GeForce RTX 5080 Laptop GPU (driver 32.0.15.9191), plus Intel Graphics iGPU and the "Meta Virtual Monitor" adapter |
| Display | 2560 × 1600 |
| Free storage | C: 161 GB free |

## Project and repository
| Item | Verified value |
|---|---|
| Repository | `C:\Dev\12th-Stamp` (moved out of OneDrive from `C:\Users\awaiz\OneDrive\Desktop\12th-Stamp` on 2026-10-17), remote `https://github.com/4waiz/12th-Stamp.git`, branch `main`, no commits at audit time |
| Unity project | `Game\` inside the repo (created this session) |
| OneDrive | Desktop is redirected into OneDrive (Known Folder Move), and OneDrive.exe is running. The repo started on the Desktop, so `Game\Library` and `Game\Logs` were made directory junctions to `%LOCALAPPDATA%\TheTwelfthStamp\` to keep sync away from Unity's churn. The repo itself moved out of OneDrive on 2026-10-17. `Game\Logs` became a normal folder again on 2026-10-17, and `Game\Library` on 2026-10-18, after it turned out that the Claude desktop app (an MSIX package) had been redirecting everything its sessions wrote under `%LOCALAPPDATA%\TheTwelfthStamp` into its private storage, so the junctions dangled for Unity Hub and Explorer. With the junction, Unity 6.6 stopped at "unable to open the specified log file" (exit code 127) whenever an `Editor.log` already existed, while launches without one worked. Builds are written to `C:\Dev\12th-Stamp-builds\Builds`, outside source control and outside OneDrive. |
| Previous folder | `C:\Users\awaiz\OneDrive\Desktop\The-12th-Stamp` now contains only an empty `.git` (remote `bilal-feroz/The-12th-Stamp`). Left untouched. |

## Unity
| Item | Verified value |
|---|---|
| Editor | 6000.6.2f1 (revision 770e33f6875c), `C:\Program Files\Unity\Hub\Editor\6000.6.2f1` |
| Build support | Windows Standalone (Mono only: win64/win32/arm64 player variants; no IL2CPP) and WebGL. **No Android module**, so a standalone Quest APK is not buildable without installing it. |
| Licence | Entitlement licence present. Batchmode connects to the licensing client (log: "Successfully updated license"). |
| Editor-bundled package versions used | URP 17.6.0, Input System 1.20.0, OpenXR 1.18.0, XR Management 4.7.0, XR Core Utils 2.6.0, uGUI 2.6.0 (includes TextMeshPro), Test Framework 1.8.0 |
| Also bundled, not used | XR Interaction Toolkit 3.6.0, Meta OpenXR 2.6.1, XR Hands 1.9.0, Oculus XR 4.5.5 (deprecated on this editor) |
| Editor integration | **No Unity MCP or editor GUI automation.** Unity is driven by command-line batchmode (`-executeMethod`, `-runTests`). Builds are launched as normal Windows processes. |

## VR runtime
| Item | Verified value |
|---|---|
| Active OpenXR runtime (HKLM, read only) | `C:\Program Files\Meta Horizon\Support\oculus-runtime\oculus_openxr_64.json` (Meta Horizon Link) |
| Link install | `C:\Program Files\Meta Horizon` and `OVRServer_x64.exe` present. No Oculus/Meta Windows service matched `Get-Service` at audit time. |
| Headset | Not verifiable by me. Physical checks need Awaiz (see QA report). |
| Global changes | None. The active OpenXR runtime is **not** modified by this project. |

## Tools
| Tool | Verified |
|---|---|
| Git / GitHub CLI | git 2.54.0, gh present |
| FFmpeg / ffprobe | 8.1.1 full build (winget) |
| Python | 3.12.10; `uv` at `~\.local\bin\uv` |
| Node | 24.16.0 |
| .NET SDK | 8.0.425 |
| Hyperframes CLI | 0.8.167 (npx cache). This is the documented Kokoro TTS path (`npx hyperframes tts`) used by `/brag --voice`. |
| Blender | 5.2 at `C:\Program Files\Blender Foundation\Blender 5.2\blender.exe` (not on PATH). Two Blender MCP servers are registered but need Blender running with the add-on, so modelling uses Blender's command-line background mode. |
| VS Code | present |

## MCP / browser
- **Built-in browser** (`mcp__Claude_Browser__*`): works. ChatGPT was signed in by Awaiz at about 12:32. Image downloads arrive in `C:\Users\awaiz\Downloads` as complete PNGs with a `.tmp` name, and are renamed into `Art\References`.
- Claude in Chrome and chrome-devtools tools are registered (deferred) but not used.
- No native Windows GUI automation tool is available in this session.

## /brag (skill registry: `C:\Users\awaiz\.claude\skills\brag\SKILL.md`)
- On Opus 5.5, `/brag` without `--full` or `--voice` hands off to `/brag-slim`, which has no voice. `--voice` enables narration through **Kokoro via Hyperframes**, a single provider that runs locally.
- `/brag` is a 15–25 s launch-video workflow, not a game-audio API. Game narration clips are generated with the same underlying pipeline (`npx hyperframes tts`, local Kokoro) as standalone WAV files, then imported into Unity. See `Audio/AudioManifest.md`.
- The bundled music ("Happy Beats / Business Moves", ende.app) is upbeat and corporate, so it is unsuitable for horror and not used. The bundled SFX are Kenney CC0.

## Jam requirements (official FAQ fetched 17 Oct, https://www.shiftgamejam.com/faq.html)
- Dates: 17–18 Oct 2026. **No deadline time is published.** The FAQ says the deadline is "announced during the weekend", and late entries are not judged. → **Blocker: Awaiz to confirm the exact deadline and timezone.**
- Public criteria: five equally weighted criteria (creativity and innovation; interpretation of theme; gameplay and mechanics; art direction; overall presentation). The **on-site rubric supplied in the brief differs** (Gameplay 30, Theme 20, Creativity 15, Art/Audio 15, Technical 10, Presentation 10). The brief's on-site rubric is the working priority. → **Awaiz to confirm the final rubric.**
- AI tools are allowed, but AI-generated content must be listed in the submission notes. Assets must be free, owned, or licensed, with a record of the source. Teams are up to 5 people. The build is uploaded to the itch.io jam page. Partner branding (Abu Dhabi Gaming / Unity) is restricted, so this project uses no partner logos.
- The itch.io jam page could not be reached from this machine (connection reset / HTTP 000), so it is **not verified**.

## Missing capabilities and decisions
1. No Unity editor GUI access. All scenes, materials and settings are generated by editor scripts in batchmode, and the world is assembled from code at runtime.
2. No Android module, so the Quest APK is out of scope (and optional in the brief).
3. XR Interaction Toolkit is installed with the editor but not used. A small custom interaction layer serves desktop and VR from the same game code, so behaviour is deterministic and testable without a headset.
4. Headset comfort, stereo correctness and controller feel can only be confirmed by Awaiz wearing the Quest 3.
