# The Twelfth Stamp: pitch and 60-second judges' demo

## One-line pitch

You came home from the Festival of Passports with eleven seals in your passport and a twelfth you never earned. Something forged it, and it walked home in your shape. Tear your passport's seams open to borrow light and sound from the countries you visited, prove which places were real, and find the one way home it cannot copy.

## 30-second spoken pitch

> "The Twelfth Stamp is a first-person survival-puzzle horror game for Quest 3 and PC. Saeed comes home from a festival where every country stamps your passport. There are eleven seals, but there are twelve stamps, and the twelfth says HOME. It was forged by the Passenger, a figure of smoked glass that copies the places he remembers. Your passport is your only tool. Tear a seam open and you can pull Egyptian sunlight into a rainy Paris night to drive the Passenger off a desk, or use a London chime to hide the sound of a tear. Every tear lets something through in both directions. Verify all eleven seals, cancel the forgery in Berlin, and open the one door home that it cannot fake."

How it fits the theme: the festival's passport is the game's only tool, and every country's "stamp" is a puzzle about telling a real place from a remembered copy.

## 60-second judges' demo (Main menu > "Judges' Demo", or `TheTwelfthStamp.exe --mode=desktop --demo`)

The demo never touches the story save. Each step below is checked by the in-build automated test (`--autotest=demo`). The times come from those runs, so a person playing will take longer.

| # | What the player does | What the judge sees | Desktop | Quest 3 |
|---|---|---|---|---|
| 1 | Arrive on a rainy Seine embankment | "FRANCE / Judges' demo". The Passenger, in a raincoat disguise, guards the immigration desk. The arrival trace is blocked. | | |
| 2 | Open the passport and pick Egypt | The insert page with eleven seal spaces. Egypt is selected. | F, then the mouse wheel | X, then B |
| 3 | Aim at the seam facing the desk and open a tear | A slit opens. Real Egyptian sunlight pours into the night. | Hold left mouse | Grip the seam and pull, or hold the right trigger |
| 4 | Widen it to a window | The beam reaches the desk. The Passenger is burned back into the dark. A hand reaches through the tear from the other side: wider lets more through, in both directions. | Keep holding | Keep pulling |
| 5 | Seal the tear | The tear stitches shut. | Hold right mouse | Hold A |
| 6 | Walk to the desk and press the passport to the arrival trace | France seal verified (3 / 11). The insert flips over: a twelfth stamp, **HOME — ENTRY GRANTED**, that was never issued. | E | Trigger |
| 7 | End card | Title card, then the demo results with the measured time. | | |

### Before the judges arrive

- [ ] Build from `C:\Dev\12th-Stamp-builds\Builds\Windows` or the ZIP. Both desktop shortcuts start.
- [ ] Desktop: volume at a comfortable level, subtitles on (default), Graphics "High" or "Ultra".
- [ ] Quest 3: Link connected, Meta Horizon Link set as the active OpenXR runtime **by you** in the Link app. Run the demo once in the headset before judging.
- [ ] Tell judges: pause any time (Esc / left Menu button). Taking the headset off is always fine.

### If something goes wrong

| Problem | Do this |
|---|---|
| VR will not start | The game offers Retry / Desktop Mode / Quit. Choose Desktop Mode and run the demo on the PC. |
| The tear will not open | The passport must be open with Egypt selected, and you must aim at the stitched seam (a shimmering line) facing the desk. |
| The Passenger reaches you | The screen drains. Seal the tear, back away, then try again. If vitality runs out, the failure screen offers Retry, which restarts at the embankment (not the whole game). |
