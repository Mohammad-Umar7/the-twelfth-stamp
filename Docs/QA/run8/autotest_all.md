# AutoTest report: all

- Date: 2026-10-17 15:06:30
- Build: 0.9.0 | mode: Desktop | resolution: 1600x900
- GPU: NVIDIA GeForce RTX 5080 Laptop GPU | API: Direct3D11
- Result: **109 passed, 0 failed**
- Frame timing: frames=197836 median=1.1ms p95=1.7ms worst=44.8ms (desktop window, includes loading spikes; NOT a VR measurement)
- Errors/exceptions logged: 0

## Steps
    [    0.0s] AutoTest 'all' mode=Desktop screen=1600x900
    [    3.1s] PASS main menu cell active
    [    3.1s] PASS main menu panel open
    [    3.1s] screenshot 01_menu.png
    [    3.1s] --- Judges' demo flow (Paris + Egyptian sunlight) ---
    [    4.2s] PASS demo enters Paris (waited 1.0s)
    [    5.6s] PASS Passenger guards the desk
    [    5.6s] PASS trace is blocked while the Passenger is at the desk
    [    5.6s] screenshot 02_paris_arrival.png
    [    6.2s] PASS passport open with Egypt selected
    [    6.2s] screenshot 03_passport_open.png
    [    6.3s] PASS desk seam targeted from the passport
    [    6.5s] PASS tear opened to Egypt
    [    6.5s] PASS Egyptian sunlight flows through the tear
    [    7.7s] screenshot 04_slit_sunlight.png
    [    9.3s] PASS tear widened to a window
    [    9.3s] PASS sunlight drives the Passenger off the desk (waited 0.0s)
    [    9.3s] screenshot 05_window_repel.png
    [   11.7s] PASS a hand comes through the widened tear (waited 2.4s)
    [   12.7s] screenshot 06_hand_through_tear.png
    [   13.8s] PASS tear sealed (one active tear, cleanly closed) (waited 0.2s)
    [   13.8s] screenshot 07_sealed.png
    [   13.8s] PASS trace usable after the desk is cleared
    [   13.8s] PASS France seal verified (waited 0.0s)
    [   15.5s] PASS counterfeit HOME stamp revealed (waited 1.7s)
    [   17.0s] screenshot 08_counterfeit_stamp.png
    [   28.7s] PASS demo results shown (waited 11.7s)
    [   28.7s] screenshot 09_demo_results.png
    [   28.7s] PASS demo did not write the campaign save
    [   28.7s] --- Egypt reflector puzzle ---
    [   29.7s] PASS enters Egypt (waited 1.0s)
    [   31.2s] screenshot 10_egypt_arrival.png
    [   31.2s] PASS trace hidden before the puzzle
    [   47.7s] PASS the mummy rises behind the player (waited 16.5s)
    [   47.7s] (Egypt: Passenger drain disabled for the close-up photos)
    [   50.2s] screenshot 10b_egypt_mummy_rises.png
    [   50.6s] screenshot 10c_egypt_mummy_close.png
    [   52.8s] PASS reflected sunlight burns the wrong shadow and reveals the trace (waited 0.8s)
    [   52.8s] screenshot 11_egypt_trace.png
    [   52.8s] PASS Egypt verified, sunlight resource unlocked (waited 0.0s)
    [   54.8s] screenshot 12_egypt_verified.png
    [   54.8s] --- Story: shop prologue -> homecoming -> corruption -> UAE tutorial -> Egypt ---
    [   55.8s] PASS new story starts outside the Marrakech shop (waited 1.0s)
    [   57.8s] screenshot 20_shop_exterior.png
    [   59.3s] PASS shopkeeper present behind the counter
    [   59.3s] screenshot 21_shop_interior.png
    [   63.1s] PASS payment step reached (waited 0.0s)
    [   63.1s] PASS festival token placed in the brass tray
    [   63.1s] PASS case step reached (waited 0.0s)
    [   63.1s] PASS glasses put in their case (not worn)
    [   64.3s] PASS prologue complete (waited 1.2s)
    [   71.6s] PASS travel home to the Abu Dhabi apartment (waited 7.3s)
    [   73.1s] screenshot 22_home_arrival.png
    [   73.2s] PASS unpacked camera, passport and case onto the table (waited 0.0s)
    [   75.7s] PASS phone becomes interactive (voicemail)
    [   80.7s] screenshot 23_home_voicemail.png
    [   80.7s] PASS glasses case can be opened
    [   81.3s] PASS glasses taken out of the case
    [   82.2s] PASS glasses worn (safe virtual interaction) (waited 0.5s)
    [   91.2s] screenshot 24_reflection_wrong.png
    [   98.3s] PASS the apartment becomes the Margin copy in place (waited 7.0s)
    [  100.3s] screenshot 25_margin_apartment.png
    [  100.3s] PASS a seam is targeted in the apartment
    [  100.6s] PASS first tear opened (to Egypt)
    [  100.6s] PASS light reveals the genuine note (waited 0.0s)
    [  101.6s] screenshot 26_first_tear.png
    [  101.6s] PASS homecoming seal verified via the genuine detail (waited 0.0s)
    [  102.7s] PASS tutorial tear sealed (waited 0.2s)
    [  107.7s] PASS first controlled threat appears (waited 5.0s)
    [  108.7s] screenshot 27_first_threat.png
    [  112.3s] PASS tear widened to a doorway
    [  112.4s] screenshot 28_doorway.png
    [  113.2s] PASS stepped through the doorway into Egypt (waited 0.8s)
    [  113.2s] PASS tutorial complete after escaping the apartment
    [  114.7s] screenshot 29_egypt_after_escape.png
    [  114.7s] --- Countries: New York, Japan, Riyadh, Agra, Rio, London, Marrakech ---
    [  115.7s] PASS enters NewYork (waited 1.0s)
    [  116.9s] screenshot 30_newyork.png
    [  116.9s] PASS arrival gate blocked while it stands there
    [  117.3s] screenshot 30b_newyork_passenger_close.png
    [  118.3s] PASS booth powered; screen shows the double arrival
    [  118.3s] screenshot 31_newyork_screen.png
    [  118.3s] PASS noisemaker draws it off the gate (waited 0.0s)
    [  122.7s] PASS arrival gate clear (waited 4.4s)
    [  122.7s] PASS USA trace revealed and usable
    [  122.7s] PASS USA seal verified (waited 0.0s)
    [  122.7s] PASS noisemaker becomes a carried resource
    [  123.8s] PASS enters Japan (waited 1.0s)
    [  125.0s] screenshot 32_japan.png
    [  125.0s] PASS lantern on the correct hook reveals the genuine trace (waited 0.0s)
    [  125.8s] screenshot 33_japan_solved.png
    [  125.8s] PASS Japan trace revealed and usable
    [  125.8s] PASS Japan seal verified (waited 0.0s)
    [  126.8s] PASS enters Riyadh (waited 1.0s)
    [  128.0s] screenshot 34_riyadh.png
    [  129.1s] PASS optical panel aligned
    [  129.1s] PASS walking through the honest arch reveals the trace (waited 0.0s)
    [  129.1s] screenshot 35_riyadh_solved.png
    [  129.1s] PASS SaudiArabia trace revealed and usable
    [  129.1s] PASS SaudiArabia seal verified (waited 0.0s)
    [  130.1s] PASS enters Agra (waited 1.0s)
    [  131.3s] screenshot 36_agra.png
    [  131.3s] PASS lamp on the correct pedestal reveals the trace (waited 0.0s)
    [  133.8s] screenshot 37_agra_stepout.png
    [  133.8s] PASS India trace revealed and usable
    [  133.8s] PASS India seal verified (waited 0.0s)
    [  134.9s] PASS enters Rio (waited 1.0s)
    [  136.6s] screenshot 38_rio.png
    [  136.6s] PASS between beats (waited 0.0s)
    [  137.2s] PASS drum beat 1 (waited 0.7s)
    [  137.5s] PASS between beats (waited 0.3s)
    [  138.5s] PASS drum beat 2 (waited 0.9s)
    [  138.8s] PASS between beats (waited 0.3s)
    [  139.7s] PASS drum beat 3 (waited 0.9s)
    [  139.7s] PASS Brazil verified by three masked stamps (waited 0.0s)
    [  140.8s] PASS enters London (waited 1.0s)
    [  142.0s] screenshot 39_london.png
    [  142.0s] PASS checkpoint seam targeted
    [  151.6s] PASS clock chime (waited 9.6s)
    [  151.8s] PASS tear opened under the chime reveals the trace (waited 0.0s)
    [  152.4s] screenshot 40_london_chime.png
    [  153.5s] PASS London tear sealed (waited 0.2s)
    [  153.5s] PASS UnitedKingdom trace revealed and usable
    [  153.5s] PASS UnitedKingdom seal verified (waited 0.0s)
    [  154.5s] PASS enters ShopCorrupt (waited 1.0s)
    [  155.7s] (Marrakech: Passenger drain disabled for this puzzle-logic check)
    [  156.2s] screenshot 41_marrakech_margin.png
    [  156.2s] PASS Marrakech puzzle props built
    [  157.0s] PASS counter rebuilt; the drawer gives up the calibration insert (waited 0.8s)
    [  157.0s] PASS glasses case set out on the counter
    [  157.0s] PASS insert fitted; hidden notes appear (waited 0.0s)
    [  159.0s] screenshot 42_marrakech_notes.png
    [  188.0s] PASS optician's notes read in full; trace revealed (waited 29.0s)
    [  188.0s] PASS Morocco trace revealed and usable
    [  188.0s] PASS Morocco seal verified (waited 0.0s)
    [  188.0s] PASS calibration insert recovered
    [  188.0s] --- Finale: Berlin -> near miss -> real home -> epilogue -> results ---
    [  189.0s] PASS enters Berlin (waited 1.0s)
    [  190.2s] screenshot 50_berlin.png
    [  190.2s] PASS the true return is hidden at the start
    [  190.2s] PASS registration plinth accepts ten seals plus the insert
    [  190.2s] PASS eleven of eleven seals verified (waited 0.0s)
    [  195.4s] PASS the mimic appears at the blank wall (waited 5.2s)
    [  196.9s] screenshot 51_berlin_mimic.png
    [  197.3s] screenshot 51b_berlin_mimic_close.png
    [  197.3s] PASS the noisemaker from New York is here
    [  197.3s] PASS seam facing the doors targeted
    [  198.9s] PASS Egyptian sunlight pours toward the decoy doors
    [  198.9s] beam pos=(2.05, 1.40, 0.00) fwd=(1.00, -0.05, 0.00) strength=1.00 aperture=0.42 target=(8.55, 1.30, 0.00) local=(0.00, 0.24, 6.50) exposure=1.00 blocked=no
    [  198.9s] PASS daylight exposes the forged HOME doors (waited 0.0s)
    [  199.9s] screenshot 52_berlin_sunlight.png
    [  199.9s] PASS forged HOME stamp cancelled with VOID (waited 0.0s)
    [  201.0s] PASS resource tear sealed before the home tear (waited 0.2s)
    [  201.0s] PASS genuine home can open (11 seals, insert, forgery cancelled)
    [  201.0s] PASS the blank wall holds the true return
    [  204.6s] PASS home tear opened to the real apartment
    [  204.6s] screenshot 53_home_tear.png
    [  206.7s] PASS near miss survived; glasses removed (virtual gesture) (waited 2.1s)
    [  209.2s] screenshot 54_real_home.png
    [  209.8s] PASS glasses put down and broken (waited 0.6s)
    [  224.7s] PASS bathroom, epilogue, campaign complete (waited 14.8s)
    [  224.7s] PASS results shown (waited 0.0s)
    [  225.5s] screenshot 55_results.png
