using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// Unscripted dread between the set pieces: now and then a scare one-shot from the ElevenLabs horror set
    /// (Audio/ElevenLabsHorror.md) plays somewhere around the player - a scream down the street, a door slamming in the
    /// dark, breathing right behind the head. Every place has two sounds of its own and the horror places also draw on a
    /// shared pool (scream, growl, bones, pounding, breath, giggle); the calm places (the shop, home before the glasses)
    /// only creak and knock, and rarely. Close sounds sit just behind a shoulder, the rest 7-16 m away behind or beside
    /// the player. Nothing fires over a menu, a pause, a scripted beat, narration, a chase or a close stalker, the fail
    /// sequence or an automated test, and never on the menu, the Victory Ground or in the epilogue. Gentle: no jump
    /// scares, half as often, quieter.
    /// </summary>
    public class AmbientScares : MonoBehaviour
    {
        public static AmbientScares I { get; private set; }

        readonly struct Scare
        {
            public readonly string Id;
            public readonly bool Close; // right behind the head instead of out in the place
            public readonly bool Big;   // a jump scare: never on Gentle
            public Scare(string id, bool close = false, bool big = false) { Id = id; Close = close; Big = big; }
        }

        static readonly Scare[] Shared =
        {
            new Scare("scare_any_scream", big: true), new Scare("scare_any_growl", close: true, big: true),
            new Scare("scare_any_bones", close: true), new Scare("scare_any_pound", big: true),
            new Scare("scare_any_breath", close: true), new Scare("scare_any_giggle"),
        };

        static readonly Scare[] Shop = { new Scare("scare_shop_1", close: true), new Scare("scare_shop_2") };
        static readonly Scare[] Home = { new Scare("scare_home_1"), new Scare("scare_home_2") };
        static readonly Scare[] Margin = { new Scare("scare_margin_1", close: true), new Scare("scare_margin_2", big: true) };
        static readonly Scare[] Egypt = { new Scare("scare_egy_1", big: true), new Scare("scare_egy_2", close: true) };
        static readonly Scare[] Paris = { new Scare("scare_par_1", big: true), new Scare("scare_par_2", big: true) };
        static readonly Scare[] NewYork = { new Scare("scare_ny_1", big: true), new Scare("scare_ny_2", big: true) };
        static readonly Scare[] Japan = { new Scare("scare_jpn_1", big: true), new Scare("scare_jpn_2", close: true) };
        static readonly Scare[] Riyadh = { new Scare("scare_sau_1", big: true), new Scare("scare_sau_2") };
        static readonly Scare[] Agra = { new Scare("scare_ind_1"), new Scare("scare_ind_2", big: true) };
        static readonly Scare[] Rio = { new Scare("scare_bra_1", big: true), new Scare("scare_bra_2") };
        static readonly Scare[] London = { new Scare("scare_uk_1", big: true), new Scare("scare_uk_2") };
        static readonly Scare[] Morocco = { new Scare("scare_mor_1", big: true), new Scare("scare_mor_2", close: true) };
        static readonly Scare[] Berlin = { new Scare("scare_ger_1", big: true), new Scare("scare_ger_2") };

        static Scare[] Local(CellId id)
        {
            switch (id)
            {
                case CellId.ShopNormal: return Shop;
                case CellId.ApartmentNormal: return Home;
                case CellId.ApartmentCorrupt: return Margin;
                case CellId.Egypt: return Egypt;
                case CellId.Paris: return Paris;
                case CellId.NewYork: return NewYork;
                case CellId.Japan: return Japan;
                case CellId.Riyadh: return Riyadh;
                case CellId.Agra: return Agra;
                case CellId.Rio: return Rio;
                case CellId.London: return London;
                case CellId.ShopCorrupt: return Morocco;
                case CellId.Berlin: return Berlin;
                default: return null; // the menu, the Victory Ground and the epilogue stay clean
            }
        }

        static bool Calm(CellId id) => id == CellId.ShopNormal || id == CellId.ApartmentNormal;
        static bool Gentle => Game.Settings != null && Game.Settings.scareIntensity == 0;

        CellId m_Cell = CellId.None;
        float m_Next = -1f;
        string m_Last, m_LastButOne;

        void Awake() => I = this;

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        void Update()
        {
            var cell = Game.Cells != null && Game.Cells.Active != null ? Game.Cells.Active.Id : CellId.None;
            float now = Time.time; // scaled: the countdown stops while paused
            if (cell != m_Cell)
            {
                m_Cell = cell;
                m_Next = now + Delay(cell, true);
            }
            if (AudioDirector.I == null || Game.Rig == null || Local(cell) == null || now < m_Next) return;
            if (Blocked()) { m_Next = now + Random.Range(2f, 5f); return; }
            m_Next = now + (Fire(cell, Game.Rig.Head) ? Delay(cell, false) : 5f);
        }

        /// <summary>Plays a scare for the current place right away (testing and scripted beats). False when it has none.</summary>
        public bool FireNow() => Game.Rig != null && Local(m_Cell) != null && Fire(m_Cell, Game.Rig.Head);

        static bool Blocked()
        {
            if (Game.IsAutoTest || Game.InMenu || Game.InputLocked) return true;
            if (Game.Story != null && Game.Story.Failing) return true;
            if (AudioDirector.I.VoiceActive) return true;
            return ThreatAudio.I != null && (ThreatAudio.I.ChaseActive || ThreatAudio.I.Fear > 0.6f);
        }

        static float Delay(CellId cell, bool arriving)
        {
            float lo, hi;
            if (Calm(cell)) { lo = arriving ? 35f : 55f; hi = arriving ? 60f : 100f; }
            else { lo = arriving ? 8f : 18f; hi = arriving ? 16f : 40f; }
            return Random.Range(lo, hi) * (Gentle ? 2f : 1f);
        }

        bool Fire(CellId cell, Transform head)
        {
            bool calm = Calm(cell);
            var first = calm || Random.value < 0.6f ? Local(cell) : Shared;
            var second = calm ? null : first == Shared ? Local(cell) : Shared;
            if (!TryPick(first, out var s) && (second == null || !TryPick(second, out s))) return false;

            var fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            fwd = fwd.sqrMagnitude > 0.01f ? fwd.normalized : Vector3.forward;
            Vector3 pos;
            float volume, minDistance;
            if (s.Close)
            {
                // Just behind one shoulder, at head height.
                var side = Vector3.Cross(Vector3.up, fwd) * (Random.value < 0.5f ? -1f : 1f);
                pos = head.position - fwd * Random.Range(0.5f, 0.9f) + side * Random.Range(0.25f, 0.6f) + Vector3.up * Random.Range(-0.1f, 0.1f);
                volume = Gentle ? 0.45f : 0.75f;
                minDistance = 0.8f;
            }
            else
            {
                // Out of sight, behind or beside the player, a street or a hall away.
                var dir = Quaternion.AngleAxis(Random.Range(100f, 260f), Vector3.up) * fwd;
                pos = head.position + dir * Random.Range(7f, 16f) + Vector3.up * Random.Range(-0.5f, 2f);
                volume = Gentle ? 0.55f : 1f;
                minDistance = 5f;
            }
            AudioDirector.I.Play(s.Id, pos, volume, Random.Range(0.94f, 1.03f), Bus.SFX, s.Big, null, minDistance);
            m_LastButOne = m_Last;
            m_Last = s.Id;
            return true;
        }

        /// <summary>A uniform pick among the usable scares of a pool (no jump scares on Gentle, not one of the last two).</summary>
        bool TryPick(Scare[] pool, out Scare pick)
        {
            pick = default;
            int n = 0;
            foreach (var s in pool)
            {
                if ((s.Big && Gentle) || s.Id == m_Last || s.Id == m_LastButOne || !AudioDirector.I.Has(s.Id)) continue;
                if (Random.Range(0, ++n) == 0) pick = s;
            }
            return n > 0;
        }
    }
}
