using System;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// The walkthrough for each place: what to do, in order, how to tell each step is done (read from the encounter's
    /// public state, never changed), where the marker goes, and what the danger is. Guide shows it. Where nothing in the
    /// world gives the answer away (Japan's hook, Agra's pedestal, Riyadh's arch) the marker shows it: the player asked
    /// to be told what to do.
    /// </summary>
    public static partial class GuideBook
    {
        public static PlaceGuide For(Cell cell)
        {
            if (cell == null) return null;
            try
            {
                switch (cell.Id)
                {
                    case CellId.Menu:
                    case CellId.ApartmentNormal:   // the homecoming and the prologue tell their own story
                    case CellId.ShopNormal:
                    case CellId.Epilogue:
                        return null;
                    case CellId.ApartmentCorrupt: return Uae((ApartmentCell)cell);
                    case CellId.ShopCorrupt: return Morocco((ShopCell)cell);
                }
                switch (cell)
                {
                    case EgyptCell e: return Egypt(e);
                    case ParisCell p: return Paris(p);
                    case NewYorkCell n: return NewYork(n);
                    case JapanCell j: return Japan(j);
                    case RiyadhCell r: return Riyadh(r);
                    case AgraCell a: return Agra(a);
                    case RioCell r: return Rio(r);
                    case LondonCell l: return London(l);
                    case BerlinCell b: return Berlin(b);
                }
                return Generic(cell);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[TS-Guide] no guide for " + cell.Id + ": " + ex.Message);
                return null;
            }
        }

        // ------------------------------------------------------------------ shared pieces
        static string Key(Intent i) => Hud.KeyTag(i);
        static string Use => Key(Intent.Interact);
        static string Grab => Key(Intent.Grab);
        static bool Vr => Game.Mode == GameMode.VR;
        static string RunKey => Vr ? "push the left stick fully forward" : "hold <color=#EDCF99><b>SHIFT</b></color>";
        static string Activate => Vr ? "pull the trigger" : "click or press " + Use;
        static bool Verified(CountryId id) => Game.Campaign != null && Game.Campaign.IsVerified(id);
        static Vector3? Pos(Component c) => c != null && c.gameObject.activeInHierarchy ? c.transform.position : (Vector3?)null;
        static Vector3 W(Cell c, float x, float y, float z) => c.transform.TransformPoint(new Vector3(x, y, z));
        static float Dist(Vector3 p) => Game.Rig != null ? (p - Game.Rig.Feet).magnitude : 999f;
        static bool Holding(Grabbable g) => g != null && g.Holder != null;
        static bool TearOpen => Game.Tears != null && Game.Tears.Active != null;
        static Vector3? TearAt => TearOpen ? Game.Tears.Active.OpeningCenter : (Vector3?)null;
        static bool PassportOpen => Game.Passport != null && Game.Passport.IsOpen;

        /// <summary>Press the passport to the revealed trace.</summary>
        static GuideStep TraceStep(CountryId id, Func<TraceMark> trace, string where)
        {
            return new GuideStep
            {
                Text = "Walk to the glowing trace " + where + " and press your passport to it  " + Use,
                Hint = "The trace is a softly glowing stamp mark. Stand close, look straight at it and press " + Use + ". If it says something is in the way, deal with that first.",
                Done = () => Verified(id),
                Target = () =>
                {
                    var t = trace();
                    return t != null && t.Revealed ? t.transform.position : (Vector3?)null;
                },
                Label = "Trace",
            };
        }

        /// <summary>After verifying, the country's stamp appears; reaching it shifts the player on.</summary>
        static GuideStep StampStep(CountryId id, string chaser = "the Passenger")
        {
            return new GuideStep
            {
                Text = "Run to the stamp before " + chaser + " reaches you: it carries you to the next country  (run: " + RunKey + ")",
                Hint = "Follow the marker (and the gold star on the map). The stamp is a glowing disc; touch it to shift.",
                Done = () => Verified(id) && (Game.Story == null || Game.Story.WaitingStamp == null),
                Target = () => Game.Story != null && Game.Story.WaitingStamp != null ? Game.Story.WaitingStamp.transform.position : (Vector3?)null,
                Label = "Stamp",
            };
        }

        /// <summary>The way on when there is no stamp to run for: the passport and a tear.</summary>
        static GuideStep TravelStep(Cell cell)
        {
            return new GuideStep
            {
                Text = "Open your passport  " + Key(Intent.Passport) + ", choose where to go next  " + Key(Intent.Destination) +
                       ", aim at a seam and open a tear  " + Key(Intent.Tear) + ". Widen it to a doorway and walk through",
                Hint = "Seams are faint glowing lines on walls (teal circles on the map). Keep holding " + Key(Intent.Tear) + " until the tear is wide enough to step through.",
                Done = () => false,
                Target = () => NearestSeam(cell),
                Label = "Seam",
            };
        }

        static Vector3? NearestSeam(Cell cell, bool revealedOnly = true)
        {
            if (cell == null || Game.Rig == null) return null;
            Vector3? best = null;
            float bd = float.MaxValue;
            foreach (var s in cell.Seams)
            {
                if (s == null || !s.isActiveAndEnabled || (revealedOnly && !s.Revealed)) continue;
                float d = (s.Center - Game.Rig.Feet).sqrMagnitude;
                if (d < bd) { bd = d; best = s.Center; }
            }
            return best;
        }

        /// <summary>A warning when the Passenger is close or chasing.</summary>
        static string PassengerAlert(float range, string near)
        {
            var pc = Game.Passenger;
            if (pc == null || !pc.Visible || Game.Rig == null) return null;
            if (pc.Chasing) return "<b>RUN.</b> It is chasing you: " + RunKey + ".";
            float d = (pc.transform.position - Game.Rig.Feet).magnitude;
            return d < range ? near : null;
        }

        static string TearAlert()
        {
            if (!TearOpen) return null;
            float c = Game.Tears.Contamination;
            return c > 0.6f ? "The tear is straining. Seal it now: hold " + Key(Intent.Seal) + "." : null;
        }

        static string FirstOf(params Func<string>[] alerts)
        {
            foreach (var a in alerts)
            {
                var s = a();
                if (!string.IsNullOrEmpty(s)) return s;
            }
            return null;
        }

        /// <summary>For a place without its own walkthrough: the country's objective, then the stamp and travel.</summary>
        static PlaceGuide Generic(Cell cell)
        {
            var c = cell.Country;
            if (c == null) return null;
            var g = new PlaceGuide { Title = c.DisplayName + " · " + c.EncounterTitle, Briefing = c.Objective };
            g.Steps.Add(new GuideStep { Text = c.Objective, Done = () => Verified(c.Id), Target = () => NearestSeam(cell) });
            g.Steps.Add(StampStep(c.Id));
            g.Steps.Add(TravelStep(cell));
            g.Alert = () => PassengerAlert(10f, "The Passenger is close. Keep your distance.");
            return g;
        }

        // UAE, the Margin apartment's whack-a-ghost: Guide/GuideBook.Uae.cs.

        // Egypt, the temple run: Guide/GuideBook.Egypt.cs.

        // France, the bubble shooter over the Seine: Guide/GuideBook.Paris.cs.

        // ------------------------------------------------------------------ Japan
        static PlaceGuide Japan(JapanCell j)
        {
            const CountryId id = CountryId.Japan;
            var enc = j.Encounter as JapanEncounter;
            var g = new PlaceGuide
            {
                Title = "Japan · The Claw Machine",
                Briefing = "A claw machine glows in the lantern street. The golden passport inside it is your way out of Japan: win it and the gate at the end of the street opens.",
                Danger = "The lantern figure comes in through the gate while you play, and now and then it rushes. Run down the street until it loses you, then come back to the machine.",
            };
            Func<bool> solved = () => Verified(id) || (enc != null && enc.Solved);
            g.Steps.Add(new GuideStep
            {
                Text = "Line the claw up over the <b>golden passport</b> with the joystick buttons, then press <b>DROP</b>  " + Use,
                Hint = "Each arrow moves the claw one place. Under the cat, press DROP: the claw grabs it and carries it to the chute. A plush toy is the wrong prize; try again.",
                Done = () => solved() || (j.Claw != null && j.Claw.GoldenOut),
                Target = () => j.Claw != null ? j.Claw.ControlsPosition : (Vector3?)null,
                Label = "Claw machine",
            });
            g.Steps.Add(new GuideStep
            {
                Text = "Take the golden passport from the prize tray  " + Grab,
                Done = solved,
                Target = () => j.Claw != null ? j.Claw.GoldenPosition : (Vector3?)null,
                Label = "Golden passport",
            });
            g.Steps.Add(StampStep(id, "the lantern figure"));
            g.Steps.Add(TravelStep(j));
            g.Alert = () => PassengerAlert(7f, "The lantern figure is close. Run, lose it, come back.");
            return g;
        }

        // ------------------------------------------------------------------ Saudi Arabia: Riyadh
        static PlaceGuide Riyadh(RiyadhCell r)
        {
            const CountryId id = CountryId.SaudiArabia;
            var enc = r.Encounter as RiyadhEncounter;
            var g = new PlaceGuide
            {
                Title = "Saudi Arabia · The Maze",
                Briefing = "A maze of mud-brick walls. A portal waits past its far (north) end and carries you on to the next country.",
                Danger = "The heat drains your energy all the time: drink the glowing water glasses on the way. The figure walks straight through the walls after you.",
            };
            Func<bool> solved = () => Verified(id) || (enc != null && enc.Solved);
            g.Steps.Add(new GuideStep
            {
                Text = "Find your way through the maze to the portal. Drink the water glasses to keep your energy up",
                Hint = "Keep heading north. Dead ends often hold a glass of water: walk up to it to drink. Do not stand still, it is always coming.",
                Done = solved,
                Target = () => MazeWaypoint(r),
                Label = "This way",
            });
            g.Steps.Add(StampStep(id));
            g.Steps.Add(TravelStep(r));
            g.Alert = () => PassengerAlert(6f, "It is right behind you. Keep moving.");
            return g;
        }

        /// <summary>A few cells further along the maze's way out from where the player is.</summary>
        static Vector3? MazeWaypoint(RiyadhCell r)
        {
            var m = r.Maze;
            if (m == null || Game.Rig == null) return null;
            var here = m.CellAt(r.transform.InverseTransformPoint(Game.Rig.Feet));
            int i = m.Solution.IndexOf(here);
            if (i < 0)
            {
                // Off the way out: point back to the nearest cell on it.
                float best = float.MaxValue;
                for (int k = 0; k < m.Solution.Count; k++)
                {
                    float d = (m.Solution[k] - here).sqrMagnitude;
                    if (d < best) { best = d; i = k; }
                }
                return r.transform.TransformPoint(m.CellCenter(m.Solution[i]) + Vector3.up);
            }
            int next = Mathf.Min(i + 2, m.Solution.Count - 1);
            if (i == m.Solution.Count - 1) return r.transform.TransformPoint(m.PortalLocal + Vector3.up);
            return r.transform.TransformPoint(m.CellCenter(m.Solution[next]) + Vector3.up);
        }

        // ------------------------------------------------------------------ India: Agra
        static PlaceGuide Agra(AgraCell a)
        {
            const CountryId id = CountryId.India;
            var enc = a.Encounter as AgraEncounter;
            var g = new PlaceGuide
            {
                Title = "India · The Taj Mahal Puzzle",
                Briefing = "On a table by the pool lies a picture puzzle of the Taj Mahal, the real one standing at the end of the garden. Put the picture right to go on.",
                Danger = "A figure climbs out of the pool and hunts the garden while you work. Run until it loses you, then come back to the table.",
            };
            Func<bool> solved = () => Verified(id) || (enc != null && enc.Solved);
            g.Steps.Add(new GuideStep
            {
                Text = "Put the 6 Taj Mahal pieces on the board: pick one up  " + Use + "  and aim at its place (it snaps in)",
                Hint = "Each place on the board shows a faint ghost of its part of the picture, and only the right piece goes in. The postcard behind the board shows the finished picture.",
                Done = solved,
                Target = () => a.Puzzle != null ? a.Puzzle.BoardPosition : (Vector3?)null,
                Label = "Puzzle",
            });
            g.Steps.Add(StampStep(id, "the figure from the pool"));
            g.Steps.Add(TravelStep(a));
            g.Alert = () => PassengerAlert(8f, "The figure from the pool is coming. Run, lose it, come back.");
            return g;
        }

        // USA (New York) and Brazil (Rio): Guide/GuideBook.NewYork.cs and Guide/GuideBook.Rio.cs.

        // United Kingdom (London) and Morocco: Guide/GuideBook.London.cs and Guide/GuideBook.Morocco.cs.

        // Germany, Berlin, the finale's shooting gallery: Guide/GuideBook.Berlin.cs.
    }
}
