using System;
using System.Collections;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// London's arcade game, Stop the Clock: a Big Ben clock face (1.6 m across, gold rim, Roman numerals, minute ticks) in
    /// a stone case on a cast-iron stand, its needle spinning, a green zone on the dial and a big red STOP button on a
    /// lectern in front of it. Press while the needle is in the green: a hit lights a lamp over the dial, the needle stops
    /// for a moment, then the zone moves and shrinks and the needle speeds up. A miss buzzes and puts the count back to
    /// nought. Three hits in a row stop the clock at midnight. The face looks down local -Z (the player stands there).
    /// </summary>
    public class StopTheClock : MonoBehaviour
    {
        public const int HitsToWin = 3;
        public const float Radius = 0.8f;      // the dial, 1.6 m across
        public const float DialY = 1.95f;      // the dial's centre above the ground
        const float ZoneInner = 0.28f, ZoneOuter = 0.56f;
        const float Grace = 3f;                // degrees either side of the zone that still count (input and frame latency)
        static readonly float[] Widths = { 70f, 56f, 44f };   // the zone for the 1st, 2nd and 3rd hit
        static readonly float[] Speeds = { 100f, 130f, 165f }; // degrees per second

        public PushButton Button { get; private set; }
        public Interactable Face { get; private set; }
        public int Hits { get; private set; }
        public int Misses { get; private set; }
        public bool Won { get; private set; }
        /// <summary>Clock degrees from XII, clockwise as the player sees the face.</summary>
        public float NeedleAngle { get; private set; }
        /// <summary>The zone runs clockwise from here for ZoneWidth degrees.</summary>
        public float ZoneStart { get; private set; }
        public float ZoneWidth { get; private set; }
        public float Speed { get; private set; }
        /// <summary>The needle is held still for a moment after a hit (presses are ignored meanwhile).</summary>
        public bool Frozen => m_Freeze > 0f || Won;
        public event Action<StopTheClock, bool> Pressed;   // true: a hit
        public event Action<StopTheClock> Completed;

        public Vector3 FaceCenter => transform.TransformPoint(new Vector3(0f, DialY, -0.05f));
        public Vector3 ButtonPosition => Button != null ? Button.transform.position : transform.position;
        public bool NeedleInZone => Rel(NeedleAngle) <= ZoneWidth;
        /// <summary>The needle is in the middle half of the zone (tests press here).</summary>
        public bool NeedleNearZoneCentre { get { float r = Rel(NeedleAngle); return r >= ZoneWidth * 0.25f && r <= ZoneWidth * 0.75f; } }
        /// <summary>The needle is well away from the zone, either side (the tests' deliberate miss).</summary>
        public bool NeedleFarFromZone { get { float r = Rel(NeedleAngle); return r > ZoneWidth + 40f && r < 320f; } }

        Transform m_Needle, m_Zone;
        MeshFilter m_ZoneMesh;
        Material m_ZoneMat, m_DialMat;
        readonly Renderer[] m_Lamps = new Renderer[HitsToWin];
        Material m_LampOff, m_LampOn, m_LampWon;
        Light m_FaceLight;
        float m_Freeze, m_Cooldown, m_Flash;
        bool m_FlashHit, m_PendingLevel;
        static readonly Color ZoneBase = new Color(0.16f, 0.72f, 0.28f), ZoneGlow = new Color(0.25f, 1.5f, 0.45f);

        float Rel(float angle) => Mathf.Repeat(angle - ZoneStart, 360f);

        public static StopTheClock Build(Transform parent, Vector3 pos, float yaw)
        {
            var go = new GameObject("StopTheClock");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var c = go.AddComponent<StopTheClock>();
            c.Construct();
            c.ResetGame();
            return c;
        }

        void Construct()
        {
            var t = transform;
            var stone = Mats.Lit(new Color(0.74f, 0.64f, 0.46f), 0.3f, 0f, "limestone_blocks");
            var iron = Mats.Lit(new Color(0.05f, 0.06f, 0.06f), 0.5f, 0.7f);
            var gold = Mats.Lit(new Color(0.95f, 0.74f, 0.32f), 0.75f, 1f, null, false, new Color(0.35f, 0.24f, 0.06f));
            var slate = Mats.Lit(new Color(0.16f, 0.17f, 0.2f), 0.45f, 0f, "stone_dark_slate");
            var granite = Mats.Lit(new Color(0.42f, 0.42f, 0.44f), 0.5f, 0f, "granite_pavers");
            var black = Mats.Lit(new Color(0.03f, 0.03f, 0.035f), 0.55f, 0.3f);
            // Backlit opal glass, like the real dials at night.
            m_DialMat = new Material(Mats.Lit(new Color(0.96f, 0.93f, 0.82f), 0.3f, 0f, null, false, new Color(0.55f, 0.5f, 0.36f)));

            // The stand: a granite plinth, two cast-iron legs, the stone clock case, a name board, a slate roof, a finial.
            MeshFactory.Box("Plinth", t, new Vector3(0f, 0.11f, 0.05f), new Vector3(1.7f, 0.22f, 0.75f), granite, true);
            foreach (var x in new[] { -0.62f, 0.62f })
            {
                MeshFactory.Box("Leg", t, new Vector3(x, 0.61f, 0.08f), new Vector3(0.16f, 0.78f, 0.16f), iron, true);
                MeshFactory.Box("LegFoot", t, new Vector3(x, 0.25f, 0.08f), new Vector3(0.26f, 0.06f, 0.26f), iron, false);
                MeshFactory.Box("LegCap", t, new Vector3(x, 0.97f, 0.08f), new Vector3(0.24f, 0.06f, 0.24f), gold, false);
            }
            MeshFactory.Box("ClockCase", t, new Vector3(0f, 2.025f, 0.1f), new Vector3(1.9f, 2.05f, 0.18f), stone, true);
            // A gilt square frame round the dial and gilt spandrels in its corners.
            var frame = new MeshBuilder();
            const float f = 0.88f, ft = 0.035f, fz = -0.005f;
            frame.AddBox(new Vector3(0f, DialY + f, fz), new Vector3(2f * f + ft, ft, 0.02f));
            frame.AddBox(new Vector3(0f, DialY - f, fz), new Vector3(2f * f + ft, ft, 0.02f));
            frame.AddBox(new Vector3(-f, DialY, fz), new Vector3(ft, 2f * f, 0.02f));
            frame.AddBox(new Vector3(f, DialY, fz), new Vector3(ft, 2f * f, 0.02f));
            foreach (var sx in new[] { -1f, 1f })
                foreach (var sy in new[] { -1f, 1f })
                {
                    var c = new Vector3(sx * 0.73f, DialY + sy * 0.73f, fz);
                    frame.AddTorus(c, 0.06f, 0.012f, 18, 6, Quaternion.Euler(90f, 0f, 0f));
                    frame.AddSphere(c + new Vector3(0f, 0f, -0.01f), 0.025f, 10, 6);
                    frame.AddBox(c + new Vector3(-sx * 0.07f, -sy * 0.07f, 0f), new Vector3(0.018f, 0.12f, 0.012f), Quaternion.Euler(0f, 0f, sx * sy * 45f));
                }
            MeshFactory.FromBuilder("GiltFrame", frame, gold, t);

            // The dial: a thin opal disc, the gold rim, an inner ring, sixty minute ticks and the Roman numerals.
            var dial = new MeshBuilder();
            dial.AddCylinder(new Vector3(0f, DialY, 0.005f), Radius, 0.025f, 56, 1f, Quaternion.Euler(-90f, 0f, 0f));
            MeshFactory.FromBuilder("Dial", dial, m_DialMat, t);
            var rim = new MeshBuilder();
            rim.AddTorus(new Vector3(0f, DialY, -0.025f), Radius, 0.035f, 64, 10, Quaternion.Euler(90f, 0f, 0f));
            rim.AddTorus(new Vector3(0f, DialY, -0.022f), ZoneOuter + 0.025f, 0.008f, 56, 6, Quaternion.Euler(90f, 0f, 0f));
            MeshFactory.FromBuilder("GoldRim", rim, gold, t);
            var ticks = new MeshBuilder();
            for (int i = 0; i < 60; i++)
            {
                float a = i * 6f * Mathf.Deg2Rad;
                bool five = i % 5 == 0;
                float r = five ? 0.735f : 0.752f;
                ticks.AddBox(new Vector3(Mathf.Sin(a) * r, DialY + Mathf.Cos(a) * r, -0.022f), five ? new Vector3(0.018f, 0.07f, 0.004f) : new Vector3(0.008f, 0.035f, 0.004f), Quaternion.Euler(0f, 0f, -i * 6f));
            }
            MeshFactory.FromBuilder("Ticks", ticks, black, t);
            string[] numerals = { "XII", "I", "II", "III", "IIII", "V", "VI", "VII", "VIII", "IX", "X", "XI" };
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f * Mathf.Deg2Rad;
                var n = TextFactory.World(t, numerals[i], 0.085f, new Color(0.05f, 0.05f, 0.06f), TMPro.TextAlignmentOptions.Center, true, 0.3f);
                n.transform.localPosition = new Vector3(Mathf.Sin(a) * 0.645f, DialY + Mathf.Cos(a) * 0.645f - 0.012f, -0.026f);
                n.fontStyle = TMPro.FontStyles.Bold;
            }

            // The green zone: an arc of the inner band, rebuilt when it shrinks, turned to where it is.
            m_ZoneMat = new Material(Mats.Emissive(ZoneBase, ZoneGlow));
            m_Zone = new GameObject("GreenZone").transform;
            m_Zone.SetParent(t, false);
            m_Zone.localPosition = new Vector3(0f, DialY, -0.021f);
            m_ZoneMesh = m_Zone.gameObject.AddComponent<MeshFilter>();
            var zr = m_Zone.gameObject.AddComponent<MeshRenderer>();
            zr.sharedMaterial = m_ZoneMat;
            zr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // The needle: black iron with a gilt spade tip and a counterweight, on a gilt boss.
            m_Needle = new GameObject("Needle").transform;
            m_Needle.SetParent(t, false);
            m_Needle.localPosition = new Vector3(0f, DialY, -0.045f);
            MeshFactory.Box("Shaft", m_Needle, new Vector3(0f, 0.3f, 0f), new Vector3(0.032f, 0.66f, 0.012f), black, false);
            MeshFactory.Box("Spade", m_Needle, new Vector3(0f, 0.6f, -0.002f), new Vector3(0.075f, 0.075f, 0.012f), gold, false, 1f, Quaternion.Euler(0f, 0f, 45f));
            MeshFactory.Box("Point", m_Needle, new Vector3(0f, 0.68f, 0f), new Vector3(0.016f, 0.1f, 0.01f), black, false);
            MeshFactory.Box("Tail", m_Needle, new Vector3(0f, -0.09f, 0f), new Vector3(0.04f, 0.18f, 0.012f), black, false);
            var weight = new MeshBuilder();
            weight.AddCylinder(new Vector3(0f, -0.17f, 0.006f), 0.05f, 0.014f, 18, 1f, Quaternion.Euler(-90f, 0f, 0f));
            MeshFactory.FromBuilder("Counterweight", weight, gold, m_Needle);
            MeshFactory.Sphere("Boss", t, new Vector3(0f, DialY, -0.055f), 0.05f, gold);

            // Three lamps over the dial: one lights for each hit in a row.
            m_LampOff = Mats.Lit(new Color(0.12f, 0.14f, 0.12f), 0.85f, 0.2f);
            m_LampOn = Mats.Emissive(new Color(0.3f, 0.95f, 0.4f), new Color(0.4f, 2.4f, 0.6f));
            m_LampWon = Mats.Emissive(new Color(1f, 0.78f, 0.3f), new Color(1.5f, 0.95f, 0.2f));
            var bezels = new MeshBuilder();
            for (int i = 0; i < HitsToWin; i++)
            {
                var p = new Vector3((i - 1) * 0.24f, 2.9f, -0.0f);
                m_Lamps[i] = MeshFactory.Sphere("Lamp" + i, t, p, 0.055f, m_LampOff).GetComponent<Renderer>();
                bezels.AddTorus(p + new Vector3(0f, 0f, -0.012f), 0.062f, 0.011f, 20, 6, Quaternion.Euler(90f, 0f, 0f));
            }
            MeshFactory.FromBuilder("LampBezels", bezels, gold, t);

            // The name board, the roof and the finial.
            MeshFactory.Box("NameBoard", t, new Vector3(0f, 3.19f, 0.1f), new Vector3(1.96f, 0.28f, 0.2f), iron, false);
            var title = TextFactory.World(t, "STOP  THE  CLOCK", 0.11f, new Color(0.98f, 0.8f, 0.4f), TMPro.TextAlignmentOptions.Center, true, 1.9f);
            title.transform.localPosition = new Vector3(0f, 3.19f, -0.005f);
            MeshFactory.Box("RoofL", t, new Vector3(-0.5f, 3.56f, 0.1f), new Vector3(1.16f, 0.06f, 0.3f), slate, false, 1f, Quaternion.Euler(0f, 0f, 24f));
            MeshFactory.Box("RoofR", t, new Vector3(0.5f, 3.56f, 0.1f), new Vector3(1.16f, 0.06f, 0.3f), slate, false, 1f, Quaternion.Euler(0f, 0f, -24f));
            MeshFactory.Frustum("Finial", t, new Vector3(0f, 3.79f, 0.1f), 0.05f, 0.005f, 0.3f, gold, 8);
            MeshFactory.Sphere("FinialBall", t, new Vector3(0f, 3.82f, 0.1f), 0.045f, gold);

            // The lectern with the big red STOP button, tilted toward the player.
            var lectern = new GameObject("Lectern").transform;
            lectern.SetParent(t, false);
            lectern.localPosition = new Vector3(0f, 0f, -0.78f);
            MeshFactory.Box("LecternFoot", lectern, new Vector3(0f, 0.03f, 0f), new Vector3(0.36f, 0.06f, 0.36f), iron, true);
            MeshFactory.Box("LecternPost", lectern, new Vector3(0f, 0.47f, 0f), new Vector3(0.12f, 0.86f, 0.12f), iron, true);
            MeshFactory.Box("LecternBand", lectern, new Vector3(0f, 0.7f, 0f), new Vector3(0.15f, 0.04f, 0.15f), gold, false);
            MeshFactory.Box("LecternTop", lectern, new Vector3(0f, 0.92f, 0f), new Vector3(0.36f, 0.05f, 0.3f), iron, false, 1f, Quaternion.Euler(-20f, 0f, 0f));
            var go = new GameObject("StopButton");
            go.transform.SetParent(lectern, false);
            go.transform.localPosition = new Vector3(0f, 0.95f, -0.01f);
            go.transform.localRotation = Quaternion.Euler(-20f, 0f, 0f);
            MeshFactory.Cylinder("Bezel", go.transform, new Vector3(0f, -0.005f, 0f), 0.105f, 0.02f, gold, 24, false);
            var red = new Color(0.92f, 0.08f, 0.06f);
            var cap = MeshFactory.Cylinder("Cap", go.transform, new Vector3(0f, 0.012f, 0f), 0.085f, 0.045f, Mats.Emissive(red, red * 0.9f), 24, false);
            var label = TextFactory.World(cap.transform, "STOP", 0.045f, Color.white, TMPro.TextAlignmentOptions.Center, false, 0.2f);
            label.transform.localPosition = new Vector3(0f, 0.047f, 0f);
            label.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            label.fontStyle = TMPro.FontStyles.Bold;
            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(0.24f, 0.1f, 0.24f);
            col.center = new Vector3(0f, 0.03f, 0f);
            Button = go.AddComponent<PushButton>();
            Button.Prompt = "STOP the needle in the green";
            Button.Cap = cap.transform;
            Button.PressOffset = new Vector3(0f, -0.02f, 0f);
            Button.MaxDistance = 2.4f;
            Button.Pressed += _ => OnPress();
            // The face itself takes a press too (desktop: E while looking at the clock, so the needle stays in view; VR: the
            // aim ray on the face and the trigger). The red button is the one for the hand.
            var faceGo = new GameObject("FaceTarget");
            faceGo.transform.SetParent(t, false);
            faceGo.transform.localPosition = new Vector3(0f, DialY, -0.05f);
            var fc = faceGo.AddComponent<BoxCollider>();
            fc.size = new Vector3(2f * Radius, 2f * Radius, 0.04f);
            Face = faceGo.AddComponent<Interactable>();
            Face.Prompt = "Stop the clock";
            Face.MaxDistance = 3.2f;
            Face.OnUse = _ =>
            {
                AudioDirector.I?.Play("button_press", ButtonPosition, 0.8f);
                if (Button != null && Button.Cap != null) StartCoroutine(PushCap());
                OnPress();
            };

            // Light on the face and the button, so it reads down the promenade at midnight.
            var lg = new GameObject("ClockLight");
            lg.transform.SetParent(t, false);
            lg.transform.localPosition = new Vector3(0f, 2.4f, -1.4f);
            m_FaceLight = lg.AddComponent<Light>();
            m_FaceLight.type = LightType.Point; m_FaceLight.color = new Color(1f, 0.86f, 0.62f); m_FaceLight.intensity = 1.5f; m_FaceLight.range = 4.5f; m_FaceLight.shadows = LightShadows.None;
        }

        /// <summary>A fresh game: nothing hit, the slow needle, the widest zone.</summary>
        public void ResetGame()
        {
            StopAllCoroutines();
            Won = false;
            Hits = 0;
            Misses = 0;
            m_Freeze = 0f; m_Cooldown = 0f; m_Flash = 0f; m_PendingLevel = false;
            NeedleAngle = 0f;
            SetLevel(0, true);
            if (Button != null) Button.Usable = true;
            if (Face != null) Face.Usable = true;
            if (m_Zone != null) m_Zone.gameObject.SetActive(true);
            m_DialMat.SetColor("_EmissionColor", new Color(0.55f, 0.5f, 0.36f));
            UpdateLamps();
            PlaceNeedle();
        }

        /// <summary>Already stopped (the seal is verified): the needle at midnight, every lamp gold, the button dead.</summary>
        public void ShowStopped()
        {
            StopAllCoroutines();
            Won = true;
            Hits = HitsToWin;
            NeedleAngle = 0f;
            if (Button != null) Button.Usable = false;
            if (Face != null) Face.Usable = false;
            if (m_Zone != null) m_Zone.gameObject.SetActive(false);
            m_DialMat.SetColor("_EmissionColor", new Color(0.9f, 0.75f, 0.4f));
            UpdateLamps();
            PlaceNeedle();
        }

        void SetLevel(int level, bool move)
        {
            level = Mathf.Clamp(level, 0, Widths.Length - 1);
            ZoneWidth = Widths[level];
            Speed = Speeds[level];
            if (move)
            {
                // Well ahead of the needle, so the next press needs a fresh look.
                float centre = NeedleAngle + UnityEngine.Random.Range(130f, 250f);
                ZoneStart = Mathf.Repeat(centre - ZoneWidth / 2f, 360f);
            }
            BuildZone();
        }

        void BuildZone()
        {
            if (m_Zone == null) return;
            var b = new MeshBuilder();
            int seg = Mathf.Max(4, Mathf.CeilToInt(ZoneWidth / 4f));
            for (int i = 0; i < seg; i++)
            {
                float a0 = ZoneWidth * i / seg * Mathf.Deg2Rad, a1 = ZoneWidth * (i + 1) / seg * Mathf.Deg2Rad;
                Vector3 P(float a, float r) => new Vector3(Mathf.Sin(a) * r, Mathf.Cos(a) * r, 0f);
                FacingViewer(b, P(a0, ZoneInner), P(a1, ZoneInner), P(a1, ZoneOuter), P(a0, ZoneOuter));
            }
            // Bright edges so the ends of the zone read at a glance.
            foreach (var a in new[] { 0f, ZoneWidth * Mathf.Deg2Rad })
            {
                var dir = new Vector3(Mathf.Sin(a), Mathf.Cos(a), 0f);
                var side = new Vector3(dir.y, -dir.x, 0f) * 0.008f;
                FacingViewer(b, dir * (ZoneInner - 0.01f) - side, dir * (ZoneInner - 0.01f) + side, dir * (ZoneOuter + 0.01f) + side, dir * (ZoneOuter + 0.01f) - side, -0.002f);
            }
            var old = m_ZoneMesh.sharedMesh;
            m_ZoneMesh.sharedMesh = b.ToMesh("GreenZone");
            if (old != null) Destroy(old);
            m_Zone.localRotation = Quaternion.Euler(0f, 0f, -ZoneStart);
        }

        /// <summary>A quad seen from the clock's front (local -Z), whatever order its corners come in.</summary>
        static void FacingViewer(MeshBuilder b, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float z = 0f)
        {
            var o = new Vector3(0f, 0f, z);
            p0 += o; p1 += o; p2 += o; p3 += o;
            if (Vector3.Cross(p1 - p0, p3 - p0).z > 0f) { var s = p1; p1 = p3; p3 = s; }
            b.AddQuad(p0, p1, p2, p3, Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
        }

        void UpdateLamps()
        {
            for (int i = 0; i < HitsToWin; i++)
                if (m_Lamps[i] != null) m_Lamps[i].sharedMaterial = Won ? m_LampWon : i < Hits ? m_LampOn : m_LampOff;
        }

        void PlaceNeedle()
        {
            if (m_Needle != null) m_Needle.localRotation = Quaternion.Euler(0f, 0f, -NeedleAngle);
        }

        void OnPress()
        {
            if (Won || m_Freeze > 0f || m_Cooldown > 0f) return;
            m_Cooldown = 0.25f;
            float r = Rel(NeedleAngle);
            bool hit = r <= ZoneWidth + Grace || r >= 360f - Grace;
            var face = FaceCenter;
            if (hit)
            {
                Hits++;
                AudioDirector.I?.Play("clock_tick", face, 1f, 1f);
                if (Hits < HitsToWin) AudioDirector.I?.Play("clock_chime", face, 0.55f, 1.5f + 0.15f * Hits);
                m_Freeze = 0.55f;
                m_PendingLevel = true;
            }
            else
            {
                Misses++;
                Hits = 0;
                AudioDirector.I?.Play("phone_buzz", ButtonPosition, 0.9f, 0.6f);
                AudioDirector.I?.Play("dial_click", face, 0.7f, 0.5f);
                SetLevel(0, true);
            }
            m_Flash = hit ? 0.55f : 0.7f;
            m_FlashHit = hit;
            UpdateLamps();
            if (Hits >= HitsToWin) Win();
            Pressed?.Invoke(this, hit);
            if (Won) Completed?.Invoke(this);
        }

        void Win()
        {
            Won = true;
            Hits = HitsToWin;
            if (Button != null) Button.Usable = false;
            if (Face != null) Face.Usable = false;
            UpdateLamps();
            StartCoroutine(StopAtMidnight());
        }

        IEnumerator PushCap()
        {
            var cap = Button.Cap;
            var home = cap.localPosition;
            cap.localPosition = home + Button.PressOffset;
            yield return new WaitForSeconds(0.15f);
            cap.localPosition = home;
        }

        IEnumerator StopAtMidnight()
        {
            // The needle holds where it was stopped, then sweeps on to XII: midnight at last.
            yield return new WaitForSeconds(0.5f);
            AudioDirector.I?.Play("clock_chime", FaceCenter, 1f, 1f);
            float from = NeedleAngle, to = 360f;
            for (float k = 0f; k < 1f; k += Time.deltaTime / 1.1f)
            {
                NeedleAngle = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, k));
                PlaceNeedle();
                m_DialMat.SetColor("_EmissionColor", Color.Lerp(new Color(0.55f, 0.5f, 0.36f), new Color(0.9f, 0.75f, 0.4f), k));
                yield return null;
            }
            NeedleAngle = 0f;
            PlaceNeedle();
            if (m_Zone != null) m_Zone.gameObject.SetActive(false);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (m_Cooldown > 0f) m_Cooldown -= dt;
            if (m_Flash > 0f)
            {
                m_Flash -= dt;
                float k = Mathf.Clamp01(m_Flash / 0.6f);
                var flashBase = m_FlashHit ? new Color(0.85f, 1f, 0.85f) : new Color(0.9f, 0.12f, 0.1f);
                var flashGlow = m_FlashHit ? new Color(2.2f, 3f, 2.2f) : new Color(2.6f, 0.25f, 0.2f);
                m_ZoneMat.SetColor("_BaseColor", Color.Lerp(ZoneBase, flashBase, k));
                m_ZoneMat.SetColor("_EmissionColor", Color.Lerp(ZoneGlow, flashGlow, k));
            }
            if (Won) return;
            if (m_Freeze > 0f)
            {
                m_Freeze -= dt;
                if (m_Freeze <= 0f && m_PendingLevel)
                {
                    // The next round: the zone jumps somewhere new, a little smaller, and the needle runs faster.
                    m_PendingLevel = false;
                    SetLevel(Hits, true);
                    AudioDirector.I?.Play("dial_click", FaceCenter, 0.6f, 1.4f);
                }
                return;
            }
            NeedleAngle = Mathf.Repeat(NeedleAngle + Speed * dt, 360f);
            PlaceNeedle();
        }
    }
}
