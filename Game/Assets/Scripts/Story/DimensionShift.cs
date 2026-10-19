using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace TwelfthStamp
{
    /// <summary>
    /// The horror "dimension shift" between places, in five beats.
    /// 1. Rumble: the light sinks into a slow pulse of the destination's light (no red: playtest, 18 Oct), things near the
    ///    player start to rattle, the first cracks split the walls.
    /// 2. Tearing loose: props and furniture near the player lift off and circle him, faster and faster (stand-in copies:
    ///    the real ones are only hidden, and shown again at the swap); debris tears up from the floor; the wall surface peels
    ///    away in sheets that turn over into passport pages and spiral up; through every crack and bared patch shows the
    ///    light of where he is going (Egypt's sun, Paris's rain-light), in shafts across the floor. On desktop a short
    ///    streak tunnel opens at the end.
    /// 3. Inhale (0.4 s): everything slows and hangs in the air, the room's light and the sound drain away; only the cracks
    ///    blaze.
    /// 4. Slam: the cracks burst, one dark fade hides the swap the caller makes (activate the next cell, place the player),
    ///    its colour a dark shade of the destination's light.
    /// 5. Arrival: the new place assembles round him: the debris that came through falls and sinks into its floor, dust
    ///    drifts down through its light, a ring of that light runs out across the ground, and its lamps come up.
    /// Comfort (the brief's rules): the camera and the rig are never moved, shaken or turned; nothing flashes faster than
    /// about once a second (the pulse is 0.7 Hz, every other change is one slow ramp); debris keeps out of arm's reach and
    /// circles both ways round (the view never swirls as one), slower in VR; the tunnel lasts at most 1.5 s and never runs
    /// in VR, with reduced effects or on Gentle; reduced effects also drop the rattle and thin everything out; Gentle (scare
    /// intensity 0) gets a shorter, softer version. Skippable once it has been seen. Short build-ups (1.8 s for tear travel)
    /// keep every beat: each layer is staged on the build-up's fraction, with floors on its own durations.
    /// </summary>
    public static class DimensionShift
    {
        public enum Stage { None, Build, Inhale, Slam, Arrival }

        const string SeenKey = "ts_shift_seen";

        public static bool Running { get; private set; }

        /// <summary>Where the running shift is (tests time their screenshots on it).</summary>
        public static Stage Phase { get; private set; }

        /// <summary>The player has seen a full shift before (then the skip input jumps straight to the swap).</summary>
        public static bool Seen
        {
            get { try { return PlayerPrefs.GetInt(SeenKey, 0) == 1; } catch { return false; } }
            set { try { PlayerPrefs.SetInt(SeenKey, value ? 1 : 0); PlayerPrefs.Save(); } catch { } }
        }

        public static bool Gentle => Game.Settings != null && Game.Settings.scareIntensity == 0;
        static bool Reduced => Game.Settings != null && Game.Settings.reducedEffects;

        /// <summary>The skip input (desktop Skip key, either VR trigger).</summary>
        public static bool SkipPressed()
        {
            var i = Game.Input;
            if (i == null) return false;
            if (i.Skip != null && i.Skip.WasPressedThisFrame()) return true;
            return Game.Mode == GameMode.VR && ((i.RTrigger != null && i.RTrigger.WasPressedThisFrame()) || (i.LTrigger != null && i.LTrigger.WasPressedThisFrame()));
        }

        /// <summary>What the last shift did, for tests and QA.</summary>
        public struct Report
        {
            public Color Glimpse;
            /// <summary>The slam's fade colour: a dark shade of the destination's light (never red).</summary>
            public Color Slam;
            public int Loose, Pages, Tears, Shafts, Settled;
            /// <summary>The room's light at the bottom of the inhale, as a share of its own level.</summary>
            public float InhaleLight;
            public bool Skipped;
            /// <summary>The clips it used (the audio helper's shift clips, or the older ones standing in).</summary>
            public string Sounds;
        }

        public static Report Last;

        /// <summary>The originals the last shift hid behind its stand-ins (tests check they come back).</summary>
        public static readonly List<Renderer> LastHidden = new List<Renderer>();

        /// <summary>The light of a place as it shows through the cracks before the player arrives.</summary>
        public static Color GlimpseOf(CellId id)
        {
            switch (id)
            {
                case CellId.Egypt: return new Color(1f, 0.78f, 0.45f);            // desert sun
                case CellId.Paris: return new Color(0.55f, 0.72f, 1f);            // rain-light
                case CellId.NewYork: return new Color(0.58f, 0.8f, 0.96f);        // harbour dusk
                case CellId.Japan: return new Color(0.68f, 0.66f, 1f);            // moonlight
                case CellId.Riyadh: return new Color(1f, 0.64f, 0.36f);           // dusk amber
                case CellId.Agra: return new Color(1f, 0.72f, 0.56f);             // dawn over the garden
                case CellId.Rio: return new Color(0.72f, 0.88f, 1f);              // frost
                case CellId.London: return new Color(0.72f, 0.8f, 0.95f);         // river grey
                case CellId.Berlin: return new Color(0.9f, 0.93f, 1f);            // the hall's white
                case CellId.ApartmentNormal: return new Color(1f, 0.7f, 0.44f);   // sunset through the blinds
                case CellId.ShopNormal: return new Color(1f, 0.74f, 0.46f);
                case CellId.ApartmentCorrupt: return new Color(0.58f, 0.72f, 0.96f);
                case CellId.ShopCorrupt: return new Color(0.5f, 0.86f, 0.8f);
                case CellId.Epilogue: return new Color(1f, 0.92f, 0.78f);
                default: return new Color(0.85f, 0.9f, 1f);
            }
        }

        /// <summary>The new clip if the audio helper has made it, else an existing one.</summary>
        static string Pick(string id, string fallback)
        {
            var a = AudioDirector.I;
            if (a == null) return fallback;
            bool made = a.Info(id) != null || Resources.Load<AudioClip>("Audio/SFX/" + id) != null;
            return made && a.Has(id) ? id : fallback;
        }

        // Where each of the audio helper's shift clips peaks, in seconds into the clip (Tools/audio/sfx_shift.py), for when
        // the audio manifest has no "peak" for it.
        const float RumblePeak = 4f, SwellPeak = 2.4f, WhooshPeak = 1.5f;

        /// <summary>
        /// A one-shot built round a peak (the manifest's "peak", else the fallback): started once the shift is within the
        /// clip's lead of its moment, part-way in when the shift is shorter than that lead, so the peak lands on the moment.
        /// It waits on the shift's own clock, so a pause holds it back too. No clip, no cue.
        /// </summary>
        class Cue
        {
            readonly string m_Id;
            readonly float m_Moment, m_Lead, m_Volume;
            bool m_Done;
            AudioSource m_Source;
            AudioClip m_Clip;

            public Cue(string id, float moment, float fallbackPeak, float volume)
            {
                m_Id = id;
                m_Moment = moment;
                var info = id != null ? AudioDirector.I?.Info(id) : null;
                m_Lead = info != null && info.peak > 0f ? info.peak : fallbackPeak;
                m_Volume = volume;
                m_Done = id == null;
            }

            /// <summary>Seconds since the build-up began.</summary>
            public void Tick(float elapsed)
            {
                if (m_Done || elapsed < m_Moment - m_Lead) return;
                m_Done = true;
                m_Source = AudioDirector.I?.PlayToPeak(m_Id, Mathf.Max(0f, m_Moment - elapsed), m_Volume);
                m_Clip = m_Source != null ? m_Source.clip : null;
            }

            /// <summary>Fades it out on a skip. Play sources are pooled, so only while it is still playing this clip.</summary>
            public void Stop()
            {
                m_Done = true;
                if (m_Source != null && m_Source.isPlaying && m_Source.clip == m_Clip) AudioDirector.I?.FadeOutAndStop(m_Source, 0.2f);
                m_Source = null;
            }
        }

        /// <summary>
        /// Plays the shift around the player. <paramref name="swap"/> runs once at the peak, behind the fade.
        /// <paramref name="buildUp"/> is the chaos before the peak (Gentle halves it); the inhale, the slam and the arrival
        /// add about 2.3 s (the swap comes about 0.9 s after the build-up). <paramref name="into"/> (or <paramref name="glimpse"/>) is the light that shows through the cracks.
        /// Input is locked throughout.
        /// </summary>
        public static IEnumerator Run(System.Action swap, float buildUp = 4f, CellId into = CellId.None, Color? glimpse = null)
        {
            Running = true;
            Phase = Stage.Build;
            Game.InputLocks++;
            bool gentle = Gentle, reduced = Reduced, vr = Game.Mode == GameMode.VR;
            float build = Mathf.Max(0.8f, gentle ? buildUp * 0.5f : buildUp);
            bool canSkip = Seen;
            var light = glimpse ?? GlimpseOf(into);
            Last = new Report { Glimpse = light };
            LastHidden.Clear();
            var head = Game.Rig.Head;
            var root = new GameObject("DimensionShift").transform;
            root.position = Game.Rig.Feet;
            var centre = root.position;

            var guard = root.gameObject.AddComponent<Guard>();
            guard.Beat = Time.time;
            guard.Track(Game.Cells.Active);
            var glow = PointLight(root, centre + Vector3.up * 2.2f, light, 9f);
            var slamShade = new Color(light.r * 0.12f, light.g * 0.12f, light.b * 0.12f);
            Last.Slam = slamShade;

            float motion = vr ? 0.6f : 1f;                              // VR: everything circles slower
            float thin = gentle ? 0.4f : reduced ? 0.6f : 1f;
            float liftTime = Mathf.Clamp(build * 0.3f, 0.35f, 1f);
            var look = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            look = look.sqrMagnitude > 0.01f ? look.normalized : Vector3.forward;
            var used = new List<Vector3>();                             // tears and pages keep apart
            var loose = Loose.Gather(Game.Cells.Active, centre, look, gentle ? 5 : reduced ? 8 : 16, guard);
            var debris = Debris(root, Mathf.RoundToInt(56 * thin));
            var tears = Tears(root, head, look, gentle ? 3 : 7, light, gentle ? 1 : vr ? 2 : 3, guard, used);
            var pages = Pages(root, head, centre, look, Mathf.RoundToInt((vr ? 16 : 22) * thin), light, build, guard, used);
            var tunnel = !vr && !reduced && !gentle ? new Tunnel(root, head, build, light, guard) : null;
            Last.Loose = loose.Count;
            Last.Pages = pages.Count;
            Last.Tears = tears.Count;
            foreach (var tr in tears) if (tr.HasShaft) Last.Shafts++;
            LastHidden.AddRange(guard.Hidden);

            // Sound. The audio helper's shift clips are one-shots built round a peak: the rumble groans up and cuts out as
            // the inhale begins, the reversed swell sucks in toward the slam and the whoosh rushes past on it (tear_breach
            // is the hit). Without them, older clips stand in: a low hum, and a caught breath for the inhale.
            var audio = AudioDirector.I;
            float slamAt = build + (gentle ? 0.3f : 0.4f);
            string rumbleClip = Pick("shift_rumble", null), swellClip = Pick("shift_reverse_swell", null), whooshClip = Pick("shift_whoosh", null);
            string tearId = Pick("shift_tear", "tear_open_slit"), arriveId = Pick("shift_arrive", "tear_seal");
            var cues = new[]
            {
                new Cue(rumbleClip, build, RumblePeak, gentle ? 0.5f : 0.85f),
                new Cue(swellClip, slamAt, SwellPeak, gentle ? 0.5f : 0.85f),
                new Cue(whooshClip, slamAt, WhooshPeak, gentle ? 0.5f : 0.9f),
            };
            Last.Sounds = "rumble=" + (rumbleClip ?? "tear_hum_loop") + " swell=" + (swellClip ?? "breath_scared") + " whoosh=" + (whooshClip ?? "-") + " tear=" + tearId + " arrive=" + arriveId;
            var stretch = audio?.Loop("tear_stretch_loop", head, 0f, Bus.SFX, 0f);
            var heart = audio?.Loop("heartbeat_loop", head, 0f, Bus.SFX, 0f);
            var rumble = rumbleClip == null ? audio?.Loop("tear_hum_loop", head, 0f, Bus.SFX, 0f) : null;
            float rumbleTop = gentle ? 0.4f : 0.8f, stretchTop = rumbleClip != null ? 0.45f : 0.7f;
            audio?.Play("pass_sting", null, gentle ? 0.4f : 0.75f, 0.8f, Bus.SFX, true);

            float t = 0f, pulse = 0f, shiftNow = 0f, tintNow = 0f, glowNow = 0f;
            int whispers = 0, maxWhispers = gentle ? 0 : build >= 3f ? 4 : 2, tearSounds = 0, pageSounds = 0;
            bool skipped = false;
            while (t < build)
            {
                if (canSkip && SkipPressed())
                {
                    skipped = true;
                    foreach (var c in cues) c.Stop();
                    break;
                }
                guard.Beat = Time.time;
                float dt = Time.deltaTime;
                t += dt;
                foreach (var c in cues) c.Tick(t);
                float k = Mathf.Clamp01(t / build);                    // 0..1 build-up
                pulse = 0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f * 0.7f); // 0.7 Hz: well under the flashing limit
                shiftNow = k * (0.75f + 0.25f * pulse) * (gentle ? 0.6f : 1f);
                tintNow = shiftNow * Mathf.Lerp(1f, 0.35f, Mathf.Clamp01((k - 0.15f) / 0.6f)); // the tint gives way to the cracks' light
                PostFx.I?.SetShift(shiftNow, tintNow, light);
                glowNow = Mathf.Lerp(0f, gentle ? 1.1f : 2.2f, k) * (0.65f + 0.35f * pulse);
                glow.intensity = glowNow;
                guard.Dim(Mathf.Lerp(1f, 0.25f, k));
                if (stretch != null) { audio.SetBaseVolume(stretch, k * stretchTop); stretch.pitch = 0.7f + k * 0.6f; }
                if (heart != null) { audio.SetBaseVolume(heart, 0.3f + k * 0.5f); heart.pitch = 1f + k * 0.5f; }
                if (rumble != null) { audio.SetBaseVolume(rumble, Mathf.Min(1f, k * 1.6f) * rumbleTop); rumble.pitch = 0.45f + k * 0.2f; }
                if (whispers < maxWhispers && t > build * (0.25f + whispers * 0.72f / maxWhispers))
                {
                    var a = Random.Range(0f, Mathf.PI * 2f);
                    audio?.Play("pass_whisper", head.position + new Vector3(Mathf.Cos(a), 0.2f, Mathf.Sin(a)) * 2.5f, 0.6f, Random.Range(0.8f, 1.1f));
                    whispers++;
                }
                foreach (var l in loose) l.Step(centre, k, dt * motion, liftTime, !reduced);
                foreach (var d in debris) d.Step(centre, k, dt * motion);
                foreach (var p in pages)
                {
                    p.Step(centre, k, dt * motion);
                    if (p.JustStarted && pageSounds < 6) { pageSounds++; audio?.Play("pp_page", p.Point, 0.55f, Random.Range(0.8f, 1.2f)); }
                }
                foreach (var tr in tears)
                {
                    tr.Step(k, 0f, 0f, pulse);
                    if (!tr.Heard && k >= tr.At) { tr.Heard = true; if (tearSounds++ < 3) audio?.Play(tearId, tr.Point, (tearSounds == 1 ? 0.85f : 0.6f) * (gentle ? 0.6f : 1f), Random.Range(0.85f, 1.1f), Bus.SFX, true); }
                }
                tunnel?.Step(t, dt);
                yield return null;
            }

            // The inhale: everything slows and hangs, the light and the sound drain away; only the cracks blaze.
            float inhale = skipped ? 0f : gentle ? 0.3f : 0.4f;
            if (inhale > 0f)
            {
                Phase = Stage.Inhale;
                if (swellClip == null) audio?.Play("breath_scared", null, gentle ? 0.45f : 0.8f, 1f, Bus.SFX, true);
                for (float i = 0f; i < inhale; i += Time.deltaTime)
                {
                    guard.Beat = Time.time;
                    foreach (var c in cues) c.Tick(build + i);
                    float f = Mathf.SmoothStep(0f, 1f, i / inhale);
                    float room = Mathf.Lerp(0.25f, 0.06f, f);
                    guard.Dim(room);
                    Last.InhaleLight = room;
                    glow.intensity = glowNow * (1f - f);
                    PostFx.I?.SetShift(shiftNow * (1f - 0.4f * f), tintNow * (1f - 0.6f * f));
                    PostFx.I?.SetInhale(f * (gentle ? 0.6f : 1f));
                    if (stretch != null) audio.SetBaseVolume(stretch, stretchTop * (1f - f));
                    if (heart != null) audio.SetBaseVolume(heart, 0.8f * (1f - f));
                    if (rumble != null) audio.SetBaseVolume(rumble, rumbleTop * (1f - f));
                    float dt = Time.deltaTime * Mathf.Lerp(1f, 0.06f, f) * motion; // hanging in the air
                    foreach (var l in loose) l.Step(centre, 1f, dt, liftTime, false);
                    foreach (var d in debris) d.Step(centre, 1f, dt);
                    foreach (var p in pages) p.Step(centre, 1f, dt);
                    foreach (var tr in tears) tr.Step(1f, f, 0f, pulse);
                    PageLight(1f - 0.65f * f);
                    yield return null;
                }
            }

            // The slam: the cracks burst as one dark fade closes, in a dark shade of where he is going.
            Phase = Stage.Slam;
            audio?.Play("tear_breach", null, gentle ? 0.6f : 1f, 0.85f, Bus.SFX, true);
            Fader.I.SetColor(slamShade);
            Fader.I.FadeTo(1f, skipped ? 0.15f : 0.28f);
            float slam = skipped ? 0.18f : 0.32f;
            for (float s = 0f; s < slam; s += Time.deltaTime)
            {
                guard.Beat = Time.time;
                float f = Mathf.Clamp01(s / slam);
                foreach (var tr in tears) tr.Step(1f, 1f, 1f - (1f - f) * (1f - f), pulse);
                yield return null;
            }
            var across = new Color(light.r * 0.2f, light.g * 0.2f, light.b * 0.2f);
            float cross = skipped ? 0.05f : 0.22f;
            for (float c = 0f; c < cross; c += Time.deltaTime)
            {
                guard.Beat = Time.time;
                Fader.I.SetColor(Color.Lerp(slamShade, across, c / cross));
                yield return null;
            }
            Fader.I.SetColor(across);

            // What came through with him (the arrival settles it in the new place).
            var carry = new List<Vector3>();
            int keep = gentle ? 10 : reduced ? 16 : 26;
            foreach (var d in debris) if (d.Up && carry.Count < keep) carry.Add(d.T.position - centre);
            while (carry.Count < keep)
            {
                float a = Random.Range(0f, Mathf.PI * 2f), r = Random.Range(1.5f, 3.8f);
                carry.Add(new Vector3(Mathf.Cos(a) * r, Random.Range(0.8f, 2.4f), Mathf.Sin(a) * r));
            }
            guard.Restore();
            PageLight(1f);
            Object.Destroy(root.gameObject);
            if (stretch != null) audio.FadeOutAndStop(stretch, 0.3f);
            if (heart != null) audio.FadeOutAndStop(heart, 1.2f);
            if (rumble != null) audio.FadeOutAndStop(rumble, 0.4f);
            try { swap?.Invoke(); }
            catch (System.Exception e) { Debug.LogException(e); }
            yield return null;

            Phase = Stage.Arrival;
            yield return Arrive(light, across, carry, gentle, reduced, motion, arriveId);
            PostFx.I?.SetShift(0f);
            PostFx.I?.SetInhale(0f);
            Fader.I.SetColor(Color.black);
            Last.Skipped = skipped;
            if (!skipped) Seen = true;
            Game.InputLocks--;
            Phase = Stage.None;
            Running = false;
        }

        // ------------------------------------------------------------------ arrival: the new place assembles
        static IEnumerator Arrive(Color light, Color from, List<Vector3> carry, bool gentle, bool reduced, float motion, string sound)
        {
            var head = Game.Rig.Head;
            var feet = Game.Rig.Feet;
            var root = new GameObject("ShiftArrival").transform;
            root.position = feet;
            var guard = root.gameObject.AddComponent<Guard>();
            guard.Beat = Time.time;
            guard.Track(Game.Cells.Active);
            guard.Dim(0.15f);
            var settling = Settle(root, feet, carry, motion);
            Last.Settled = settling.Count;
            float floor = Physics.Raycast(head.position, Vector3.down, out var hit, 4f, Layers.WorldMask, QueryTriggerInteraction.Ignore) ? hit.point.y : feet.y;
            var ring = new Ring(root, new Vector3(feet.x, floor + 0.03f, feet.z), light, gentle ? 0.5f : 0.9f, guard);
            Dust(head.position, light, gentle ? 50 : reduced ? 70 : 140);
            AudioDirector.I?.Play(sound, null, gentle ? 0.5f : 0.85f, 1f, Bus.SFX, true);
            Fader.I.SetColor(from);
            Fader.I.FadeTo(0f, 0.9f);
            const float Length = 1.4f;
            int thuds = 0;
            for (float t = 0f; t < Length; t += Time.deltaTime)
            {
                guard.Beat = Time.time;
                guard.Dim(Mathf.Lerp(0.15f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.1f) / 1.1f)))); // its lamps come up
                PostFx.I?.SetShift((1f - t / Length) * 0.6f);
                PostFx.I?.SetInhale(Mathf.Clamp01(1f - t / 0.5f) * 0.5f);  // the dark lifts first
                ring.Step(t);
                foreach (var s in settling)
                {
                    s.Step(feet, t, Time.deltaTime);
                    if (s.JustLanded && thuds < 3) { thuds++; AudioDirector.I?.Play("drop_soft", s.T.position, 0.25f, Random.Range(1.2f, 1.5f)); }
                }
                yield return null;
            }
            guard.Restore();
            Object.Destroy(root.gameObject);
        }

        // ------------------------------------------------------------------ guard
        /// <summary>
        /// Lives on the shift's root. Dims and restores the cell's lights, shows the hidden originals again, and destroys
        /// what the shift made. If the coroutine stops beating (the player quits to the menu mid-shift), it puts the lights,
        /// the props, the grade and the fade colour back, so nothing is left behind.
        /// </summary>
        class Guard : MonoBehaviour
        {
            public float Beat;
            public readonly List<Renderer> Hidden = new List<Renderer>();
            readonly List<Light> m_Lights = new List<Light>();
            readonly List<float> m_Base = new List<float>();
            readonly List<Object> m_Temp = new List<Object>();
            bool m_Restored;

            public void Track(Cell cell)
            {
                if (cell == null) return;
                foreach (var l in cell.GetComponentsInChildren<Light>())
                {
                    m_Lights.Add(l);
                    m_Base.Add(l.intensity);
                }
            }

            /// <summary>The cell's lights at a share of their own levels.</summary>
            public void Dim(float share)
            {
                for (int i = 0; i < m_Lights.Count; i++)
                    if (m_Lights[i] != null) m_Lights[i].intensity = m_Base[i] * share;
            }

            /// <summary>A material or mesh made for this shift, destroyed with it.</summary>
            public T Temp<T>(T o) where T : Object { m_Temp.Add(o); return o; }

            public void Restore()
            {
                if (m_Restored) return;
                m_Restored = true;
                Dim(1f);
                foreach (var r in Hidden) if (r != null) r.enabled = true;
            }

            void Update()
            {
                // 2.5 s without a beat: the coroutine is gone (a hitch is never that long). The input lock is left alone:
                // returning to the menu resets it.
                if (Time.time - Beat < 2.5f || m_Restored) return;
                Restore();
                PageLight(1f);
                PostFx.I?.SetShift(0f);
                PostFx.I?.SetInhale(0f);
                if (Fader.I != null) Fader.I.SetColor(Color.black);
                Running = false;
                Phase = Stage.None;
                Destroy(gameObject);
            }

            void OnDestroy()
            {
                Restore();
                foreach (var o in m_Temp) if (o != null) Destroy(o);
            }
        }

        // ------------------------------------------------------------------ shared pieces
        static Mesh s_Unit, s_Cube, s_PageFront;
        const float PageW = 0.3f, PageH = 0.42f;

        /// <summary>A 1 m quad in local XY, seen from -Z, UVs 0..1 (scaled per use).</summary>
        static Mesh Unit => s_Unit != null ? s_Unit : s_Unit = MeshFactory.QuadMesh(1f, 1f, "ShiftQuad");

        static Mesh Cube
        {
            get
            {
                if (s_Cube != null) return s_Cube;
                var b = new MeshBuilder();
                b.AddBox(Vector3.zero, Vector3.one, 1f);
                return s_Cube = b.ToMesh("ShiftCube");
            }
        }

        /// <summary>The wall side of a page: UVs in metres, so the wall's own material keeps its scale on it.</summary>
        static Mesh PageFront
        {
            get
            {
                if (s_PageFront != null) return s_PageFront;
                var b = new MeshBuilder();
                b.AddQuad(new Vector3(PageW / 2f, -PageH / 2f, 0f), new Vector3(-PageW / 2f, -PageH / 2f, 0f), new Vector3(-PageW / 2f, PageH / 2f, 0f), new Vector3(PageW / 2f, PageH / 2f, 0f),
                    new Vector2(PageW, 0f), new Vector2(0f, 0f), new Vector2(0f, PageH), new Vector2(PageW, PageH));
                return s_PageFront = b.ToMesh("ShiftPageFront");
            }
        }

        static Transform Piece(string name, Transform parent, Mesh mesh, Material mat, Vector3 localPos, Quaternion localRot, Vector3 scale)
        {
            var go = MeshFactory.Make(name, mesh, mat, parent, localPos, localRot);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            return go.transform;
        }

        static Light PointLight(Transform parent, Vector3 at, Color color, float range)
        {
            var l = new GameObject("ShiftLight").AddComponent<Light>();
            l.transform.SetParent(parent, false);
            l.transform.position = at;
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.intensity = 0f;
            l.shadows = LightShadows.None;
            return l;
        }

        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        /// <summary>A wall or a building face, not a prop (tears and pages need a real surface).</summary>
        static bool Large(Collider c)
        {
            var s = c.bounds.size;
            return Mathf.Max(s.x, s.z) >= 1.4f || s.y >= 2f;
        }

        static bool Crowded(List<Vector3> used, Vector3 p, float apart)
        {
            foreach (var u in used) if ((u - p).sqrMagnitude < apart * apart) return true;
            return false;
        }

        // ------------------------------------------------------------------ loose: props and furniture that tear free
        /// <summary>
        /// A stand-in for one prop (all its renderers, moved as one): it rattles where it stands, tears free, lifts and
        /// circles the player, faster and faster. The original is only hidden, and shown again at the swap.
        /// </summary>
        class Loose
        {
            readonly Transform m_Pivot;
            readonly Vector3 m_Home, m_Spin;
            readonly float m_At, m_Lift, m_Orbit, m_Speed, m_Phase, m_HomeR, m_HomeY;
            float m_Angle, m_Free = -1f;

            Loose(Transform pivot, Vector3 centre, float at, float size)
            {
                m_Pivot = pivot;
                m_Home = pivot.position;
                m_At = at;
                var d = m_Home - centre;
                m_HomeY = d.y;
                d.y = 0f;
                m_HomeR = d.magnitude;
                m_Angle = Mathf.Atan2(d.z, d.x);
                bool big = size > 0.7f;
                m_Orbit = Mathf.Clamp(Mathf.Max(m_HomeR, 1.4f + size * 0.6f), 1.4f, 4.8f); // never within arm's reach
                m_Lift = big ? Random.Range(0.25f, 0.6f) : Random.Range(0.5f, 1.3f);
                m_Speed = (big ? Random.Range(0.25f, 0.5f) : Random.Range(0.4f, 0.9f)) * (Random.value < 0.5f ? -1f : 1f);
                m_Phase = Random.Range(0f, 10f);
                m_Spin = Random.onUnitSphere * (big ? Random.Range(15f, 45f) : Random.Range(50f, 150f));
            }

            public void Step(Vector3 centre, float k, float dt, float liftTime, bool rattle)
            {
                if (m_Free < 0f)
                {
                    if (k < m_At)
                    {
                        // It rattles where it stands, harder as its moment nears (about a centimetre at most).
                        float near = Mathf.Clamp01(1f - (m_At - k) / 0.15f);
                        if (!rattle || near <= 0f) return;
                        float a = near * near * 0.012f, s = Time.time * 22f;
                        m_Pivot.position = m_Home + new Vector3(Mathf.PerlinNoise(s, m_Phase) - 0.5f, Mathf.PerlinNoise(m_Phase, s) - 0.5f, Mathf.PerlinNoise(s + 7f, m_Phase) - 0.5f) * (2f * a);
                        return;
                    }
                    m_Free = 0f;
                }
                m_Free += dt;
                float u = Mathf.Clamp01(m_Free / liftTime);
                float e = 1f - (1f - u) * (1f - u);
                m_Angle += m_Speed * (0.3f + 2.4f * k * k) * e * dt;
                float r = Mathf.Lerp(m_HomeR, m_Orbit, e);
                float y = m_HomeY + m_Lift * e + Mathf.Sin(Time.time * 1.6f + m_Phase) * 0.05f * e;
                m_Pivot.position = centre + new Vector3(Mathf.Cos(m_Angle) * r, y, Mathf.Sin(m_Angle) * r);
                m_Pivot.Rotate(m_Spin * (e * dt), Space.World);
            }

            /// <summary>The props near the player: whole small models (a chair, a lamp, a stack of books) or single pieces.</summary>
            public static List<Loose> Gather(Cell cell, Vector3 centre, Vector3 look, int max, Guard guard)
            {
                var list = new List<Loose>();
                if (cell == null || max <= 0 || Game.Rig == null) return list;
                var rig = Game.Rig.transform;
                var passenger = Game.Passenger != null ? Game.Passenger.transform : null;
                var byParent = new Dictionary<Transform, List<MeshRenderer>>();
                foreach (var r in cell.GetComponentsInChildren<MeshRenderer>())
                {
                    if (!Eligible(r, centre, rig, passenger)) continue;
                    var key = r.transform.parent;
                    if (!byParent.TryGetValue(key, out var parts)) byParent[key] = parts = new List<MeshRenderer>();
                    parts.Add(r);
                }
                var found = new List<(List<MeshRenderer> parts, Bounds b)>();
                foreach (var kv in byParent)
                {
                    var b = kv.Value[0].bounds;
                    foreach (var r in kv.Value) b.Encapsulate(r.bounds);
                    if (kv.Key != cell.transform && kv.Value.Count > 1 && PropSized(b) && Near(b, centre)) found.Add((kv.Value, b));
                    else
                        foreach (var r in kv.Value)
                            if (PropSized(r.bounds) && Near(r.bounds, centre)) found.Add((new List<MeshRenderer> { r }, r.bounds));
                }
                // Nearest first, with a head start for what is in front of him (he sees it go).
                float Score(Bounds b)
                {
                    var d = b.center - centre;
                    d.y = 0f;
                    return d.magnitude - 1.5f * Mathf.Max(0f, Vector3.Dot(look, d.normalized));
                }
                found.Sort((x, y) => Score(x.b).CompareTo(Score(y.b)));
                for (int i = 0; i < found.Count && list.Count < max; i++)
                {
                    var (parts, b) = found[i];
                    var pivot = new GameObject("Loose").transform;
                    pivot.SetParent(guard.transform, false);
                    pivot.position = b.center;
                    foreach (var r in parts)
                    {
                        var copy = MeshFactory.Make(r.name, r.GetComponent<MeshFilter>().sharedMesh, null, pivot);
                        copy.transform.SetPositionAndRotation(r.transform.position, r.transform.rotation);
                        copy.transform.localScale = r.transform.lossyScale;
                        var mr = copy.GetComponent<MeshRenderer>();
                        mr.sharedMaterials = r.sharedMaterials;
                        mr.shadowCastingMode = r.shadowCastingMode;
                        r.enabled = false;
                        guard.Hidden.Add(r);
                    }
                    // Small things go first, furniture later, with some mixing.
                    float size = Mathf.Max(b.size.x, b.size.y, b.size.z);
                    float at = Mathf.Lerp(0.06f, 0.45f, Random.value * 0.6f + Mathf.Clamp01(size / 1.3f) * 0.4f);
                    list.Add(new Loose(pivot, centre, at, size));
                }
                return list;
            }

            static bool Eligible(MeshRenderer r, Vector3 centre, Transform rig, Transform passenger)
            {
                if (!r.enabled || r.isPartOfStaticBatch) return false;
                if (Flat(r.bounds.center - centre) > 6f) return false;
                int layer = r.gameObject.layer;
                if (layer != Layers.Default && layer != Layers.Interactable) return false;
                if (r.shadowCastingMode == ShadowCastingMode.ShadowsOnly) return false;
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) return false;
                var m = r.sharedMaterial;
                if (m == null || m.shader == null || m.shader.name.StartsWith("TwelfthStamp/") || m.renderQueue >= 2900) return false;
                if (r.transform.IsChildOf(rig) || (passenger != null && r.transform.IsChildOf(passenger))) return false;
                return r.GetComponentInParent<StampPortal>() == null && r.GetComponentInParent<PassengerBody>() == null && r.GetComponentInParent<LODGroup>() == null;
            }

            static bool PropSized(Bounds b)
            {
                var s = b.size;
                return Mathf.Max(s.x, s.z) <= 1.3f && s.y <= 1.9f && s.x * s.y * s.z <= 0.8f && s.sqrMagnitude >= 0.0025f;
            }

            static bool Near(Bounds b, Vector3 centre)
            {
                float d = Flat(b.center - centre);
                return d >= 0.6f && d <= 5.5f && b.min.y >= centre.y - 0.3f && b.center.y <= centre.y + 2.6f;
            }
        }

        // ------------------------------------------------------------------ debris: torn up from the floor, circling
        class Shard
        {
            public readonly Transform T;
            readonly Vector3 m_Size, m_Spin;
            readonly float m_At, m_Radius, m_Height, m_Speed, m_Lift, m_Wobble;
            float m_Angle;

            /// <summary>It has torn free and is in the air.</summary>
            public bool Up { get; private set; }

            public Shard(Transform t, Vector3 size, float at, float angle, float radius, float height)
            {
                T = t; m_Size = size; m_At = at; m_Angle = angle; m_Radius = radius; m_Height = height;
                m_Speed = Random.Range(0.35f, 1.1f) * (Random.value < 0.5f ? -1f : 1f); // both ways round
                m_Lift = Random.Range(0.4f, 1.6f);
                m_Wobble = Random.Range(0f, 10f);
                m_Spin = Random.onUnitSphere * Random.Range(60f, 220f);
            }

            public void Step(Vector3 centre, float k, float dt)
            {
                float u = Mathf.Clamp01((k - m_At) / 0.25f);
                if (u <= 0f) return;
                Up = true;
                float e = 1f - (1f - u) * (1f - u);
                m_Angle += m_Speed * (0.15f + 2.6f * k * k) * dt;      // faster and faster
                float r = m_Radius + Mathf.Sin(Time.time * 1.3f + m_Wobble) * 0.25f * k;
                float y = Mathf.Lerp(0.03f, m_Height + m_Lift * k, e) + Mathf.Sin(Time.time * 2f + m_Wobble) * 0.08f * e;
                T.position = centre + new Vector3(Mathf.Cos(m_Angle) * r, y, Mathf.Sin(m_Angle) * r);
                T.localScale = m_Size * Mathf.Min(1f, u * 2.5f);
                T.Rotate(m_Spin * (k * dt), Space.Self);
            }
        }

        static Material Dark => Mats.Lit(new Color(0.07f, 0.06f, 0.06f), 0.6f, 0.2f, null, false, new Color(0.05f, 0.045f, 0.04f)); // faintly lit edges (no red ember)
        static Material Pale => Mats.Lit(new Color(0.55f, 0.52f, 0.48f), 0.3f, 0f, "plaster_warm");

        static List<Shard> Debris(Transform root, int count)
        {
            var list = new List<Shard>();
            for (int i = 0; i < count; i++)
            {
                var size = new Vector3(Random.Range(0.08f, 0.5f), Random.Range(0.015f, 0.05f), Random.Range(0.06f, 0.4f));
                var t = Piece("Shard", root, Cube, Random.value < 0.55f ? Dark : Pale, Vector3.zero, Random.rotation, Vector3.zero);
                // Never inside arm's reach of the face.
                list.Add(new Shard(t, size, Random.Range(0.02f, 0.6f), Random.Range(0f, Mathf.PI * 2f), Random.Range(1.3f, 4.5f), Random.Range(0.1f, 2.2f)));
            }
            return list;
        }

        // ------------------------------------------------------------------ tears: cracks with the other place's light
        static Texture2D s_Shaft;

        /// <summary>Light shaft: bright where it leaves the crack, gone at its far end, soft across.</summary>
        static Texture2D ShaftTex
        {
            get
            {
                if (s_Shaft != null) return s_Shaft;
                const int w = 32, h = 64;
                s_Shaft = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "TS_ShiftShaft", wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float fy = (y + 0.5f) / h, fx = Mathf.Abs((x + 0.5f) / w * 2f - 1f);
                        float along = Mathf.Pow(1f - fy, 1.6f) * Mathf.SmoothStep(0f, 1f, fy / 0.06f);
                        float across = (1f - fx * fx) * (1f - fx * fx);
                        s_Shaft.SetPixel(x, y, new Color(1f, 1f, 1f, along * across));
                    }
                s_Shaft.Apply();
                return s_Shaft;
            }
        }

        class WallTear
        {
            public readonly float At;
            public readonly Vector3 Point;
            public bool Heard;
            readonly Transform m_T;
            readonly Vector3 m_Full;
            readonly Material m_Core, m_Glow, m_Shaft, m_Pool;
            readonly Light m_Lamp;
            public bool HasShaft => m_Shaft != null;

            public WallTear(Transform root, RaycastHit hit, float at, Color light, Material black, bool lamp, Guard guard)
            {
                At = at;
                Point = hit.point;
                var n = hit.normal;
                m_T = new GameObject("WallTear").transform;
                m_T.SetParent(root, true);
                m_T.position = hit.point + n * 0.02f;
                m_T.rotation = Quaternion.LookRotation(-n, Vector3.up) * Quaternion.Euler(0f, 0f, Random.Range(-20f, 20f));
                float w = Random.Range(0.25f, 0.6f), h = Random.Range(1.1f, 2.2f);
                m_Full = new Vector3(w, h, 1f);
                m_T.localScale = Vector3.zero;

                // Behind to front: the glowing rim, the black split, the other place's light in the split (no red rim since
                // the playtest of 18 Oct: the rim is the same light, dimmer).
                m_Glow = guard.Temp(new Material(Mats.Particle(Tex.SoftDot, true, new Color(light.r * 0.8f, light.g * 0.8f, light.b * 0.8f), 1.2f)) { name = "ShiftTearGlow", renderQueue = 3000 });
                m_Core = guard.Temp(new Material(Mats.Particle(Tex.TearMask, true, light, 0f)) { name = "ShiftTearCore", renderQueue = 3002 });
                Piece("Glow", m_T, Unit, m_Glow, new Vector3(0f, 0f, 0.01f), Quaternion.identity, new Vector3(2.4f, 1.5f, 1f));
                Piece("Split", m_T, Unit, black, Vector3.zero, Quaternion.identity, Vector3.one);
                Piece("Light", m_T, Unit, m_Core, new Vector3(0f, 0f, -0.004f), Quaternion.identity, new Vector3(0.55f, 0.9f, 1f));

                // The shaft: from the crack into the room and down, ending where it lands; a pool of light there.
                var dir = (n - Vector3.up * 0.55f + Random.insideUnitSphere * 0.12f).normalized;
                var from = hit.point + n * 0.03f;
                float len = 3.2f;
                Vector3? pool = null;
                if (Physics.Raycast(from, dir, out var land, 6f, Layers.WorldMask, QueryTriggerInteraction.Ignore))
                {
                    len = Mathf.Max(0.8f, land.distance);
                    if (land.normal.y > 0.6f) pool = land.point + land.normal * 0.02f;
                }
                m_Shaft = guard.Temp(new Material(Mats.Particle(ShaftTex, true, light, 0f)) { name = "ShiftShaft", renderQueue = 3003 });
                m_Shaft.SetVector("_NearFade", new Vector4(0.4f, 0.8f, 0f, 0f));
                var b = new MeshBuilder();
                var side = Vector3.Cross(dir, Vector3.up);
                side = side.sqrMagnitude > 0.01f ? side.normalized : Vector3.right;
                var cross = Vector3.Cross(side, dir).normalized;
                float w0 = w * 0.7f, w1 = Random.Range(0.8f, 1.2f);
                var o = from - root.position; // the mesh sits at the shift's root
                foreach (var s in new[] { side, cross })
                {
                    var e = o + dir * len;
                    b.AddQuad(o - s * w0 / 2f, e - s * w1 / 2f, e + s * w1 / 2f, o + s * w0 / 2f, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f));
                }
                var shaft = MeshFactory.FromBuilder("Shaft", b, m_Shaft, root);
                shaft.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                guard.Temp(shaft.GetComponent<MeshFilter>().sharedMesh);
                if (pool.HasValue)
                {
                    m_Pool = guard.Temp(new Material(Mats.Particle(Tex.SoftDot, true, light, 0f)) { name = "ShiftPool", renderQueue = 3003 });
                    var flat = Vector3.ProjectOnPlane(dir, Vector3.up);
                    if (flat.sqrMagnitude < 0.01f) flat = Vector3.forward;
                    var p = Piece("Pool", root, Unit, m_Pool, Vector3.zero, Quaternion.identity, new Vector3(0.9f, 1.5f, 1f));
                    p.SetPositionAndRotation(pool.Value, Quaternion.LookRotation(Vector3.down, flat.normalized));
                }
                if (lamp)
                {
                    m_Lamp = PointLight(root, hit.point + n * 0.45f, light, 5f);
                }
            }

            /// <summary>k: the build-up; blaze: the inhale (0..1); burst: the slam (0..1).</summary>
            public void Step(float k, float blaze, float burst, float pulse)
            {
                float g = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((k - At) / 0.4f));
                m_T.localScale = new Vector3(m_Full.x * (0.3f + 0.7f * g) * (1f + 0.8f * burst), m_Full.y * g * (1f + 0.25f * burst), 1f);
                float open = g * g;
                m_Core.SetFloat("_Intensity", 4f * open * (1f + 1.5f * blaze) * (1f + 1.2f * burst));
                m_Glow.SetFloat("_Intensity", 1.2f * g * (1f - 0.6f * open) * (0.75f + 0.25f * pulse) * (1f - 0.5f * blaze));
                float s = Mathf.Clamp01((g - 0.15f) / 0.6f);
                s *= s;
                if (m_Shaft != null) m_Shaft.SetFloat("_Intensity", 1.2f * s * (1f + 1.3f * blaze) * (1f + burst));
                if (m_Pool != null) m_Pool.SetFloat("_Intensity", 1.8f * s * (1f + blaze) * (1f + burst));
                if (m_Lamp != null) m_Lamp.intensity = 3.4f * open * (1f + 0.9f * blaze) * (1f + burst);
            }
        }

        /// <summary>A direction to look for a surface: three in four within about 70 degrees of where he is looking.</summary>
        static float Bearing(Vector3 look)
            => Random.value < 0.75f ? Mathf.Atan2(look.z, look.x) + Random.Range(-1.2f, 1.2f) : Random.Range(0f, Mathf.PI * 2f);

        static List<WallTear> Tears(Transform root, Transform head, Vector3 look, int count, Color light, int lamps, Guard guard, List<Vector3> used)
        {
            var list = new List<WallTear>();
            var black = guard.Temp(Mats.UnlitInstance(new Color(0.01f, 0f, 0.005f, 1f), Tex.TearMask, true, 1f));
            black.renderQueue = 3001;
            int tries = 0;
            while (list.Count < count && tries++ < count * 8)
            {
                float a = Bearing(look);
                var dir = new Vector3(Mathf.Cos(a), Random.Range(-0.15f, 0.25f), Mathf.Sin(a)).normalized;
                if (!Physics.Raycast(head.position, dir, out var hit, 7f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) continue;
                if (hit.distance < 1.2f || Mathf.Abs(hit.normal.y) > 0.4f || hit.collider.gameObject.layer == Layers.Passenger || !Large(hit.collider)) continue;
                if (Crowded(used, hit.point, 0.9f)) continue;
                used.Add(hit.point);
                float at = 0.03f + 0.42f * list.Count / Mathf.Max(1, count - 1); // they open one after another
                list.Add(new WallTear(root, hit, at, light, black, list.Count < lamps, guard));
            }
            return list;
        }

        // ------------------------------------------------------------------ pages: the walls peel away
        static Material[] s_PageArt;
        static readonly Color PageTint = new Color(0.95f, 0.9f, 0.82f, 1f);

        /// <summary>Passport pages, postcards and notes: the back of each peeled sheet (unlit, seen from both sides).</summary>
        static Material[] PageArt
        {
            get
            {
                if (s_PageArt != null) return s_PageArt;
                var list = new List<Material>();
                foreach (var n in new[] { "passport_insert_page", "passport_insert_page", "passport_id_page", "passport_insert_page", "postcard_1", "postcard_2", "postcard_3", "postcard_4", "optician_notes", "ui_panel_paper" })
                {
                    var tex = Tex.Art(n);
                    if (tex == null) continue;
                    var m = new Material(Mats.Particle(tex, false, PageTint, 1.05f)) { name = "ShiftPage_" + n };
                    m.SetVector("_NearFade", new Vector4(0.35f, 0.5f, 0f, 0f));
                    list.Add(m);
                }
                if (list.Count == 0) list.Add(new Material(Mats.Particle(Tex.White, false, PageTint, 0.6f)) { name = "ShiftPage" });
                return s_PageArt = list.ToArray();
            }
        }

        /// <summary>The pages' share of their light (they dim with the room in the inhale).</summary>
        static void PageLight(float share)
        {
            if (s_PageArt == null) return;
            foreach (var m in s_PageArt) if (m != null) m.SetColor("_TintColor", new Color(PageTint.r * share, PageTint.g * share, PageTint.b * share, 1f));
        }

        static Material SurfaceOf(Collider c)
        {
            var r = c.GetComponent<Renderer>();
            var m = r != null ? r.sharedMaterial : null;
            if (m != null && m.shader != null && !m.shader.name.StartsWith("TwelfthStamp/") && m.renderQueue < 2900) return m;
            return Pale;
        }

        class Page
        {
            public readonly Vector3 Point;
            public bool JustStarted { get; private set; }
            readonly Transform m_Hinge, m_Sheet, m_Patch;
            readonly Quaternion m_Rest;
            readonly Vector3 m_Spin;
            readonly float m_At, m_PeelTime, m_Rise, m_Speed, m_Target, m_Phase, m_Ceiling;
            float m_Peel = -1f, m_Angle, m_R, m_Y, m_Fly;
            bool m_Free;

            /// <summary>A sheet of the surface at <paramref name="point"/>: hinged at its top edge, the wall's own material
            /// facing out, a page on its back.</summary>
            public Page(Transform root, Vector3 point, Vector3 normal, Vector3 upRef, Material front, Material art, Material dark, Material glow, float at, float peelTime, float ceiling)
            {
                Point = point;
                m_At = at;
                m_PeelTime = peelTime;
                m_Ceiling = ceiling;
                var tex = art.mainTexture;
                bool wide = tex != null && tex.width > tex.height * 1.15f;
                float w = (wide ? PageH : PageW) * 1.3f, h = (wide ? PageW : PageH) * 1.3f; // large sheets: they read across the room
                m_Rest = Quaternion.LookRotation(-normal, upRef);
                m_Hinge = new GameObject("Page").transform;
                m_Hinge.SetParent(root, true);
                m_Hinge.SetPositionAndRotation(point + normal * 0.012f + m_Rest * Vector3.up * (h / 2f), m_Rest);
                m_Sheet = new GameObject("Sheet").transform;
                m_Sheet.SetParent(m_Hinge, false);
                m_Sheet.localPosition = new Vector3(0f, -h / 2f, 0f);
                Piece("Face", m_Sheet, PageFront, front, Vector3.zero, Quaternion.identity, new Vector3(w / PageW, h / PageH, 1f));
                // Faces the wall, upside down, so it reads the right way up once the sheet has turned over.
                Piece("Back", m_Sheet, Unit, art, new Vector3(0f, 0f, 0.003f), Quaternion.Euler(0f, 180f, 180f), new Vector3(w, h, 1f));

                // The patch it bares: dark, with the other place's light coming through.
                m_Patch = new GameObject("Bared").transform;
                m_Patch.SetParent(root, true);
                m_Patch.SetPositionAndRotation(point + normal * 0.006f, m_Rest);
                Piece("Dark", m_Patch, Unit, dark, Vector3.zero, Quaternion.identity, new Vector3(w * 0.92f, h * 0.92f, 1f));
                Piece("Glow", m_Patch, Unit, glow, new Vector3(0f, 0f, -0.003f), Quaternion.identity, new Vector3(w * 0.85f, h * 0.85f, 1f));
                m_Patch.localScale = Vector3.zero;

                m_Speed = Random.Range(0.9f, 1.8f) * (Random.value < 0.7f ? 1f : -1f);
                m_Rise = Random.Range(0.35f, 0.8f);
                m_Target = Random.Range(1.6f, 2.8f);
                m_Phase = Random.Range(0f, 10f);
                m_Spin = Random.onUnitSphere * Random.Range(50f, 140f);
            }

            public void Step(Vector3 centre, float k, float dt)
            {
                JustStarted = false;
                if (m_Peel < 0f)
                {
                    if (k < m_At) return;
                    m_Peel = 0f;
                    JustStarted = true;
                }
                if (!m_Free)
                {
                    // Peels away from the bottom edge and turns over, baring the patch behind it.
                    m_Peel += dt;
                    float u = Mathf.Clamp01(m_Peel / m_PeelTime);
                    m_Hinge.rotation = m_Rest * Quaternion.Euler(150f * u * u * (3f - 2f * u), 0f, 0f);
                    m_Patch.localScale = Vector3.one * Mathf.Clamp01(u * 1.6f);
                    if (u < 1f) return;
                    m_Free = true;
                    m_Sheet.SetParent(m_Hinge.parent, true);
                    var d = m_Sheet.position - centre;
                    m_Angle = Mathf.Atan2(d.z, d.x);
                    m_Y = m_Sheet.position.y;
                    d.y = 0f;
                    m_R = d.magnitude;
                }
                // Then it spirals up round him, fluttering, and is gone into the dark overhead.
                m_Fly += dt;
                m_Angle += m_Speed * (0.5f + k) * dt;
                m_R = Mathf.MoveTowards(m_R, m_Target, dt * 0.9f);
                m_Y += m_Rise * (0.4f + k) * dt;
                float flutter = Mathf.Sin(m_Fly * 3.1f + m_Phase) * 0.06f * Mathf.Clamp01(m_Fly * 3f);
                m_Sheet.position = new Vector3(centre.x + Mathf.Cos(m_Angle) * m_R, m_Y + flutter, centre.z + Mathf.Sin(m_Angle) * m_R);
                m_Sheet.Rotate(m_Spin * dt, Space.Self);
                float keep = Mathf.Clamp01((m_Ceiling - 0.25f - m_Y) / 0.5f) * Mathf.Clamp01(4.5f - m_Fly);
                m_Sheet.localScale = Vector3.one * keep;
            }
        }

        static List<Page> Pages(Transform root, Transform head, Vector3 centre, Vector3 look, int count, Color light, float build, Guard guard, List<Vector3> used)
        {
            var list = new List<Page>();
            if (count <= 0) return list;
            float ceiling = Physics.Raycast(head.position, Vector3.up, out var top, 8f, Layers.WorldMask, QueryTriggerInteraction.Ignore) ? top.point.y : head.position.y + 4f;
            var art = PageArt;
            PageLight(1f);
            var dark = Mats.Unlit(new Color(0.015f, 0.005f, 0.008f));
            var glow = guard.Temp(new Material(Mats.Particle(Tex.SoftDot, true, light, 1.3f)) { name = "ShiftBared" });
            float last = build >= 3f ? 0.68f : 0.55f, peel = Mathf.Clamp(build * 0.2f, 0.25f, 0.6f);
            float At() => Mathf.Lerp(0.08f, last, list.Count / (float)Mathf.Max(1, count - 1)) + Random.Range(-0.03f, 0.03f);

            // Off the walls first.
            int tries = 0;
            while (list.Count < count && tries++ < count * 8)
            {
                float a = Bearing(look);
                var dir = new Vector3(Mathf.Cos(a), Random.Range(-0.3f, 0.35f), Mathf.Sin(a)).normalized;
                if (!Physics.Raycast(head.position, dir, out var hit, 6.5f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) continue;
                if (hit.distance < 1.1f || Mathf.Abs(hit.normal.y) > 0.35f || !Large(hit.collider) || hit.collider.GetComponent<Renderer>() == null) continue;
                if (hit.collider.gameObject.layer == Layers.Passenger || Crowded(used, hit.point, 0.55f)) continue;
                used.Add(hit.point);
                list.Add(new Page(root, hit.point, hit.normal, Vector3.up, SurfaceOf(hit.collider), art[Random.Range(0, art.Length)], dark, glow, At(), peel, ceiling));
            }
            // Out in the open, the rest lift off the ground round him.
            tries = 0;
            while (list.Count < count && tries++ < count * 6)
            {
                float a = Bearing(look), r = Random.Range(1.6f, 4.5f);
                var from = new Vector3(centre.x + Mathf.Cos(a) * r, head.position.y, centre.z + Mathf.Sin(a) * r);
                if (!Physics.Raycast(from, Vector3.down, out var hit, 4f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) continue;
                if (hit.normal.y < 0.7f || hit.collider.GetComponent<Renderer>() == null || Crowded(used, hit.point, 0.55f)) continue;
                used.Add(hit.point);
                var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                list.Add(new Page(root, hit.point, hit.normal, outward, SurfaceOf(hit.collider), art[Random.Range(0, art.Length)], dark, glow, At(), peel, ceiling));
            }
            return list;
        }

        // ------------------------------------------------------------------ arrival pieces
        /// <summary>Debris that came through: it winds down its circling, falls, lands flat and sinks into the new floor.</summary>
        class Settler
        {
            public readonly Transform T;
            public bool JustLanded { get; private set; }
            readonly Vector3 m_Size;
            readonly Quaternion m_From, m_To;
            readonly float m_Y0, m_Floor, m_Delay, m_Fall, m_R, m_W;
            float m_Angle;
            bool m_Landed;

            public Settler(Transform t, Vector3 size, Vector3 centre, Vector3 offset, float w)
            {
                T = t;
                m_Size = size;
                m_W = w;
                m_Y0 = centre.y + offset.y;
                offset.y = 0f;
                m_R = Mathf.Max(1.2f, offset.magnitude);
                m_Angle = Mathf.Atan2(offset.z, offset.x);
                m_Delay = Random.Range(0f, 0.25f);
                m_Fall = Random.Range(0.4f, 0.7f);
                // Where it comes down: the circling winds down over 0.8 s (w * (s - s^2 / 1.6) further round by time s).
                float s = Mathf.Min(0.8f, m_Delay + m_Fall);
                float land = m_Angle + w * (s - s * s / 1.6f);
                var above = new Vector3(centre.x + Mathf.Cos(land) * m_R, m_Y0 + 0.2f, centre.z + Mathf.Sin(land) * m_R);
                float floor = Physics.Raycast(above, Vector3.down, out var hit, 8f, Layers.WorldMask, QueryTriggerInteraction.Ignore) ? hit.point.y : centre.y;
                m_Floor = Mathf.Min(m_Y0, floor + size.y / 2f);
                m_From = t.rotation;
                m_To = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            }

            public void Step(Vector3 centre, float t, float dt)
            {
                JustLanded = false;
                m_Angle += m_W * Mathf.Clamp01(1f - t / 0.8f) * dt;
                float u = Mathf.Clamp01((t - m_Delay) / m_Fall);
                T.position = new Vector3(centre.x + Mathf.Cos(m_Angle) * m_R, Mathf.Lerp(m_Y0, m_Floor, u * u), centre.z + Mathf.Sin(m_Angle) * m_R);
                T.rotation = Quaternion.Slerp(m_From, m_To, Mathf.SmoothStep(0f, 1f, u));
                if (u >= 1f && !m_Landed) { m_Landed = true; JustLanded = true; }
                float sink = Mathf.Clamp01((t - m_Delay - m_Fall - 0.12f) / 0.3f);
                T.localScale = m_Size * (1f - sink);
            }
        }

        static List<Settler> Settle(Transform root, Vector3 centre, List<Vector3> carry, float motion)
        {
            var list = new List<Settler>();
            foreach (var off in carry)
            {
                var size = new Vector3(Random.Range(0.08f, 0.45f), Random.Range(0.015f, 0.05f), Random.Range(0.06f, 0.35f));
                var t = Piece("Settling", root, Cube, Random.value < 0.7f ? Dark : Pale, Vector3.zero, Random.rotation, size);
                t.position = centre + off;
                float w = Random.Range(0.5f, 1.4f) * (Random.value < 0.5f ? -1f : 1f) * motion;
                list.Add(new Settler(t, size, centre, off, w));
            }
            return list;
        }

        /// <summary>A ring of the new place's light running out across its ground.</summary>
        class Ring
        {
            readonly Transform m_T;
            readonly Material m_Mat;
            readonly Color m_Color;

            public Ring(Transform root, Vector3 at, Color light, float strength, Guard guard)
            {
                m_Color = light * strength;
                m_Mat = guard.Temp(new Material(Mats.Particle(Tex.Ring, true, Color.white, 1.6f)) { name = "ShiftArrivalRing" });
                m_Mat.SetVector("_NearFade", new Vector4(0.2f, 0.4f, 0f, 0f));
                m_T = Piece("ArrivalRing", root, Unit, m_Mat, Vector3.zero, Quaternion.Euler(90f, 0f, 0f), Vector3.zero);
                m_T.position = at;
            }

            public void Step(float t)
            {
                float f = Mathf.Clamp01((t - 0.12f) / 1.05f);
                float r = Mathf.Lerp(0.6f, 7f, 1f - (1f - f) * (1f - f));
                m_T.localScale = new Vector3(r * 2f, r * 2f, 1f);
                m_Mat.SetColor("_TintColor", new Color(m_Color.r, m_Color.g, m_Color.b, Mathf.Sin(f * Mathf.PI)));
            }
        }

        /// <summary>Dust drifting down through the new place's light (it outlives the shift by a couple of seconds).</summary>
        static void Dust(Vector3 head, Color light, int count)
        {
            var go = new GameObject("ShiftDust");
            go.transform.position = head + Vector3.up * 0.9f;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 1f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3.2f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.03f);
            main.startColor = new Color(light.r, light.g, light.b, 0.9f);
            main.maxParticles = count;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;
            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(7f, 1.6f, 7f);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f);
            vel.y = new ParticleSystem.MinMaxCurve(-0.45f, -0.15f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            col.color = fade;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = Mats.Particle(Tex.SoftDot, true, Color.white, 1.6f, Mats.StencilNotEqual);
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.maxParticleSize = 0.04f;
            ps.Play();
            Object.Destroy(go, 3.6f);
        }

        // ------------------------------------------------------------------ tunnel (desktop only, at most 1.5 s)
        static Texture2D s_Dashes;

        /// <summary>Soft dashes that repeat along v (the tunnel streaks scroll through them).</summary>
        static Texture2D Dashes
        {
            get
            {
                if (s_Dashes != null) return s_Dashes;
                const int w = 16, h = 64;
                s_Dashes = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "TS_ShiftDashes", wrapMode = TextureWrapMode.Repeat };
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float dx = Mathf.Abs((x + 0.5f) / w * 2f - 1f);
                        float fy = (y + 0.5f) / h;
                        float dash = Mathf.Clamp01(Mathf.Sin(fy * Mathf.PI * 2f) * 1.6f);
                        s_Dashes.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - dx * 1.8f) * dash));
                    }
                s_Dashes.Apply();
                return s_Dashes;
            }
        }

        class Tunnel
        {
            readonly GameObject m_Go;
            readonly Material m_Mat;
            readonly Color m_Color;
            readonly float m_Open, m_Span;
            float m_Scroll;

            public Tunnel(Transform root, Transform head, float build, Color color, Guard guard)
            {
                // A ring of streaks along the view, fixed in the world (it never follows the head), opening round the room
                // instead of replacing it: the floor and walls stay visible between the streaks as a stable frame. Open for
                // the last 1.5 s of the build-up (a little over half of a short one).
                m_Span = Mathf.Min(1.5f, build * 0.55f);
                m_Open = build - m_Span;
                m_Color = color;
                m_Mat = guard.Temp(new Material(Mats.Particle(Dashes, true, color, 1.4f)) { name = "ShiftTunnel" });
                m_Mat.mainTextureScale = new Vector2(1f, 2f);
                var b = new MeshBuilder();
                const int Streaks = 40;
                var fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
                if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
                var right = Vector3.Cross(Vector3.up, fwd);
                var origin = head.position + fwd * 0.9f - root.position; // the mesh sits at the shift's root
                for (int i = 0; i < Streaks; i++)
                {
                    float a = i / (float)Streaks * Mathf.PI * 2f + Random.Range(-0.05f, 0.05f);
                    float r = Random.Range(1.2f, 2f);
                    var o = (right * Mathf.Cos(a) + Vector3.up * Mathf.Sin(a)) * r;
                    var w = (right * -Mathf.Sin(a) + Vector3.up * Mathf.Cos(a)) * Random.Range(0.05f, 0.14f);
                    float len = Random.Range(4f, 9f);
                    var p = origin + o;
                    b.AddQuad(p - w, p - w + fwd * len, p + w + fwd * len, p + w, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f));
                }
                m_Go = MeshFactory.FromBuilder("ShiftTunnel", b, m_Mat, root);
                m_Go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                guard.Temp(m_Go.GetComponent<MeshFilter>().sharedMesh);
                m_Go.SetActive(false);
            }

            public void Step(float t, float dt)
            {
                bool on = t >= m_Open;
                if (m_Go.activeSelf != on) m_Go.SetActive(on);
                if (!on) return;
                float f = Mathf.Clamp01((t - m_Open) / m_Span);
                m_Scroll += dt * (1.5f + f * 2.5f); // dashes rush toward the viewer
                m_Mat.mainTextureOffset = new Vector2(0f, m_Scroll);
                m_Mat.SetColor("_TintColor", new Color(m_Color.r, m_Color.g, m_Color.b, 1f) * Mathf.Sin(f * Mathf.PI));
            }
        }
    }
}
