using System;
using System.Collections;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// Morocco's arcade game, Memory Match: eight cards face down on a felt board on the haunted shop's counter (4 x 2),
    /// four pairs of glasses, cameras, passports and stamps. Flip a card (it turns over through 180 degrees), then another:
    /// a pair stays face up and glows, two that differ turn back over after a moment. All four pairs and the counter gives
    /// up its secret. Each card's face carries a small model built from primitives.
    /// </summary>
    public class MemoryMatch : MonoBehaviour
    {
        public const int Pairs = 4, Cols = 4, Rows = 2;
        public static readonly string[] KindNames = { "glasses", "camera", "passport", "stamp" };
        const float PitchX = 0.24f, PitchZ = 0.26f, CardW = 0.17f, CardD = 0.22f, FlipSeconds = 0.34f, MismatchSeconds = 0.9f;

        sealed class Card
        {
            public Interactable It;
            public Transform Flipper, Icon;
            public Renderer Plate;
            public GameObject Glow;
            public int Kind;
            public bool FaceUp, Matched, Turning;
        }

        readonly Card[] m_Cards = new Card[Pairs * 2];
        int m_Waiting = -1;   // the face-up card waiting for its partner
        Material m_Ivory, m_IvoryLit, m_Teal, m_Gold, m_GlowMat;
        Material m_Black, m_Lens, m_Chrome, m_Navy, m_Rubber, m_Wood, m_Brass;

        public int Count => m_Cards.Length;
        public int MatchedPairs { get; private set; }
        public int Flips { get; private set; }
        public int Mismatches { get; private set; }
        public bool Solved => MatchedPairs >= Pairs;
        /// <summary>A pair is being checked (the second card is turning, or two wrong ones wait to turn back).</summary>
        public bool Resolving { get; private set; }
        public event Action<MemoryMatch> FirstFlip;
        public event Action<MemoryMatch, int, bool> PairChecked;   // the first card's kind, matched?
        public event Action<MemoryMatch> Completed;

        public Interactable CardAt(int i) => m_Cards[i].It;
        public int KindAt(int i) => m_Cards[i].Kind;
        public bool IsFaceUp(int i) => m_Cards[i].FaceUp;
        public bool IsMatched(int i) => m_Cards[i].Matched;
        public bool IsTurning(int i) => m_Cards[i].Turning;
        public Vector3 BoardPosition => transform.position;

        /// <summary>The nearest face-down card to a point (the guide's marker), or the board when none is left.</summary>
        public Vector3 NextCardPosition(Vector3 from)
        {
            Vector3? best = null;
            float bd = float.MaxValue;
            foreach (var c in m_Cards)
            {
                if (c == null || c.FaceUp || c.Matched) continue;
                float d = (c.It.transform.position - from).sqrMagnitude;
                if (d < bd) { bd = d; best = c.It.transform.position; }
            }
            return best ?? BoardPosition;
        }

        /// <summary>A board centred on <paramref name="topCenter"/> (its top surface, parent space), the cards in rows across it.</summary>
        public static MemoryMatch Build(Transform parent, Vector3 topCenter)
        {
            var go = new GameObject("MemoryMatch");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = topCenter;
            var m = go.AddComponent<MemoryMatch>();
            m.Construct();
            m.Deal();
            return m;
        }

        void Construct()
        {
            var t = transform;
            m_Ivory = Mats.Lit(new Color(0.93f, 0.89f, 0.8f), 0.3f, 0f, "paper_ivory");
            m_IvoryLit = Mats.Lit(new Color(1f, 0.95f, 0.82f), 0.3f, 0f, null, false, new Color(0.75f, 0.6f, 0.3f));
            m_Teal = Mats.Lit(new Color(0.07f, 0.3f, 0.3f), 0.45f);
            m_Gold = Mats.Lit(new Color(0.92f, 0.72f, 0.34f), 0.75f, 1f, null, false, new Color(0.25f, 0.17f, 0.04f));
            m_GlowMat = Mats.Unlit(new Color(1f, 0.82f, 0.42f, 0.6f), Tex.SoftDot, true, 1.5f);
            m_Black = Mats.Lit(new Color(0.04f, 0.04f, 0.045f), 0.55f, 0.2f);
            m_Lens = Mats.Lit(new Color(0.55f, 0.75f, 0.85f), 0.95f, 0.1f, null, false, new Color(0.05f, 0.12f, 0.16f));
            m_Chrome = Mats.Lit(new Color(0.8f, 0.81f, 0.83f), 0.85f, 1f);
            m_Navy = Mats.Lit(new Color(0.1f, 0.14f, 0.32f), 0.35f, 0f, "leather_navy");
            m_Rubber = Mats.Lit(new Color(0.62f, 0.08f, 0.07f), 0.25f);
            m_Wood = Mats.Lit(new Color(0.55f, 0.34f, 0.18f), 0.45f, 0f, "wood_dark");
            m_Brass = Mats.Lit(new Color(0.84f, 0.64f, 0.34f), 0.68f, 0.9f);

            // A green baize board with a brass edge, laid over the counter top.
            var felt = Mats.Lit(new Color(0.08f, 0.26f, 0.17f), 0.1f, 0f, "fabric_linen");
            float bw = Cols * PitchX + 0.02f, bd = Rows * PitchZ + 0.04f;
            MeshFactory.Box("Baize", t, new Vector3(0f, -0.008f, 0f), new Vector3(bw, 0.016f, bd), felt, true);
            var edge = new MeshBuilder();
            edge.AddBox(new Vector3(0f, -0.004f, -bd / 2f), new Vector3(bw + 0.02f, 0.024f, 0.012f));
            edge.AddBox(new Vector3(0f, -0.004f, bd / 2f), new Vector3(bw + 0.02f, 0.024f, 0.012f));
            edge.AddBox(new Vector3(-bw / 2f, -0.004f, 0f), new Vector3(0.012f, 0.024f, bd));
            edge.AddBox(new Vector3(bw / 2f, -0.004f, 0f), new Vector3(0.012f, 0.024f, bd));
            MeshFactory.FromBuilder("BrassEdge", edge, m_Brass, t);

            for (int i = 0; i < m_Cards.Length; i++)
            {
                int col = i % Cols, row = i / Cols;
                var root = new GameObject("Card" + i);
                root.transform.SetParent(t, false);
                root.transform.localPosition = new Vector3((col - (Cols - 1) / 2f) * PitchX, 0.0045f, (row - (Rows - 1) / 2f) * PitchZ);
                var col3 = root.AddComponent<BoxCollider>();
                col3.size = new Vector3(CardW, 0.04f, CardD);
                col3.center = new Vector3(0f, 0.01f, 0f);
                var c = new Card();
                c.Flipper = new GameObject("Flipper").transform;
                c.Flipper.SetParent(root.transform, false);
                // The card: ivory stock, its back (on top while face down) teal with a gilt border and a lens emblem, its
                // face (underneath until it turns over) framed in teal round the picture.
                c.Plate = MeshFactory.Box("Plate", c.Flipper, Vector3.zero, new Vector3(CardW, 0.007f, CardD), m_Ivory, false).GetComponent<Renderer>();
                MeshFactory.Box("BackBorder", c.Flipper, new Vector3(0f, 0.004f, 0f), new Vector3(CardW - 0.012f, 0.0012f, CardD - 0.012f), m_Gold, false);
                MeshFactory.Box("Back", c.Flipper, new Vector3(0f, 0.0048f, 0f), new Vector3(CardW - 0.026f, 0.0012f, CardD - 0.026f), m_Teal, false);
                var emblem = new MeshBuilder();
                emblem.AddTorus(new Vector3(0f, 0.0058f, 0f), 0.036f, 0.0045f, 24, 6, Quaternion.identity);
                emblem.AddTorus(new Vector3(0f, 0.0056f, 0f), 0.06f, 0.002f, 28, 4, Quaternion.identity);
                emblem.AddSphere(new Vector3(0f, 0.0055f, 0f), 0.011f, 10, 6);
                MeshFactory.FromBuilder("Emblem", emblem, m_Gold, c.Flipper);
                MeshFactory.Box("FaceFrame", c.Flipper, new Vector3(0f, -0.004f, 0f), new Vector3(CardW - 0.01f, 0.0012f, CardD - 0.01f), m_Teal, false);
                MeshFactory.Box("Face", c.Flipper, new Vector3(0f, -0.0048f, 0f), new Vector3(CardW - 0.024f, 0.0012f, CardD - 0.024f), m_Ivory, false);
                // The picture hangs under the face (upside down) and comes up as the card turns.
                c.Icon = new GameObject("Picture").transform;
                c.Icon.SetParent(c.Flipper, false);
                c.Icon.localPosition = new Vector3(0f, -0.0055f, 0f);
                c.Icon.localRotation = Quaternion.Euler(0f, 0f, 180f);
                c.Glow = MeshFactory.Quad("MatchGlow", root.transform, CardW + 0.1f, CardD + 0.1f, m_GlowMat, new Vector3(0f, -0.004f, 0f), Quaternion.Euler(90f, 0f, 0f));
                c.Glow.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                c.Glow.SetActive(false);
                c.It = root.AddComponent<Interactable>();
                c.It.Prompt = "Flip this card";
                c.It.MaxDistance = 2.4f;
                int id = i;
                c.It.OnUse = _ => OnCard(id);
                m_Cards[i] = c;
            }
        }

        /// <summary>Shuffles the four pairs and lays every card face down.</summary>
        public void Deal()
        {
            StopAllCoroutines();
            var kinds = new int[m_Cards.Length];
            for (int i = 0; i < kinds.Length; i++) kinds[i] = i / 2;
            for (int i = kinds.Length - 1; i > 0; i--) { int k = UnityEngine.Random.Range(0, i + 1); (kinds[i], kinds[k]) = (kinds[k], kinds[i]); }
            for (int i = 0; i < m_Cards.Length; i++)
            {
                var c = m_Cards[i];
                c.Kind = kinds[i];
                c.FaceUp = c.Matched = c.Turning = false;
                foreach (Transform old in c.Icon) Destroy(old.gameObject);
                BuildPicture(c.Icon, c.Kind);
                Pose(c, 0f, 0f);
                c.Plate.sharedMaterial = m_Ivory;
                c.Glow.SetActive(false);
            }
            m_Waiting = -1;
            MatchedPairs = 0;
            Flips = 0;
            Mismatches = 0;
            Resolving = false;
            RefreshUsable();
        }

        /// <summary>Every pair already found (the seal is verified): all face up and glowing.</summary>
        public void ShowSolved()
        {
            StopAllCoroutines();
            foreach (var c in m_Cards) { c.FaceUp = c.Matched = true; c.Turning = false; Pose(c, 180f, 0f); SetMatchedLook(c); }
            MatchedPairs = Pairs;
            m_Waiting = -1;
            Resolving = false;
            RefreshUsable();
        }

        void RefreshUsable()
        {
            foreach (var c in m_Cards) if (c != null && c.It != null) c.It.Usable = !Solved && !Resolving && !c.FaceUp && !c.Matched && !c.Turning;
        }

        void OnCard(int i)
        {
            var c = m_Cards[i];
            if (Solved || Resolving || c.FaceUp || c.Matched || c.Turning) return;
            Flips++;
            if (Flips == 1) FirstFlip?.Invoke(this);
            AudioDirector.I?.Play("dial_click", c.It.transform.position, 0.6f, 1.25f);
            StartCoroutine(Turn(c, true));
            if (m_Waiting < 0) { m_Waiting = i; RefreshUsable(); return; }
            var first = m_Cards[m_Waiting];
            m_Waiting = -1;
            StartCoroutine(Check(first, c));
        }

        IEnumerator Check(Card a, Card b)
        {
            Resolving = true;
            RefreshUsable();
            while (a.Turning || b.Turning) yield return null;
            if (a.Kind == b.Kind)
            {
                a.Matched = b.Matched = true;
                SetMatchedLook(a);
                SetMatchedLook(b);
                MatchedPairs++;
                AudioDirector.I?.Play("coin_token", b.It.transform.position, 0.8f, 1.1f);
                AudioDirector.I?.Play("pp_seam_shimmer", b.It.transform.position, 0.35f, 1.4f);
                Resolving = false;
                RefreshUsable();
                PairChecked?.Invoke(this, a.Kind, true);
                if (Solved) Completed?.Invoke(this);
                yield break;
            }
            Mismatches++;
            AudioDirector.I?.Play("drop_soft", b.It.transform.position, 0.5f, 0.7f);
            PairChecked?.Invoke(this, a.Kind, false);
            yield return new WaitForSeconds(MismatchSeconds);
            StartCoroutine(Turn(a, false));
            StartCoroutine(Turn(b, false));
            while (a.Turning || b.Turning) yield return null;
            Resolving = false;
            RefreshUsable();
        }

        IEnumerator Turn(Card c, bool up)
        {
            c.Turning = true;
            c.FaceUp = up;
            RefreshUsable();
            float from = up ? 0f : 180f, to = up ? 180f : 0f;
            for (float k = 0f; k < 1f; k += Time.deltaTime / FlipSeconds)
            {
                float s = Mathf.SmoothStep(0f, 1f, k);
                // Lifted clear of the baize while it turns over through 180 degrees (edge-on at the top of the arc).
                Pose(c, Mathf.Lerp(from, to, s), Mathf.Sin(s * Mathf.PI) * 0.12f);
                yield return null;
            }
            Pose(c, to, 0f);
            AudioDirector.I?.Play("drop_soft", c.It.transform.position, 0.35f, 1.6f);
            c.Turning = false;
            RefreshUsable();
        }

        static void Pose(Card c, float angle, float lift)
        {
            c.Flipper.localRotation = Quaternion.Euler(0f, 0f, angle);
            c.Flipper.localPosition = new Vector3(0f, lift, 0f);
        }

        void SetMatchedLook(Card c)
        {
            c.Plate.sharedMaterial = m_IvoryLit;
            c.Glow.SetActive(true);
        }

        void Update()
        {
            // The found pairs breathe a little.
            float k = 1f + Mathf.Sin(Time.time * 2.4f) * 0.06f;
            foreach (var c in m_Cards) if (c != null && c.Glow != null && c.Glow.activeSelf) c.Glow.transform.localScale = new Vector3(k, k, 1f);
        }

        // ------------------------------------------------------------------ the pictures (local +Y up, -Z toward the player)
        void BuildPicture(Transform p, int kind)
        {
            switch (kind)
            {
                case 0: // glasses: two round rims, a bridge, the temples folded back
                {
                    var frame = new MeshBuilder();
                    foreach (var x in new[] { -0.037f, 0.037f })
                        frame.AddTorus(new Vector3(x, 0.008f, -0.01f), 0.031f, 0.0055f, 24, 6, Quaternion.identity);
                    frame.AddTube(new[] { new Vector3(-0.007f, 0.009f, -0.012f), new Vector3(0f, 0.014f, -0.016f), new Vector3(0.007f, 0.009f, -0.012f) }, 0.0035f, 6);
                    foreach (var s in new[] { -1f, 1f })
                        frame.AddTube(new[] { new Vector3(s * 0.067f, 0.008f, -0.01f), new Vector3(s * 0.07f, 0.008f, 0.02f), new Vector3(s * 0.064f, 0.008f, 0.07f) }, 0.003f, 5);
                    MeshFactory.FromBuilder("Frames", frame, m_Black, p);
                    var lenses = new MeshBuilder();
                    foreach (var x in new[] { -0.037f, 0.037f }) lenses.AddCylinder(new Vector3(x, 0.006f, -0.01f), 0.027f, 0.003f, 20);
                    MeshFactory.FromBuilder("Lenses", lenses, m_Lens, p);
                    break;
                }
                case 1: // camera: the body, a chrome top, the lens barrel facing the player, a flash and a shutter button
                {
                    MeshFactory.Box("Body", p, new Vector3(0f, 0.032f, 0.012f), new Vector3(0.11f, 0.064f, 0.05f), m_Black, false);
                    MeshFactory.Box("TopPlate", p, new Vector3(0f, 0.067f, 0.012f), new Vector3(0.11f, 0.008f, 0.05f), m_Chrome, false);
                    MeshFactory.Box("Prism", p, new Vector3(0f, 0.078f, 0.012f), new Vector3(0.04f, 0.018f, 0.04f), m_Black, false);
                    var lens = new MeshBuilder();
                    lens.AddCylinder(new Vector3(0f, 0.032f, -0.013f), 0.024f, 0.032f, 20, 1f, Quaternion.Euler(-90f, 0f, 0f));
                    lens.AddCylinder(new Vector3(0f, 0.032f, -0.044f), 0.026f, 0.006f, 20, 1f, Quaternion.Euler(-90f, 0f, 0f));
                    MeshFactory.FromBuilder("Lens", lens, m_Chrome, p);
                    var glass = new MeshBuilder();
                    glass.AddDisc(new Vector3(0f, 0.032f, -0.0505f), Vector3.back, 0.018f, 18);
                    MeshFactory.FromBuilder("Glass", glass, m_Lens, p);
                    MeshFactory.Cylinder("Shutter", p, new Vector3(0.038f, 0.071f, 0.012f), 0.007f, 0.008f, m_Chrome, 10, false);
                    break;
                }
                case 2: // passport: a navy booklet with a gilt square emblem and a gilt line
                {
                    MeshFactory.Box("Booklet", p, new Vector3(0f, 0.008f, 0f), new Vector3(0.09f, 0.016f, 0.125f), m_Navy, false);
                    MeshFactory.Box("Pages", p, new Vector3(0.002f, 0.008f, 0f), new Vector3(0.088f, 0.012f, 0.121f), m_Ivory, false);
                    MeshFactory.Box("Cover", p, new Vector3(0f, 0.0155f, 0f), new Vector3(0.09f, 0.003f, 0.125f), m_Navy, false);
                    MeshFactory.Box("Emblem", p, new Vector3(0f, 0.0175f, 0.012f), new Vector3(0.036f, 0.002f, 0.036f), m_Gold, false);
                    MeshFactory.Box("Title", p, new Vector3(0f, 0.0175f, -0.03f), new Vector3(0.05f, 0.0015f, 0.007f), m_Gold, false);
                    MeshFactory.Box("Title2", p, new Vector3(0f, 0.0175f, 0.048f), new Vector3(0.04f, 0.0015f, 0.005f), m_Gold, false);
                    break;
                }
                default: // stamp: a red rubber block on a wooden mount, a turned handle and its knob
                {
                    MeshFactory.Box("Rubber", p, new Vector3(0f, 0.006f, 0f), new Vector3(0.075f, 0.012f, 0.055f), m_Rubber, false);
                    MeshFactory.Box("Mount", p, new Vector3(0f, 0.021f, 0f), new Vector3(0.07f, 0.018f, 0.05f), m_Wood, false);
                    MeshFactory.Box("Plate", p, new Vector3(0f, 0.031f, 0f), new Vector3(0.05f, 0.004f, 0.035f), m_Brass, false);
                    MeshFactory.Frustum("Handle", p, new Vector3(0f, 0.033f, 0f), 0.011f, 0.009f, 0.036f, m_Wood, 12);
                    MeshFactory.Sphere("Knob", p, new Vector3(0f, 0.076f, 0f), 0.019f, m_Wood, 12, 8);
                    break;
                }
            }
        }
    }
}
