using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// Per-place reverb. One AudioReverbZone rides on the listener and its parameters glide between presets chosen by
    /// the active cell: small room (apartment, menu, home), tiled shop, the souk's alley, streets with facade
    /// reflections (Paris, Japan, London), hall (Berlin pavilion, New York terminal), courtyard, plaza, open dry desert,
    /// garden (Agra), arena (Rio), the Margin's long dark tail (corrupted cells), and an unreal
    /// swelling space while a dimension shift runs. Cells that mix inside and outside listen with a cheap probe (one
    /// horizontal ray a frame round the head, 8 directions, 30 m): no open directions reads as enclosed, a couple as a
    /// street, most as open ground. Wet levels stay -11..-30 dB and early reflections short (VR comfort). Music, UI,
    /// beds and inner-voice narration bypass the zone (AudioDirector).
    /// </summary>
    public class Acoustics : MonoBehaviour
    {
        struct Preset
        {
            public float Room, RoomHF, Decay, DecayHF, Refl, ReflDelay, Rev, RevDelay, Diffusion, Density;

            public Preset(float room, float roomHF, float decay, float decayHF, float refl, float reflDelay, float rev, float revDelay,
                          float diffusion, float density)
            {
                Room = room; RoomHF = roomHF; Decay = decay; DecayHF = decayHF; Refl = refl; ReflDelay = reflDelay;
                Rev = rev; RevDelay = revDelay; Diffusion = diffusion; Density = density;
            }

            public static Preset Mix(Preset a, float wa, Preset b, float wb, Preset c, float wc)
            {
                float s = Mathf.Max(1e-4f, wa + wb + wc);
                wa /= s; wb /= s; wc /= s;
                return new Preset(a.Room * wa + b.Room * wb + c.Room * wc, a.RoomHF * wa + b.RoomHF * wb + c.RoomHF * wc,
                    a.Decay * wa + b.Decay * wb + c.Decay * wc, a.DecayHF * wa + b.DecayHF * wb + c.DecayHF * wc,
                    a.Refl * wa + b.Refl * wb + c.Refl * wc, a.ReflDelay * wa + b.ReflDelay * wb + c.ReflDelay * wc,
                    a.Rev * wa + b.Rev * wb + c.Rev * wc, a.RevDelay * wa + b.RevDelay * wb + c.RevDelay * wc,
                    a.Diffusion * wa + b.Diffusion * wb + c.Diffusion * wc, a.Density * wa + b.Density * wb + c.Density * wc);
            }

            public static Preset Lerp(Preset a, Preset b, float t) => Mix(a, 1f - t, b, t, b, 0f);

            public bool Near(Preset o) =>
                Mathf.Abs(Room - o.Room) < 15f && Mathf.Abs(RoomHF - o.RoomHF) < 15f && Mathf.Abs(Decay - o.Decay) < 0.02f &&
                Mathf.Abs(Refl - o.Refl) < 15f && Mathf.Abs(Rev - o.Rev) < 15f && Mathf.Abs(DecayHF - o.DecayHF) < 0.01f &&
                Mathf.Abs(ReflDelay - o.ReflDelay) < 0.002f && Mathf.Abs(RevDelay - o.RevDelay) < 0.002f &&
                Mathf.Abs(Diffusion - o.Diffusion) < 1f && Mathf.Abs(Density - o.Density) < 1f;
        }

        //                                     room   roomHF decay  HFrat  refl   reflDl  rev    revDl  diff  dens
        static readonly Preset Room =      new Preset(-1300, -600, 0.5f, 0.6f, -700, 0.004f, -350, 0.006f, 85, 90);
        static readonly Preset TileRoom =  new Preset(-1200, -350, 0.75f, 0.75f, -500, 0.003f, -250, 0.005f, 85, 90);
        static readonly Preset Alley =     new Preset(-1500, -700, 0.9f, 0.6f, -650, 0.006f, -1000, 0.012f, 55, 80);
        static readonly Preset Street =    new Preset(-1700, -800, 1.15f, 0.55f, -900, 0.012f, -1500, 0.02f, 45, 70);
        static readonly Preset Riverside = new Preset(-1900, -900, 1.4f, 0.5f, -1300, 0.02f, -1500, 0.03f, 45, 65);
        static readonly Preset Harbour =   new Preset(-2100, -1000, 1.4f, 0.5f, -1900, 0.03f, -1600, 0.04f, 40, 60);
        static readonly Preset Hall =      new Preset(-1500, -500, 2.2f, 0.6f, -1200, 0.022f, -500, 0.03f, 100, 100);
        static readonly Preset Courtyard = new Preset(-1900, -900, 0.9f, 0.6f, -1000, 0.015f, -1400, 0.02f, 50, 70);
        static readonly Preset Plaza =     new Preset(-2300, -1100, 1.2f, 0.5f, -2100, 0.045f, -1900, 0.05f, 30, 60);
        static readonly Preset Dry =       new Preset(-3000, -1500, 0.5f, 0.5f, -3000, 0.04f, -3000, 0.04f, 30, 50);
        static readonly Preset Garden =    new Preset(-2500, -1800, 1.1f, 0.45f, -2300, 0.06f, -1800, 0.045f, 70, 60);
        static readonly Preset Arena =     new Preset(-1700, -900, 3.0f, 0.45f, -1600, 0.04f, -900, 0.05f, 80, 90);
        static readonly Preset Margin =    new Preset(-1500, -1100, 4.5f, 0.35f, -1600, 0.02f, -300, 0.07f, 100, 60);
        static readonly Preset Shift =     new Preset(-1100, -1000, 7.0f, 0.4f, -1200, 0.03f, 0, 0.08f, 100, 50);

        AudioReverbZone m_Zone;
        Preset m_Cur = Room, m_Applied;
        bool m_HasApplied;
        Cell m_Cell;
        float m_Snap, m_Open = 1f, m_ShiftW, m_Spin;
        readonly float[] m_Blocked = new float[8];
        int m_Ray;

        void Awake()
        {
            m_Zone = gameObject.AddComponent<AudioReverbZone>();
            m_Zone.minDistance = 900f; // the listener is always deep inside: full effect everywhere
            m_Zone.maxDistance = 1000f;
            m_Zone.reverbPreset = AudioReverbPreset.User;
            m_Zone.HFReference = 5000f;
            m_Zone.LFReference = 250f;
            m_Zone.roomLF = 0;
        }

        /// <summary>(enclosed, semi-open, open) presets for a cell, and whether to listen for which one applies.</summary>
        static void Profile(CellId id, out Preset enclosed, out Preset semi, out Preset open, out bool probe)
        {
            probe = true;
            switch (id)
            {
                case CellId.Menu:
                case CellId.ApartmentNormal:
                case CellId.Epilogue: enclosed = semi = open = Room; probe = false; break;
                case CellId.ApartmentCorrupt:
                case CellId.ShopCorrupt: enclosed = semi = open = Margin; probe = false; break;
                case CellId.Rio: enclosed = semi = open = Arena; probe = false; break;
                case CellId.ShopNormal: enclosed = TileRoom; semi = Alley; open = Street; break;
                case CellId.Berlin: enclosed = Hall; semi = Hall; open = Plaza; break;
                case CellId.NewYork: enclosed = Hall; semi = Harbour; open = Harbour; break;
                case CellId.Paris: enclosed = Alley; semi = Street; open = Plaza; break;
                case CellId.Japan: enclosed = Alley; semi = Street; open = Garden; break;
                case CellId.London: enclosed = Street; semi = Riverside; open = Riverside; break;
                case CellId.Egypt: enclosed = Courtyard; semi = Courtyard; open = Dry; break;
                case CellId.Riyadh: enclosed = Courtyard; semi = Plaza; open = Dry; break;
                case CellId.Agra: enclosed = Courtyard; semi = Garden; open = Garden; break;
                default: enclosed = Room; semi = Street; open = Plaza; break;
            }
        }

        void LateUpdate()
        {
            var rig = Game.Rig;
            if (rig == null || rig.Cam == null) return;
            var head = rig.Head.position;
            transform.position = head;
            float dt = Time.unscaledDeltaTime;

            var cell = Game.Cells != null ? Game.Cells.Active : null;
            if (cell != m_Cell)
            {
                m_Cell = cell;
                m_Snap = 0.6f; // a new place: settle quickly (the cut is behind a fade)
                m_Open = 0.5f;
                for (int i = 0; i < m_Blocked.Length; i++) m_Blocked[i] = 0.5f;
            }
            Profile(cell != null ? cell.Id : CellId.None, out var enclosed, out var semi, out var open, out bool probe);
            Preset target;
            if (probe)
            {
                Probe(head, dt);
                float wE = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.06f, 0.35f, m_Open));
                float wO = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 0.95f, m_Open));
                target = Preset.Mix(enclosed, wE, semi, Mathf.Max(0f, 1f - wE - wO), open, wO);
            }
            else target = enclosed;

            // A dimension shift: the space swells into something unreal, then lets go after the arrival.
            m_ShiftW = Mathf.MoveTowards(m_ShiftW, DimensionShift.Running ? 1f : 0f, dt * (DimensionShift.Running ? 0.4f : 0.65f));
            if (m_ShiftW > 0f) target = Preset.Lerp(target, Shift, m_ShiftW);

            float tau = m_Snap > 0f && !DimensionShift.Running ? 0.12f : 0.8f;
            m_Snap -= dt;
            m_Cur = Preset.Lerp(m_Cur, target, 1f - Mathf.Exp(-dt / tau));
            if (!m_HasApplied || !m_Cur.Near(m_Applied)) Apply(m_Cur);
        }

        /// <summary>One horizontal ray per frame round the head (the fan slowly turns so it samples more directions).</summary>
        void Probe(Vector3 head, float dt)
        {
            float a = (m_Ray * 45f + m_Spin) * Mathf.Deg2Rad;
            var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            m_Blocked[m_Ray] = Physics.Raycast(head, dir, 30f, 1 << Layers.Default, QueryTriggerInteraction.Ignore) ? 1f : 0f;
            m_Ray = (m_Ray + 1) % m_Blocked.Length;
            if (m_Ray == 0) m_Spin = (m_Spin + 7f) % 45f;
            float blocked = 0f;
            for (int i = 0; i < m_Blocked.Length; i++) blocked += m_Blocked[i];
            float openNow = 1f - blocked / m_Blocked.Length;
            m_Open += (openNow - m_Open) * (1f - Mathf.Exp(-dt / 0.8f));
        }

        void Apply(Preset p)
        {
            m_Zone.room = Mathf.RoundToInt(p.Room);
            m_Zone.roomHF = Mathf.RoundToInt(p.RoomHF);
            m_Zone.decayTime = Mathf.Clamp(p.Decay, 0.1f, 20f);
            m_Zone.decayHFRatio = Mathf.Clamp(p.DecayHF, 0.1f, 2f);
            m_Zone.reflections = Mathf.RoundToInt(p.Refl);
            m_Zone.reflectionsDelay = Mathf.Clamp(p.ReflDelay, 0f, 0.3f);
            m_Zone.reverb = Mathf.RoundToInt(p.Rev);
            m_Zone.reverbDelay = Mathf.Clamp(p.RevDelay, 0f, 0.1f);
            m_Zone.diffusion = Mathf.Clamp(p.Diffusion, 0f, 100f);
            m_Zone.density = Mathf.Clamp(p.Density, 0f, 100f);
            m_Applied = p;
            m_HasApplied = true;
        }
    }
}
