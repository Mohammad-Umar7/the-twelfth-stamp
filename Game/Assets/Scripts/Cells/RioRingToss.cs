using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// Rio's arcade game: a carnival ring toss stall on the avenue in front of the float. A striped awning, a counter with a
    /// basket of rings, and behind it four tiers, each with a bottle sliding left and right on its rail at its own speed.
    /// Aim over the counter and use (E / click / trigger): a ring flies from your hand in a spinning arc and comes down on the
    /// tier you pointed at, where you pointed. A ring that drops over a bottle's neck (its centre within CatchRadius of the
    /// bottle's axis as it falls past the bottle top) is a ringer: the bottle flashes, a coin sound, the ring stays on it.
    /// Misses bounce and vanish. Five ringers win.
    /// </summary>
    public class RingToss : MonoBehaviour
    {
        public const int Rows = 4, Target = 5;
        public const float CatchRadius = 0.2f, RingR = 0.15f, Amp = 1.0f, PegTall = 0.4f;
        public static readonly float[] RowZ = { 1.35f, 1.95f, 2.55f, 3.15f };
        public static readonly float[] TierTop = { 0.62f, 0.74f, 0.86f, 0.98f };
        static readonly float[] Omega = { 0.62f, 0.95f, 0.78f, 1.15f }, Phase0 = { 0f, 1.9f, 3.3f, 4.6f };
        static readonly Color[] RingCols = { new Color(1f, 0.85f, 0.15f), new Color(1f, 0.25f, 0.7f), new Color(0.15f, 0.9f, 0.95f), new Color(1f, 0.5f, 0.12f) };

        public Interactable ThrowZone;
        public readonly Transform[] Pegs = new Transform[Rows];
        public int Score { get; private set; }
        public int Thrown { get; private set; }
        public int Misses { get; private set; }
        public int InFlight { get; private set; }
        public bool Won => Score >= Target;
        public float Clock => m_Clock;
        public event System.Action<RingToss, int> Scored;   // the row whose bottle took the ring
        public event System.Action<RingToss> Missed;

        /// <summary>Where the player stands to play (on the avenue, in front of the counter), for the guide and the tests.</summary>
        public Vector3 PlayerSpot => transform.TransformPoint(new Vector3(0f, 0f, -0.6f));
        /// <summary>The middle of the bottles, for the guide's marker.</summary>
        public Vector3 TargetsPosition => transform.TransformPoint(new Vector3(0f, 1.2f, 2.2f));

        float m_Clock, m_Cooldown;
        Mesh m_RingMesh;
        readonly Material[] m_RingMats = new Material[4];
        readonly Renderer[] m_PegGlass = new Renderer[Rows];
        readonly Material[] m_PegMat = new Material[Rows];
        readonly float[] m_Flash = new float[Rows];
        readonly int[] m_OnPeg = new int[Rows];
        readonly List<GameObject> m_Rings = new List<GameObject>();
        readonly Renderer[] m_Bulbs = new Renderer[Target];
        Material m_FlashMat, m_BulbOff, m_BulbOn;
        TMPro.TextMeshPro m_ScoreText;

        public static RingToss Build(Transform parent, Vector3 pos, float yaw)
        {
            var go = new GameObject("RingTossStall");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var r = go.AddComponent<RingToss>();
            r.Construct();
            return r;
        }

        public static float PegTopY(int i) => TierTop[i] + PegTall;
        static float PegX(int i, float clock) => Amp * Mathf.Sin(Omega[i] * clock + Phase0[i]);
        Vector3 PegLocal(int i) => new Vector3(PegX(i, m_Clock), TierTop[i], RowZ[i]);

        /// <summary>Where bottle <paramref name="i"/>'s top will be <paramref name="ahead"/> seconds from now (tests).</summary>
        public Vector3 PredictPegTop(int i, float ahead) => transform.TransformPoint(new Vector3(PegX(i, m_Clock + ahead), PegTopY(i), RowZ[i]));
        public Vector3 PegTop(int i) => PredictPegTop(i, 0f);
        public int RingsOn(int i) => m_OnPeg[i];

        // ------------------------------------------------------------------ construction
        void Construct()
        {
            var t = transform;
            var k = new KitBatch();
            var red = Mats.Lit(new Color(0.85f, 0.1f, 0.12f), 0.4f);
            var white = Mats.Lit(new Color(0.95f, 0.93f, 0.88f), 0.4f);
            var yellow = Mats.Lit(new Color(1f, 0.78f, 0.15f), 0.4f);
            var teal = Mats.Lit(new Color(0.05f, 0.55f, 0.55f), 0.4f);
            var wood = Mats.Lit(new Color(0.75f, 0.55f, 0.35f), 0.4f, 0f, "wood_dark");
            var chrome = Mats.Lit(new Color(0.86f, 0.87f, 0.9f), 0.85f, 0.85f);
            var gold = Mats.Lit(new Color(0.95f, 0.72f, 0.3f), 0.7f, 0.7f, null, false, new Color(0.12f, 0.08f, 0.02f));
            var bulbs = new[]
            {
                Mats.Emissive(new Color(1f, 0.85f, 0.6f), new Color(1.5f, 1.1f, 0.6f)), Mats.Emissive(new Color(1f, 0.3f, 0.25f), new Color(1.5f, 0.22f, 0.16f)),
                Mats.Emissive(new Color(0.4f, 1f, 0.45f), new Color(0.3f, 1.35f, 0.35f)), Mats.Emissive(new Color(0.3f, 0.85f, 1f), new Color(0.2f, 0.95f, 1.45f)),
                Mats.Emissive(new Color(1f, 0.35f, 0.9f), new Color(1.45f, 0.3f, 1.2f)),
            };

            // Counter: red and white stripes down the front, a wooden top. (Its collider keeps the player outside.)
            MeshFactory.Box("CounterBody", t, new Vector3(0f, 0.5f, 0.225f), new Vector3(3.1f, 1.0f, 0.45f), white, true);
            for (int i = 0; i < 10; i++)
                k.Box(i % 2 == 0 ? red : white, new Vector3(-1.395f + i * 0.31f, 0.5f, -0.006f), new Vector3(0.31f, 0.98f, 0.01f));
            k.Box(wood, new Vector3(0f, 1.025f, 0.22f), new Vector3(3.24f, 0.05f, 0.56f));
            // Posts, side and back walls, the roof.
            foreach (var x in new[] { -1.6f, 1.6f })
            {
                k.Box(yellow, new Vector3(x, 1.5f, -0.05f), new Vector3(0.1f, 3.0f, 0.1f));
                k.Box(yellow, new Vector3(x, 1.5f, 3.68f), new Vector3(0.1f, 3.0f, 0.1f));
                k.Box(teal, new Vector3(x, 1.45f, 1.82f), new Vector3(0.06f, 2.9f, 3.7f));
                k.Box(yellow, new Vector3(x, 0.15f, 1.82f), new Vector3(0.08f, 0.3f, 3.7f));
                Solid(t, new Vector3(x, 1.45f, 1.82f), new Vector3(0.1f, 2.9f, 3.7f));
            }
            k.Box(teal, new Vector3(0f, 1.45f, 3.7f), new Vector3(3.26f, 2.9f, 0.06f));
            Solid(t, new Vector3(0f, 1.45f, 3.7f), new Vector3(3.3f, 2.9f, 0.1f));
            for (int i = 0; i < 11; i++)
                k.Box(i % 2 == 0 ? red : white, new Vector3(-1.55f + i * 0.31f, 3.0f, 1.72f), new Vector3(0.31f, 0.06f, 3.95f));
            // The striped awning over the front, its scalloped valance and a string of bulbs.
            for (int i = 0; i < 11; i++)
            {
                float x = -1.55f + i * 0.31f;
                k.Box(i % 2 == 0 ? red : white, new Vector3(x, 2.875f, -0.27f), new Vector3(0.31f, 0.03f, 1.02f), Quaternion.Euler(-14.7f, 0f, 0f));
                k.Box(i % 2 == 0 ? white : red, new Vector3(x, 2.68f, -0.77f), new Vector3(0.3f, 0.16f, 0.02f));
                k.Of(i % 2 == 0 ? white : red).AddSphere(new Vector3(x, 2.6f, -0.77f), 0.075f, 10, 5, 1f, -90f, 0f);
            }
            for (int i = 0; i < 12; i++) k.Of(bulbs[i % bulbs.Length]).AddSphere(new Vector3(-1.65f + i * 0.3f, 2.52f, -0.8f), 0.03f, 8, 5);
            foreach (var x in new[] { -1.66f, 1.66f })
                for (int i = 0; i < 7; i++) k.Of(bulbs[(i + (x < 0 ? 0 : 2)) % bulbs.Length]).AddSphere(new Vector3(x, 0.3f + i * 0.34f, -0.11f), 0.03f, 8, 5);
            // The sign board over the opening.
            k.Box(yellow, new Vector3(0f, 2.33f, -0.07f), new Vector3(3.1f, 0.34f, 0.05f));
            k.Box(red, new Vector3(0f, 2.33f, -0.06f), new Vector3(3.16f, 0.4f, 0.03f));
            var title = TextFactory.World(t, "RING TOSS", 0.2f, new Color(0.75f, 0.05f, 0.1f), TMPro.TextAlignmentOptions.Center, true, 3f);
            title.fontStyle = TMPro.FontStyles.Bold;
            title.transform.localPosition = new Vector3(0f, 2.33f, -0.1f);

            // Four tiers stepping up toward the back, each with a chrome rail and a bottle sliding on it.
            var tierCols = new[] { red, yellow, teal, Mats.Lit(new Color(0.55f, 0.2f, 0.75f), 0.4f) };
            var glassCols = new[] { new Color(0.2f, 0.75f, 0.35f), new Color(0.15f, 0.45f, 0.95f), new Color(0.95f, 0.45f, 0.15f), new Color(0.9f, 0.2f, 0.55f) };
            m_FlashMat = Mats.Emissive(new Color(1f, 0.95f, 0.7f), new Color(3f, 2.6f, 1.4f));
            for (int i = 0; i < Rows; i++)
            {
                float h = TierTop[i] - 0.02f;
                k.Box(tierCols[i], new Vector3(0f, h / 2f, RowZ[i]), new Vector3(3.08f, h, 0.58f));
                k.Box(wood, new Vector3(0f, h - 0.01f, RowZ[i]), new Vector3(3.08f, 0.02f, 0.6f));
                k.Box(chrome, new Vector3(0f, TierTop[i] - 0.01f, RowZ[i]), new Vector3(2.6f, 0.02f, 0.03f));
                foreach (var x in new[] { -1.3f, 1.3f }) k.Box(chrome, new Vector3(x, TierTop[i] + 0.02f, RowZ[i]), new Vector3(0.03f, 0.06f, 0.05f));
                var peg = new GameObject("Bottle" + i).transform;
                peg.SetParent(t, false);
                Pegs[i] = peg;
                m_PegMat[i] = Mats.Lit(glassCols[i], 0.9f, 0.1f, null, false, glassCols[i] * 0.15f);
                MeshFactory.Cylinder("Foot", peg, Vector3.zero, 0.05f, 0.015f, chrome, 12, false);
                var body = new MeshBuilder();
                body.AddLathe(Vector3.zero, new[]
                {
                    new Vector2(0.001f, 0.012f), new Vector2(0.064f, 0.015f), new Vector2(0.068f, 0.03f), new Vector2(0.068f, 0.23f), new Vector2(0.055f, 0.285f),
                    new Vector2(0.027f, 0.32f), new Vector2(0.025f, 0.385f), new Vector2(0.001f, 0.386f),
                }, 16);
                m_PegGlass[i] = MeshFactory.FromBuilder("Glass", body, m_PegMat[i], peg).GetComponent<Renderer>();
                MeshFactory.Cylinder("Cap", peg, new Vector3(0f, 0.382f, 0f), 0.03f, 0.018f, gold, 12, false);
            }
            Tick(0f);

            // Back wall: the prize and the score.
            var prize = TextFactory.World(t, "5 RINGERS WIN A PASSPORT STAMP", 0.07f, new Color(1f, 0.95f, 0.7f), TMPro.TextAlignmentOptions.Center, false, 2.8f);
            prize.transform.localPosition = new Vector3(0f, 2.45f, 3.66f);
            m_ScoreText = TextFactory.World(t, "RINGERS  0 / 5", 0.13f, new Color(1f, 0.85f, 0.3f), TMPro.TextAlignmentOptions.Center, false, 2.6f);
            m_ScoreText.fontStyle = TMPro.FontStyles.Bold;
            m_ScoreText.transform.localPosition = new Vector3(0f, 2.12f, 3.66f);
            m_BulbOff = Mats.Lit(new Color(0.25f, 0.22f, 0.2f), 0.6f);
            m_BulbOn = Mats.Emissive(new Color(1f, 0.85f, 0.3f), new Color(2.6f, 1.9f, 0.5f));
            for (int i = 0; i < Target; i++)
                m_Bulbs[i] = MeshFactory.Sphere("ScoreBulb" + i, t, new Vector3(-0.6f + i * 0.3f, 1.82f, 3.64f), 0.06f, m_BulbOff, 12, 8).GetComponent<Renderer>();
            // A big golden stamp hangs as the prize, plush prizes in the corners.
            k.Of(gold).AddCylinder(new Vector3(-1.15f, 2.1f, 3.66f), 0.2f, 0.03f, 24, 1f, Quaternion.Euler(-90f, 0f, 0f));
            var stamp = TextFactory.World(t, "STAMP", 0.06f, new Color(0.45f, 0.25f, 0.05f), TMPro.TextAlignmentOptions.Center, false, 0.4f);
            stamp.transform.localPosition = new Vector3(-1.15f, 2.1f, 3.625f);
            var plush = new[] { Mats.Lit(new Color(0.98f, 0.7f, 0.8f), 0.1f), Mats.Lit(new Color(0.6f, 0.85f, 0.95f), 0.1f), Mats.Lit(new Color(0.95f, 0.92f, 0.6f), 0.1f) };
            for (int i = 0; i < 3; i++)
            {
                var p = new Vector3(1.0f + i * 0.2f - 0.2f, 1.5f, 3.5f);
                k.Of(plush[i]).AddSphere(p + new Vector3(0f, 0.08f, 0f), 0.09f, 12, 7);
                k.Of(plush[i]).AddSphere(p + new Vector3(0f, 0.22f, 0f), 0.065f, 12, 7);
            }
            k.Box(wood, new Vector3(1.0f, 1.49f, 3.52f), new Vector3(0.8f, 0.03f, 0.22f));

            // The ring mesh and the basket of rings on the counter.
            var rb = new MeshBuilder();
            rb.AddTorus(Vector3.zero, RingR, 0.016f, 28, 8, Quaternion.identity);
            m_RingMesh = rb.ToMesh("Ring");
            for (int i = 0; i < m_RingMats.Length; i++) m_RingMats[i] = Mats.Lit(RingCols[i], 0.5f, 0f, null, false, RingCols[i] * 0.3f);
            var basket = new Vector3(1.12f, 1.05f, 0.2f);
            k.Of(Mats.Lit(new Color(0.7f, 0.5f, 0.28f), 0.2f, 0f, "wood_dark")).AddFrustum(basket, 0.17f, 0.21f, 0.14f, 18);
            for (int i = 0; i < 6; i++)
            {
                var ring = MeshFactory.Make("BasketRing", m_RingMesh, m_RingMats[i % m_RingMats.Length], t, basket + new Vector3(Mathf.Sin(i * 2.1f) * 0.03f, 0.1f + i * 0.018f, Mathf.Cos(i * 2.1f) * 0.03f),
                    Quaternion.Euler(Mathf.Sin(i * 1.7f) * 14f, i * 40f, Mathf.Cos(i * 1.3f) * 12f));
                ring.transform.localScale = Vector3.one * 0.8f;
            }
            var hint = TextFactory.World(t, "aim over the counter, throw!", 0.035f, new Color(0.4f, 0.2f, 0.05f), TMPro.TextAlignmentOptions.Center, false, 1.2f);
            hint.transform.localPosition = new Vector3(0f, 1.051f, 0.05f);
            hint.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            k.Bake(t, "RingTossDetail");

            // The throw: aim anywhere through the stall's open front (an invisible trigger over the counter) and use.
            var zone = new GameObject("ThrowZone");
            zone.transform.SetParent(t, false);
            zone.transform.localPosition = new Vector3(0f, 1.62f, 0.48f);
            var zc = zone.AddComponent<BoxCollider>();
            zc.isTrigger = true;
            zc.size = new Vector3(2.95f, 1.14f, 0.05f);
            ThrowZone = zone.AddComponent<Interactable>();
            ThrowZone.Prompt = "Throw a ring";
            ThrowZone.MaxDistance = 3.0f;
            ThrowZone.OnUse = who => ThrowFrom(who);
        }

        static void Solid(Transform parent, Vector3 center, Vector3 size)
        {
            var go = new GameObject("Solid");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.AddComponent<BoxCollider>().size = size;
        }

        // ------------------------------------------------------------------ the game
        public void ResetGame()
        {
            StopAllCoroutines();
            foreach (var r in m_Rings) if (r != null) Destroy(r);
            m_Rings.Clear();
            Score = 0;
            Thrown = 0;
            Misses = 0;
            InFlight = 0;
            for (int i = 0; i < Rows; i++) { m_OnPeg[i] = 0; m_Flash[i] = 0f; m_PegGlass[i].sharedMaterial = m_PegMat[i]; }
            if (ThrowZone != null) ThrowZone.Usable = true;
            RefreshScore();
        }

        /// <summary>Won on an earlier visit: five rings on the bottles, nothing more to throw.</summary>
        public void SetSolved()
        {
            ResetGame();
            for (int s = 0; s < Target; s++)
            {
                int i = s % Rows;
                MakeRing(Pegs[i], RestPoint(i), s);
                m_OnPeg[i]++;
            }
            Score = Target;
            if (ThrowZone != null) ThrowZone.Usable = false;
            RefreshScore();
        }

        void RefreshScore()
        {
            if (m_ScoreText != null) m_ScoreText.text = Won ? "RINGERS  5 / 5   WINNER!" : "RINGERS  " + Score + " / " + Target;
            for (int i = 0; i < Target; i++) if (m_Bulbs[i] != null) m_Bulbs[i].sharedMaterial = i < Score ? m_BulbOn : m_BulbOff;
        }

        /// <summary>Throws one ring along the interactor's aim (desktop: from just below the eyes; VR: from the hand).</summary>
        public bool ThrowFrom(Interactor who)
        {
            if (who == null || who.RayOrigin == null) return false;
            var o = who.RayOrigin;
            return ThrowAlong(o.position, o.forward, ThrowStart(who));
        }

        /// <summary>Where a ring thrown by <paramref name="who"/> leaves the hand.</summary>
        public static Vector3 ThrowStart(Interactor who)
        {
            var o = who.RayOrigin;
            if (who.Side == HandSide.Desktop || who.HoldPoint == null) return o.position + o.forward * 0.35f + o.right * 0.14f - o.up * 0.2f;
            return who.HoldPoint.position;
        }

        /// <summary>Throws a ring from <paramref name="start"/> to where the aim ray (origin, dir) comes down in the stall.</summary>
        public bool ThrowAlong(Vector3 origin, Vector3 dir, Vector3 start)
        {
            if (Won || m_Cooldown > 0f) return false;
            m_Cooldown = 0.3f;
            Thrown++;
            StartCoroutine(Fly(transform.InverseTransformPoint(start), transform.InverseTransformPoint(LandingPoint(origin, dir))));
            return true;
        }

        /// <summary>
        /// Where a ring aimed along this ray lands: on the tier whose bottle-top line the ray passes closest to (so pointing at
        /// a bottle, or at the empty rail ahead of one, puts the ring there), at the ray's x on that tier.
        /// </summary>
        public Vector3 LandingPoint(Vector3 originW, Vector3 dirW)
        {
            var o = transform.InverseTransformPoint(originW);
            var d = transform.InverseTransformDirection(dirW);
            int best = -1;
            float bestErr = float.MaxValue;
            Vector3 bestP = Vector3.zero;
            if (d.z > 0.05f)
                for (int i = 0; i < Rows; i++)
                {
                    float t = (RowZ[i] - o.z) / d.z;
                    if (t <= 0f) continue;
                    var p = o + d * t;
                    float err = Mathf.Abs(p.y - PegTopY(i));
                    if (err < bestErr) { bestErr = err; best = i; bestP = p; }
                }
            if (best < 0) { best = 1; bestP = new Vector3(o.x, 0f, RowZ[1]); } // not pointing into the stall: a lob straight ahead
            return transform.TransformPoint(new Vector3(Mathf.Clamp(bestP.x, -1.45f, 1.45f), PegTopY(best), RowZ[best]));
        }

        /// <summary>How long a ring takes from start to landing (both world points).</summary>
        public float FlightTime(Vector3 startW, Vector3 landW)
        {
            var d = transform.InverseTransformPoint(landW) - transform.InverseTransformPoint(startW);
            d.y = 0f;
            return 0.42f + 0.09f * d.magnitude;
        }

        Transform MakeRing(Transform parent, Vector3 local, int colour)
        {
            var go = MeshFactory.Make("Ring", m_RingMesh, m_RingMats[colour % m_RingMats.Length], parent, local);
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            m_Rings.Add(go);
            return go.transform;
        }

        Vector3 RestPoint(int row) => new Vector3(0f, 0.03f + m_OnPeg[row] * 0.035f, 0f);

        static float GroundAt(Vector3 p)
        {
            if (Mathf.Abs(p.x) < 1.57f)
            {
                if (p.z > -0.02f && p.z < 0.5f) return 1.05f;
                for (int i = 0; i < Rows; i++) if (Mathf.Abs(p.z - RowZ[i]) <= 0.3f) return TierTop[i] - 0.02f;
            }
            return 0f;
        }

        IEnumerator Fly(Vector3 s, Vector3 land)
        {
            InFlight++;
            var ring = MakeRing(transform, s, Thrown);
            var g = new Vector3(0f, -9.81f, 0f);
            var startW = transform.TransformPoint(s);
            float T = FlightTime(startW, transform.TransformPoint(land));
            var v0 = (land - s - 0.5f * g * T * T) / T;
            AudioDirector.I?.Play("grab", startW, 0.5f, 1.6f);
            float time = 0f, spin = Random.Range(0f, 360f);
            var prev = s;
            int caught = -1;
            bool landed = false;
            while (time < 4f)
            {
                time += Time.deltaTime;
                var p = s + v0 * time + 0.5f * g * time * time;
                // The stall's walls stop it going further; it drops straight down there.
                if (p.z > 3.6f) p.z = 3.6f;
                if (prev.z >= 0.45f && Mathf.Abs(p.x) > 1.52f) p.x = Mathf.Sign(p.x) * 1.52f;
                if (p.y < prev.y)
                    for (int i = 0; i < Rows && caught < 0; i++)
                    {
                        float top = PegTopY(i);
                        if (prev.y < top || p.y >= top) continue;
                        float f = (prev.y - top) / Mathf.Max(1e-5f, prev.y - p.y);
                        var c = Vector3.Lerp(prev, p, f);
                        var axis = PegLocal(i);
                        if (new Vector2(c.x - axis.x, c.z - axis.z).magnitude < CatchRadius) { caught = i; p = c; }
                    }
                spin += 900f * Time.deltaTime;
                ring.localPosition = p;
                ring.localRotation = Quaternion.Euler(10f * Mathf.Sin(time * 11f), spin, 8f * Mathf.Cos(time * 9f));
                if (caught >= 0) break;
                float gy = GroundAt(p);
                if (p.y <= gy + 0.016f && v0.y + g.y * time < 0f) { ring.localPosition = new Vector3(p.x, gy + 0.016f, p.z); landed = true; break; }
                prev = p;
                yield return null;
            }
            if (caught >= 0) yield return Ringer(ring, caught);
            else yield return Miss(ring, landed);
            InFlight--;
        }

        IEnumerator Ringer(Transform ring, int i)
        {
            ring.SetParent(Pegs[i], true);
            var rest = RestPoint(i);
            m_OnPeg[i]++;
            Score++;
            m_Flash[i] = 0.5f;
            AudioDirector.I?.Play("coin_token", Pegs[i].position + Vector3.up * 0.4f, 1f, 1f + Score * 0.06f);
            AudioDirector.I?.Play("dial_click", Pegs[i].position + Vector3.up * 0.4f, 0.7f, 1.5f);
            RefreshScore();
            if (Won && ThrowZone != null) ThrowZone.Usable = false;
            Scored?.Invoke(this, i);
            var from = ring.localPosition;
            var tilt = Quaternion.Euler(Random.Range(-8f, 8f), Random.Range(0f, 360f), Random.Range(-8f, 8f));
            var rot0 = ring.localRotation;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.3f)
            {
                ring.localPosition = Vector3.Lerp(from, rest, t * t);
                ring.localRotation = Quaternion.Slerp(rot0, tilt, t);
                yield return null;
            }
            ring.localPosition = rest;
            ring.localRotation = tilt;
            AudioDirector.I?.Play("drop_soft", ring.position, 0.5f, 1.3f);
        }

        IEnumerator Miss(Transform ring, bool landed)
        {
            Misses++;
            Missed?.Invoke(this);
            if (landed) AudioDirector.I?.Play("drop_soft", ring.position, 0.7f, 1.2f);
            var at = ring.localPosition;
            // A little bounce, then it shrinks away.
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.35f)
            {
                ring.localPosition = at + Vector3.up * (0.08f * Mathf.Sin(t * Mathf.PI));
                ring.localRotation = Quaternion.Euler(70f * t, ring.localEulerAngles.y + 200f * Time.deltaTime, 0f);
                yield return null;
            }
            yield return new WaitForSeconds(0.3f);
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.35f)
            {
                ring.localScale = Vector3.one * (1f - t);
                yield return null;
            }
            m_Rings.Remove(ring.gameObject);
            Destroy(ring.gameObject);
        }

        void Tick(float dt)
        {
            m_Clock += dt;
            for (int i = 0; i < Rows; i++) if (Pegs[i] != null) Pegs[i].localPosition = PegLocal(i);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Tick(dt);
            m_Cooldown -= dt;
            for (int i = 0; i < Rows; i++)
            {
                if (m_Flash[i] <= 0f) continue;
                m_Flash[i] -= dt;
                m_PegGlass[i].sharedMaterial = m_Flash[i] > 0f && Mathf.Repeat(m_Flash[i], 0.16f) > 0.08f ? m_FlashMat : m_PegMat[i];
            }
        }
    }
}
