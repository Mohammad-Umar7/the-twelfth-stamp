# Branding: key art, logo, icons and where they appear

## Sources (approved by Awaiz Ahmed)

| Asset | Original (left untouched in the repo root) | Build copy | SHA-256 |
|---|---|---|---|
| Key art "Portals of Shadow" (1254 × 1254, RGB) | `The Twelfth Stamp_ Portals of Shadow.png` | `Art/Branding/source/KeyArt_PortalsOfShadow.png` | `2994000c527d927b96df31f2a451db633f7a93aa0a1a27a601af06b8cb0da439` |
| Title logo "Bloodied Metallic" (2172 × 724, RGBA) | `The Twelfth Stamp_ Bloodied Metallic Logo.png` | `Art/Branding/source/Logo_BloodiedMetallic.png` | `53d2538e91264cb4a71d695294d1f3f45d74c971b72cbbbf7a1944502f736b7f` |

Both were made with **ChatGPT image generation**, according to Awaiz Ahmed (17 October 2026). The key art shows a passport-style cover with stylised national lettering and an emblem. Both are fictional artwork. They show no real document, they imply no endorsement by any country or organisation, and they contain no real identity information.

## Derivatives (regenerate with `python Tools/branding/make_branding.py`)

The script copies the sources, then writes every derivative and records sizes and hashes in `Art/Branding/branding_manifest.json`:

- **Corner mask:** the key art's rounded corners are fitted as circles (TL 300, TR 302, BR 364, BL 362 px). Only pixels outside those arcs become transparent; the artwork itself is never cropped or recoloured.
- **Game/Assets/Resources/Art/Branding/keyart_cover.png** (1254²): the full cover, used for the intro and the loading frame.
- **keyart_logo.png** (2048 × 683): the logo, used for the end cards.
- **keyart_titlelogo.png** (2172 × 724): a byte-identical copy of the approved "Bloodied Metallic" title logo (SHA-256 `53d2538e…`, the same as the source above), used on the intro and the welcome screen since 18 Oct 2026. It was copied as-is; `make_branding.py` does not produce it yet.
- **keyart_menu.png** (1254 × 915): the cover without its lower title band, used beside the welcome menu.
- **keyart_thumb.png** (256²): a centred crop (box 160, 55, 1050, 945), used for small headers, the loading mark and icons of 96 px and smaller (at those sizes the title is unreadable).
- **Game/Assets/Branding/Icons/icon_{1024,512,256,128,64,48,32,16}.png**: the Player Settings application icons.
- **Tools/install/TheTwelfthStamp.ico**: a real multi-resolution Windows icon (16, 20, 24, 32, 40, 48, 64, 96, 128 and 256 px; the small sizes are DIB, 256 is PNG). The build copies it next to the exe for the shortcuts.
- **Art/Branding/previews/**: contact sheets for review (icon sizes on light and dark, the logo on dark and on paper, the menu art).

## Where it appears

- **Startup:** `UI/Branding.cs` `Intro()`. By Awaiz's choice (18 Oct 2026) the intro shows the title logo only, with no cover picture: it fades in, holds, then fades out (about 3 s). Any key skips it after 0.35 s. In VR it is a head-locked card at 2.4 m with post-processing off. `--no-intro` skips it.
- **Welcome / main menu:** `Branding.ShowWelcome`. The title logo sits beside the menu card with the tagline under it; the cover art is no longer shown here (18 Oct 2026). VR uses world-space quads.
- **The cover art** still appears as the executable and shortcut icon and in the promotional materials.
- **Menu headers (pause, controls, settings, credits, results):** a small thumbnail icon above the title (`Panel3D` header icon).
- **Loading:** a small pulsing thumbnail mark, bottom-right, appears only while the screen is fully faded to black on desktop and outside menus (`Rendering/Fader.cs`).
- **End cards:** the logo and the tagline "You travelled alone. Something came back with you." after the judges' demo and the epilogue.
- **Executable icon:** `Editor/BuildScript.cs` `ApplyIcon()` sets the Standalone and default icons from `Assets/Branding/Icons` through the Player Settings API. The build fails if an icon is missing (the log line reads "Application icon set from Assets/Branding/Icons (sizes 1024, 512, 256, 128, 64, 48, 32, 16)").
- **Desktop shortcuts:** `Tools/install/Install-Shortcuts.ps1` creates "The Twelfth Stamp - Quest 3" (`--mode=vr`) and "The Twelfth Stamp - Desktop" (`--mode=desktop`). Both use the `.ico` next to the exe, and their paths are relative to the extracted folder.

## Notes

- Missing branding textures log `[TS-Brand] Missing branding texture …` as an error, and the game continues with plain text.
- If Windows shows an old icon after an update, it is the shell's icon cache. Re-run "Install Shortcuts.cmd", or sign out and back in. Don't delete system icon caches or restart Explorer as a first step.
