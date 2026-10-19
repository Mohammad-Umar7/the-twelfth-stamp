using System;
using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// UAE: Whack-a-Ghost, in the Margin's copy of Saeed's apartment. Little white sheet ghosts pop out of the furniture
    /// (behind the sofa, out of the fridge, the bookcase, the TV, the credenza and the open suitcase, from under the dining
    /// table and up in the bathroom doorway), stay up a moment and sink back. Stamp one while it is up (look at it and use
    /// it: desktop E or a click, VR touch or point and pull the trigger) and it bursts under a red UAE stamp. Ten stamped
    /// verifies the homecoming seal. They pop faster as the count rises, and one that gets away steals a little breath.
    /// </summary>
    public class GhostWhack : MonoBehaviour
    {
        public const int HitsToWin = 10;
        public ApartmentCell Cell;
        public readonly List<GhostSpot> Spots = new List<GhostSpot>();
        public int Hits { get; private set; }
        public int Escaped { get; private set; }
        public int Pops { get; private set; }
        public bool Running { get; private set; }
        public event Action<GhostWhack, GhostSpot> Stamped;
        public event Action<GhostWhack> Completed;
        float m_NextPop;
        GhostSpot m_Last;
        readonly List<float> m_Weights = new List<float>();

        float Progress => Mathf.Clamp01(Hits / (float)(HitsToWin - 1));
        /// <summary>How long a ghost stays up now: it shortens as the count rises.</summary>
        public float UpSecondsNow => Mathf.Lerp(1.8f, 1.2f, Progress);
        /// <summary>The wait between pops now: they come faster as the count rises.</summary>
        public float IntervalNow => Mathf.Lerp(1.5f, 0.55f, Progress);
        int MaxOut => Hits < 3 ? 1 : 2;

        /// <summary>A ghost that can be stamped right now (tests and the guide).</summary>
        public GhostSpot FirstUp { get { foreach (var s in Spots) if (s.Hittable) return s; return null; } }
        public bool AnyOut { get { foreach (var s in Spots) if (s.State != GhostSpot.Phase.Down) return true; return false; } }

        public static GhostWhack Build(ApartmentCell cell)
        {
            var go = new GameObject("WhackAGhost");
            go.transform.SetParent(cell.transform, false);
            var w = go.AddComponent<GhostWhack>();
            w.Cell = cell;
            w.Construct();
            return w;
        }

        void Construct()
        {
            // Where each ghost hides, where it pops up to (its hem; a ghost is about 0.75 m tall) and a clear spot of floor to
            // stamp it from (the guide and the tests use it). Apartment local space: see ApartmentCell / Detail_Apartment.
            Add("Sofa", new Vector3(1.2f, 0.05f, -2.92f), new Vector3(1.2f, 0.74f, -2.92f), new Vector3(2.75f, 0f, -1.05f));
            Add("Fridge", new Vector3(1.1f, 0.78f, 3.62f), new Vector3(1.1f, 0.78f, 2.86f), new Vector3(1.25f, 0f, 1.35f));
            Add("Bookcase", new Vector3(-4.75f, 0.72f, 2.75f), new Vector3(-3.95f, 0.72f, 2.75f), new Vector3(-2.55f, 0f, 2.25f));
            Add("Table", new Vector3(-1.6f, -0.85f, -0.32f), new Vector3(-1.6f, 0.06f, -0.32f), new Vector3(-1.6f, 0f, -1.85f));
            Add("Bathroom", new Vector3(2.75f, -0.9f, -3.82f), new Vector3(2.75f, 0.12f, -3.82f), new Vector3(3.35f, 0f, -2.25f));
            Add("Credenza", new Vector3(-4.18f, 0.02f, 0.35f), new Vector3(-4.0f, 0.78f, 0.35f), new Vector3(-2.85f, 0f, 0.3f));
            Add("TV", new Vector3(2.9f, 0.98f, 4.3f), new Vector3(2.9f, 0.98f, 3.45f), new Vector3(2.75f, 0f, 1.85f));
            Add("Suitcase", new Vector3(-3.4f, -0.65f, -1.6f), new Vector3(-3.4f, 0.2f, -1.6f), new Vector3(-2.2f, 0f, -2.35f));
        }

        void Add(string where, Vector3 hidden, Vector3 up, Vector3 stand) => Spots.Add(GhostSpot.Create(this, where, hidden, up, stand));

        /// <summary>A fresh game: every ghost hidden, the count at zero, the first pop after <paramref name="delay"/> s.</summary>
        public void Begin(float delay)
        {
            foreach (var s in Spots) s.Hide(false);
            Hits = 0;
            Escaped = 0;
            Pops = 0;
            m_Last = null;
            m_NextPop = delay;
            Running = true;
        }

        /// <summary>No more ghosts (the seal is verified, or the player left).</summary>
        public void Stop()
        {
            Running = false;
            foreach (var s in Spots) s.Hide(false);
        }

        void Update()
        {
            if (!Running || Cell == null || Cell.Mode != CellMode.Active || Cell.Encounter == null || !Cell.Encounter.IsLive) return;
            var story = Game.Story;
            if (story == null || Game.Rig == null || story.Busy || story.Failing || Game.Paused) return;
            if (Game.Campaign != null && Game.Campaign.vitality <= 0f) return; // the Retry card is up
            int outNow = 0;
            foreach (var s in Spots) if (s.State != GhostSpot.Phase.Down) outNow++;
            m_NextPop -= Time.deltaTime;
            if (m_NextPop > 0f || outNow >= MaxOut) return;
            var pick = Pick();
            if (pick == null) return;
            pick.Pop(UpSecondsNow + UnityEngine.Random.Range(-0.12f, 0.12f));
            Pops++;
            m_Last = pick;
            m_NextPop = IntervalNow + UnityEngine.Random.Range(0f, 0.35f);
        }

        /// <summary>A hidden ghost, most likely one near the player and in front of them (never the last one again).</summary>
        GhostSpot Pick()
        {
            var feet = Game.Rig.Feet;
            var head = Game.Rig.Head;
            var fwd = head.forward; fwd.y = 0f;
            m_Weights.Clear();
            float total = 0f;
            foreach (var s in Spots)
            {
                float w = 0f;
                if (s.State == GhostSpot.Phase.Down && s != m_Last)
                {
                    var p = s.UpWorld;
                    var to = p - feet; to.y = 0f;
                    float d = to.magnitude;
                    w = 1f;
                    if (d < 4.5f) w += 2f;
                    if (d < 1.1f) w = 0.3f; // not right on top of the player
                    if (fwd.sqrMagnitude > 0.01f && Vector3.Angle(fwd, to) < 65f) w += 1.5f;
                }
                m_Weights.Add(w);
                total += w;
            }
            if (total <= 0f) return null;
            float r = UnityEngine.Random.value * total;
            for (int i = 0; i < Spots.Count; i++)
            {
                r -= m_Weights[i];
                if (r <= 0f && m_Weights[i] > 0f) return Spots[i];
            }
            for (int i = Spots.Count - 1; i >= 0; i--) if (m_Weights[i] > 0f) return Spots[i];
            return null;
        }

        internal void OnStamp(GhostSpot s, Interactor who)
        {
            if (!Running || !s.Hittable) return;
            s.Burst();
            Hits++;
            AudioDirector.I?.Play("stamp_hit", s.HeadPoint, 1f, UnityEngine.Random.Range(0.95f, 1.08f));
            AudioDirector.I?.Play("pass_recoil", s.HeadPoint, 0.3f, 1.7f);
            if (who != null) AudioDirector.Haptic(who.Side, 0.7f, 0.08f);
            PostFx.I?.Flash(0.1f);
            Stamped?.Invoke(this, s);
            if (Hits >= HitsToWin)
            {
                Running = false;
                foreach (var o in Spots) if (o != s) o.Hide(true); // the rest sink away
                Completed?.Invoke(this);
            }
            else if (m_NextPop > 0.4f) m_NextPop = 0.4f; // the next one comes quickly
        }

        internal void OnEscaped(GhostSpot s)
        {
            if (!Running) return;
            Escaped++;
            Game.Story?.DrainVitality(0.03f);
            AudioDirector.I?.Play("pass_whisper", s.HeadPoint, 0.3f, 1.5f);
            if (Escaped % 3 == 1) Hud.I?.ShowCaption("it ducks away, giggling, and something cold brushes your neck: be quicker", 2.5f);
        }
    }

    /// <summary>One ghost and its hiding place in the furniture (GhostWhack). An Interactable while it is up: "Stamp it!".</summary>
    public class GhostSpot : Interactable
    {
        public enum Phase { Down, Rising, Up, Sinking, Burst }
        public string Where;
        public Vector3 HiddenLocal, UpLocal, StandLocal;   // in the apartment's (the owner's parent's) space
        public GhostWhack Owner;
        public Phase State { get; private set; } = Phase.Down;
        public float UpSeconds { get; private set; }
        public bool Hittable => State == Phase.Rising || State == Phase.Up;
        /// <summary>The middle of the ghost (aim here).</summary>
        public Vector3 AimPoint => m_Sheet != null ? m_Sheet.TransformPoint(new Vector3(0f, 0.42f, 0f)) : transform.position;
        public Vector3 HeadPoint => m_Sheet != null ? m_Sheet.TransformPoint(new Vector3(0f, 0.56f, 0f)) : transform.position;
        public Vector3 UpWorld => transform.parent.TransformPoint(UpLocal);
        public Vector3 StandWorld => transform.parent.TransformPoint(StandLocal);

        const float RiseSeconds = 0.28f, SinkSeconds = 0.32f, BurstSeconds = 0.9f;
        Transform m_Sheet, m_Mark;
        Light m_Glow;
        SphereCollider m_Col;
        Material m_MarkMat;
        readonly List<Transform> m_Puffs = new List<Transform>();
        readonly List<Vector3> m_PuffDirs = new List<Vector3>();
        Vector3 m_BurstAt;
        float m_T, m_Seed;
        bool m_Quiet;

        public static GhostSpot Create(GhostWhack owner, string where, Vector3 hidden, Vector3 up, Vector3 stand)
        {
            var go = new GameObject("Ghost_" + where);
            go.transform.SetParent(owner.transform, false);
            go.transform.localPosition = hidden;
            var sheet = new GameObject("Sheet").transform;
            sheet.SetParent(go.transform, false);
            BuildGhost(sheet);
            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.34f;
            col.center = new Vector3(0f, 0.42f, 0f);
            var s = go.AddComponent<GhostSpot>(); // Awake puts the ghost on the Interactable layer
            s.Owner = owner;
            s.Where = where;
            s.HiddenLocal = hidden;
            s.UpLocal = up;
            s.StandLocal = stand;
            s.m_Sheet = sheet;
            s.m_Col = col;
            s.m_Glow = sheet.GetComponentInChildren<Light>(true);
            s.m_Seed = UnityEngine.Random.value * 10f;
            s.Prompt = "Stamp it!";
            s.MaxDistance = 3.2f;
            s.MarkerPoint = sheet.Find("Head");
            s.BuildBurst();
            s.Hide(false);
            return s;
        }

        /// <summary>A small white sheet ghost facing +Z: a bell of a body with a wavy hem, a round head, stubby arms, dark eyes
        /// and an "o" of a mouth, faintly glowing.</summary>
        static void BuildGhost(Transform sheet)
        {
            var white = Mats.Lit(new Color(0.93f, 0.95f, 0.99f), 0.25f, 0f, null, false, new Color(0.24f, 0.28f, 0.34f));
            var dark = Mats.Lit(new Color(0.02f, 0.02f, 0.03f), 0.7f);
            MeshFactory.Frustum("Body", sheet, Vector3.zero, 0.24f, 0.13f, 0.48f, white, 20);
            MeshFactory.Sphere("Head", sheet, new Vector3(0f, 0.56f, 0f), 0.175f, white);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 2f / 8f;
                MeshFactory.Sphere("Hem", sheet, new Vector3(Mathf.Cos(a) * 0.205f, 0.025f, Mathf.Sin(a) * 0.205f), 0.055f, white, 10, 8);
            }
            MeshFactory.Sphere("ArmL", sheet, new Vector3(-0.175f, 0.34f, 0.04f), 0.055f, white, 10, 8);
            MeshFactory.Sphere("ArmR", sheet, new Vector3(0.175f, 0.34f, 0.04f), 0.055f, white, 10, 8);
            foreach (var x in new[] { -0.062f, 0.062f })
                MeshFactory.Sphere("Eye", sheet, new Vector3(x, 0.6f, 0.152f), 0.034f, dark, 10, 8).transform.localScale = new Vector3(1f, 1.45f, 0.6f);
            MeshFactory.Sphere("Mouth", sheet, new Vector3(0f, 0.5f, 0.16f), 0.026f, dark, 10, 8).transform.localScale = new Vector3(1f, 1.3f, 0.6f);
            MeshFactory.NoShadows(sheet.gameObject);
            var lg = new GameObject("Glow");
            lg.transform.SetParent(sheet, false);
            lg.transform.localPosition = new Vector3(0f, 0.45f, 0.3f);
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(0.75f, 0.88f, 1f); l.intensity = 0.8f; l.range = 1.8f; l.shadows = LightShadows.None;
        }

        /// <summary>The red UAE stamp that flashes where a ghost is stamped, and the puffs of sheet it bursts into.</summary>
        void BuildBurst()
        {
            var tex = Tex.Art(CountryDatabase.Get(CountryId.UAE).StampTexture);
            // Alpha-blended, not added: red ink added onto a white sheet is still white.
            m_MarkMat = Mats.UnlitInstance(MarkColor(0f), tex != null ? tex : Tex.Ring, true, 1.7f);
            m_MarkMat.SetFloat("_Cull", 0f);
            m_Mark = MeshFactory.Quad("StampMark_" + Where, Owner.transform, 0.36f, 0.36f, m_MarkMat).transform;
            m_Mark.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            m_Mark.gameObject.SetActive(false);
            var white = Mats.Lit(new Color(0.93f, 0.95f, 0.99f), 0.25f, 0f, null, false, new Color(0.24f, 0.28f, 0.34f));
            for (int i = 0; i < 7; i++)
            {
                var p = MeshFactory.Sphere("Puff", Owner.transform, Vector3.zero, 0.07f, white, 10, 8).transform;
                p.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                p.gameObject.SetActive(false);
                float a = i * Mathf.PI * 2f / 7f;
                m_Puffs.Add(p);
                m_PuffDirs.Add(new Vector3(Mathf.Cos(a), 0.35f + (i % 3) * 0.3f, Mathf.Sin(a)).normalized);
            }
        }

        public override bool CanUse(Interactor who) => base.CanUse(who) && Hittable && Owner != null && Owner.Running;

        public override void Use(Interactor who)
        {
            base.Use(who);
            Owner?.OnStamp(this, who);
        }

        /// <summary>Up out of its hiding place for <paramref name="upSeconds"/>, then back down.</summary>
        public void Pop(float upSeconds)
        {
            UpSeconds = Mathf.Max(0.5f, upSeconds);
            State = Phase.Rising;
            m_T = 0f;
            m_Quiet = false;
            SetVisible(true);
            transform.localPosition = HiddenLocal;
            AudioDirector.I?.Play("pass_whisper", UpWorld + Vector3.up * 0.5f, 0.32f, UnityEngine.Random.Range(1.45f, 1.7f));
            AudioDirector.I?.Play("drop_soft", UpWorld, 0.35f, 1.8f);
        }

        /// <summary>Back into hiding: sinking (it does not count as one that got away) or at once.</summary>
        public void Hide(bool animate)
        {
            if (animate && (State == Phase.Rising || State == Phase.Up))
            {
                State = Phase.Sinking;
                m_T = 0f;
                m_Quiet = true;
                return;
            }
            if (State == Phase.Burst && animate) return; // let the burst finish
            State = Phase.Down;
            transform.localPosition = HiddenLocal;
            SetVisible(false);
            if (m_Mark != null) m_Mark.gameObject.SetActive(false);
            foreach (var p in m_Puffs) if (p != null) p.gameObject.SetActive(false);
        }

        internal void Burst()
        {
            State = Phase.Burst;
            m_T = 0f;
            m_BurstAt = AimPoint;
            if (m_Col != null) m_Col.enabled = false;
            Usable = false;
            // The red stamp lands on the front of the ghost, toward the player (not inside its puffed-up head).
            var head = Game.Rig != null ? Game.Rig.Head : null;
            var toward = head != null ? head.position - HeadPoint : Vector3.zero;
            m_Mark.position = HeadPoint + (toward.sqrMagnitude > 0.01f ? toward.normalized * 0.32f : Vector3.zero);
            m_Mark.gameObject.SetActive(true);
            foreach (var p in m_Puffs) { p.position = m_BurstAt; p.gameObject.SetActive(true); }
        }

        void SetVisible(bool on)
        {
            if (m_Sheet != null) { m_Sheet.gameObject.SetActive(on); m_Sheet.localScale = Vector3.one; }
            if (m_Col != null) m_Col.enabled = on;
            Usable = on;
        }

        static Color MarkColor(float a) => new Color(1f, 0.55f, 0.5f, a); // the stamp's own red ink, a little brighter

        static float EaseOutBack(float k)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float x = k - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }

        void Update()
        {
            if (State == Phase.Down) return;
            float dt = Time.deltaTime;
            switch (State)
            {
                case Phase.Rising:
                {
                    m_T = Mathf.Min(1f, m_T + dt / RiseSeconds);
                    transform.localPosition = Vector3.LerpUnclamped(HiddenLocal, UpLocal, EaseOutBack(m_T));
                    m_Sheet.localScale = Vector3.one * Mathf.Lerp(0.7f, 1f, m_T);
                    if (m_T >= 1f) { State = Phase.Up; m_T = 0f; }
                    break;
                }
                case Phase.Up:
                {
                    m_T += dt;
                    float bob = Mathf.Sin((Time.time + m_Seed) * 6f) * 0.03f;
                    transform.localPosition = UpLocal + Vector3.up * bob;
                    if (m_T >= UpSeconds) { State = Phase.Sinking; m_T = 0f; }
                    break;
                }
                case Phase.Sinking:
                {
                    m_T = Mathf.Min(1f, m_T + dt / SinkSeconds);
                    transform.localPosition = Vector3.Lerp(UpLocal, HiddenLocal, m_T * m_T);
                    if (m_T >= 1f)
                    {
                        bool escaped = !m_Quiet;
                        State = Phase.Down;
                        SetVisible(false);
                        if (escaped) Owner?.OnEscaped(this);
                    }
                    break;
                }
                case Phase.Burst:
                {
                    m_T += dt;
                    // The sheet puffs up and pops; its scraps fly out and shrink; the red stamp flashes and fades.
                    if (m_Sheet.gameObject.activeSelf)
                    {
                        float s = m_T < 0.1f ? Mathf.Lerp(1f, 1.35f, m_T / 0.1f) : Mathf.Lerp(1.35f, 0f, (m_T - 0.1f) / 0.15f);
                        m_Sheet.localScale = Vector3.one * Mathf.Max(0f, s);
                        if (m_T > 0.25f) m_Sheet.gameObject.SetActive(false);
                    }
                    float e = Mathf.Clamp01(m_T / 0.45f);
                    for (int i = 0; i < m_Puffs.Count; i++)
                    {
                        var p = m_Puffs[i];
                        if (p == null) continue;
                        p.position = m_BurstAt + m_PuffDirs[i] * (0.6f * (1f - (1f - e) * (1f - e)));
                        p.localScale = Vector3.one * Mathf.Max(0.01f, 1.4f * (1f - e));
                        if (e >= 1f) p.gameObject.SetActive(false);
                    }
                    float m = Mathf.Clamp01(m_T / BurstSeconds);
                    m_MarkMat.SetColor("_Color", MarkColor(m < 0.15f ? 1f : 1f - (m - 0.15f) / 0.85f));
                    m_Mark.localScale = Vector3.one * Mathf.Lerp(0.85f, 1.25f, Mathf.Sqrt(m));
                    if (m >= 1f) Hide(false);
                    break;
                }
            }
            if (State == Phase.Down) return;
            // Faces the player; a little sway while it is out.
            var head = Game.Rig != null ? Game.Rig.Head : null;
            if (head != null && m_Sheet != null && m_Sheet.gameObject.activeSelf)
            {
                var to = head.position - transform.position; to.y = 0f;
                if (to.sqrMagnitude > 0.001f)
                    m_Sheet.rotation = Quaternion.LookRotation(to.normalized, Vector3.up) * Quaternion.Euler(0f, 0f, Mathf.Sin((Time.time + m_Seed) * 3.1f) * 6f);
            }
            if (head != null && m_Mark != null && m_Mark.gameObject.activeSelf) m_Mark.rotation = Quaternion.LookRotation(m_Mark.position - head.position, Vector3.up);
            if (m_Glow != null) m_Glow.intensity = 0.7f + Mathf.Sin((Time.time + m_Seed) * 4f) * 0.15f;
        }
    }
}
