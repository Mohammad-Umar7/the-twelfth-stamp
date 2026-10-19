using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// France: the bubble shooter. A big board floats upright over the Seine, its glowing frame facing the street, with a
    /// hex grid of coloured bubbles hanging from its top edge and a launcher at the bottom centre that shows the current
    /// and the next bubble. Each use fires the launcher's bubble toward the point the player aimed at (projected into the
    /// board's plane): it travels in that plane, bounces off the side walls and sticks to the first bubble (or the top) it
    /// touches, in the nearest free cell. Three or more of one colour pop; bubbles no longer hanging from the top drop.
    /// Clear <see cref="Target"/> bubbles (or the whole board) to win. Board space: x right, y up, bubbles in the plane
    /// z = <see cref="BubbleZ"/>, the player in front (-Z). Every renderer is built once (the cell caches them).
    /// </summary>
    public class BubbleBoard : MonoBehaviour
    {
        public const int Cols = 9, MaxRows = 9, StartRows = 6, ColourCount = 4;
        public const float W = 2.7f, H = 2.54f, FrameW = 3.0f, FrameH = 2.8f;
        public const float R = W / (Cols * 2 + 1), D = R * 2f, Pitch = D * 0.8660254f;
        public const float TopY = H / 2f - R, Speed = 7.5f, BubbleZ = -0.16f;
        public static readonly Vector2 Launcher = new Vector2(0f, -H / 2f + 0.16f);
        static readonly Color[] Palette =
        {
            new Color(0.97f, 0.2f, 0.28f), new Color(0.22f, 0.52f, 1f), new Color(0.25f, 0.88f, 0.4f), new Color(1f, 0.8f, 0.16f), new Color(0.74f, 0.36f, 0.96f),
        };
        public static readonly string[] ColourNames = { "red", "blue", "green", "gold", "violet" };

        public int Target = 18;
        public int Cleared { get; private set; }
        public int Popped { get; private set; }
        public int Dropped { get; private set; }
        public int Shots { get; private set; }
        public int Refills { get; private set; }
        public bool Won { get; private set; }
        /// <summary>A bubble is in flight or the last shot is still resolving: another use is ignored.</summary>
        public bool Busy { get; private set; }
        public int CurrentColour { get; private set; }
        public int NextColour { get; private set; }
        /// <summary>The cell the last shot stuck in (-1, -1 when it fizzled).</summary>
        public Vector2Int LastLanded { get; private set; } = new Vector2Int(-1, -1);
        public Interactable Surface { get; private set; }
        /// <summary>Where the player stands to shoot (set by the cell), world space.</summary>
        public Transform FiringSpot;
        public event Action<BubbleBoard> Changed;    // a shot resolved (counts changed)
        public event Action<BubbleBoard> Completed;  // the target is reached

        readonly int[,] m_Grid = new int[MaxRows, Cols];
        readonly Transform[,] m_Slot = new Transform[MaxRows, Cols];
        readonly MeshRenderer[,] m_SlotR = new MeshRenderer[MaxRows, Cols];
        readonly Material[] m_Mats = new Material[Palette.Length];
        readonly List<Vector2> m_Path = new List<Vector2>();
        readonly List<Vector2> m_AimPath = new List<Vector2>();
        Mesh m_Sphere;
        Transform m_Barrel, m_CurrentB, m_NextB, m_FlyB, m_Ring;
        MeshRenderer m_CurrentR, m_NextR, m_FlyR;
        LineRenderer m_Aim;
        ParticleSystem m_Burst;
        TMPro.TextMeshPro m_Count, m_Banner, m_Hint;
        System.Random m_Rng = new System.Random(1889);

        // Pop and drop animations run on their own pooled spheres, so the grid cell is free at once.
        sealed class Fx { public Transform T; public MeshRenderer R; public float Age; public bool Drop; public Vector2 Vel; public bool Live; }
        readonly List<Fx> m_Fx = new List<Fx>();
        int m_FxNext;

        // Flight.
        int m_FlyColour, m_FlySeg;
        float m_FlyDist;
        Vector2Int m_FlyCell;
        bool m_Flying;
        float m_ResolveWait;

        // Cached best shot (tests and the guide).
        bool m_HintDirty = true;
        Vector2 m_HintDir;
        Vector2Int m_HintCell = new Vector2Int(-1, -1);
        int m_HintScore;

        public static int ColsIn(int row) => (row & 1) == 0 ? Cols : Cols - 1;
        public static Vector2 CellPos(int row, int col) => new Vector2(-W / 2f + R + col * D + ((row & 1) == 1 ? R : 0f), TopY - row * Pitch);
        public static bool Valid(int row, int col) => row >= 0 && row < MaxRows && col >= 0 && col < ColsIn(row);
        public static Color ColourOf(int k) => Palette[Mathf.Clamp(k, 0, Palette.Length - 1)];

        public int Remaining
        {
            get { int n = 0; for (int r = 0; r < MaxRows; r++) for (int c = 0; c < Cols; c++) if (m_Grid[r, c] >= 0) n++; return n; }
        }

        public int At(int row, int col) => Valid(row, col) ? m_Grid[row, col] : -1;
        public Vector3 WorldOf(int row, int col) { var p = CellPos(row, col); return transform.TransformPoint(new Vector3(p.x, p.y, BubbleZ)); }
        public Vector3 LauncherWorld => transform.TransformPoint(new Vector3(Launcher.x, Launcher.y, BubbleZ));
        public Vector3 CentreWorld => transform.TransformPoint(new Vector3(0f, 0f, BubbleZ));

        // ------------------------------------------------------------------ build
        public static BubbleBoard Build(Transform parent, Vector3 pos, float yaw)
        {
            var go = new GameObject("BubbleBoard");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var b = go.AddComponent<BubbleBoard>();
            b.Construct();
            return b;
        }

        void Construct()
        {
            var t = transform;
            for (int r = 0; r < MaxRows; r++) for (int c = 0; c < Cols; c++) m_Grid[r, c] = -1;
            for (int k = 0; k < Palette.Length; k++)
                m_Mats[k] = Mats.Lit(Palette[k], 0.88f, 0f, null, false, Palette[k] * 0.42f);
            var back = Mats.Lit(new Color(0.04f, 0.05f, 0.1f), 0.55f, 0f, null, false, new Color(0.02f, 0.03f, 0.07f));
            var field = Mats.Lit(new Color(0.07f, 0.09f, 0.2f), 0.75f, 0f, null, false, new Color(0.05f, 0.07f, 0.17f));
            var frame = Mats.Emissive(new Color(1f, 0.76f, 0.56f), new Color(2.4f, 1.35f, 0.85f));
            var wallGlow = Mats.Emissive(new Color(0.55f, 0.75f, 1f), new Color(0.6f, 1.1f, 2.2f));
            var brass = Mats.Lit(new Color(0.8f, 0.62f, 0.32f), 0.7f, 0.9f, "metal_brushed");
            var marquee = Mats.Emissive(new Color(0.35f, 0.05f, 0.12f), new Color(0.5f, 0.06f, 0.16f));

            // Backboard, playfield, the glowing frame and the bounce walls.
            MeshFactory.Box("Backboard", t, new Vector3(0f, 0f, 0.035f), new Vector3(FrameW, FrameH, 0.04f), back, false);
            MeshFactory.Box("Playfield", t, new Vector3(0f, 0f, 0.008f), new Vector3(W, H, 0.01f), field, false);
            MeshFactory.Box("FrameTop", t, new Vector3(0f, FrameH / 2f, -0.02f), new Vector3(FrameW + 0.1f, 0.1f, 0.09f), frame, false);
            MeshFactory.Box("FrameBottom", t, new Vector3(0f, -FrameH / 2f, -0.02f), new Vector3(FrameW + 0.1f, 0.1f, 0.09f), frame, false);
            MeshFactory.Box("FrameLeft", t, new Vector3(-FrameW / 2f, 0f, -0.02f), new Vector3(0.1f, FrameH, 0.09f), frame, false);
            MeshFactory.Box("FrameRight", t, new Vector3(FrameW / 2f, 0f, -0.02f), new Vector3(0.1f, FrameH, 0.09f), frame, false);
            MeshFactory.Box("WallL", t, new Vector3(-W / 2f - 0.012f, 0f, -0.01f), new Vector3(0.024f, H, 0.03f), wallGlow, false);
            MeshFactory.Box("WallR", t, new Vector3(W / 2f + 0.012f, 0f, -0.01f), new Vector3(0.024f, H, 0.03f), wallGlow, false);
            MeshFactory.Box("Ceiling", t, new Vector3(0f, H / 2f + 0.012f, -0.01f), new Vector3(W, 0.024f, 0.03f), wallGlow, false);
            foreach (var x in new[] { -FrameW / 2f, FrameW / 2f })
                foreach (var y in new[] { -FrameH / 2f, FrameH / 2f })
                    MeshFactory.Sphere("Finial", t, new Vector3(x, y, -0.03f), 0.085f, frame, 12, 8);
            // The danger line: a bubble left this low brings a fresh board down.
            var danger = Mats.Emissive(new Color(1f, 0.3f, 0.3f), new Color(1.2f, 0.25f, 0.25f));
            float dy = CellPos(MaxRows - 1, 0).y + R + 0.01f;
            for (int i = 0; i < 14; i++)
                MeshFactory.Box("DangerDash", t, new Vector3(-W / 2f + 0.1f + i * (W - 0.2f) / 13f, dy, -0.006f), new Vector3(0.09f, 0.012f, 0.01f), danger, false);

            // Marquee and counters.
            MeshFactory.Box("Marquee", t, new Vector3(0f, FrameH / 2f + 0.25f, 0.0f), new Vector3(1.9f, 0.34f, 0.06f), marquee, false);
            MeshFactory.Box("MarqueeTrim", t, new Vector3(0f, FrameH / 2f + 0.25f, 0.012f), new Vector3(2.0f, 0.42f, 0.03f), frame, false);
            var title = TextFactory.World(t, "BULLES DE PARIS", 0.16f, new Color(1f, 0.92f, 0.78f), TMPro.TextAlignmentOptions.Center, true, 1.9f);
            title.transform.localPosition = new Vector3(0f, FrameH / 2f + 0.25f, -0.04f);
            m_Count = TextFactory.World(t, "", 0.075f, new Color(1f, 0.92f, 0.78f), TMPro.TextAlignmentOptions.Left, false, 0.9f);
            m_Count.transform.localPosition = new Vector3(-W / 2f + 0.5f, -H / 2f + 0.12f, -0.02f);
            var nextLabel = TextFactory.World(t, "NEXT", 0.055f, new Color(1f, 0.92f, 0.78f), TMPro.TextAlignmentOptions.Center, false, 0.4f);
            nextLabel.transform.localPosition = new Vector3(Launcher.x + 0.44f, Launcher.y + 0.15f, -0.02f);
            m_Hint = TextFactory.World(t, "", 0.06f, new Color(0.8f, 0.9f, 1f), TMPro.TextAlignmentOptions.Right, false, 0.9f);
            m_Hint.transform.localPosition = new Vector3(W / 2f - 0.5f, -H / 2f + 0.12f, -0.02f);
            m_Banner = TextFactory.World(t, "BRAVO !", 0.42f, new Color(1f, 0.85f, 0.4f), TMPro.TextAlignmentOptions.Center, true, 2.6f);
            m_Banner.transform.localPosition = new Vector3(0f, 0.15f, -0.2f);
            m_Banner.gameObject.SetActive(false);

            // The launcher: a brass cradle, a barrel that swings to the aim, a ring round the bubble it holds.
            MeshFactory.Box("LauncherBase", t, new Vector3(Launcher.x, -H / 2f + 0.03f, -0.05f), new Vector3(0.46f, 0.07f, 0.08f), brass, false);
            m_Barrel = new GameObject("Barrel").transform;
            m_Barrel.SetParent(t, false);
            m_Barrel.localPosition = new Vector3(Launcher.x, Launcher.y, BubbleZ + 0.05f);
            MeshFactory.Box("BarrelL", m_Barrel, new Vector3(-0.07f, 0.2f, 0f), new Vector3(0.025f, 0.26f, 0.03f), brass, false);
            MeshFactory.Box("BarrelR", m_Barrel, new Vector3(0.07f, 0.2f, 0f), new Vector3(0.025f, 0.26f, 0.03f), brass, false);
            MeshFactory.Box("BarrelTip", m_Barrel, new Vector3(0f, 0.34f, 0f), new Vector3(0.17f, 0.025f, 0.03f), frame, false);
            var ring = new MeshBuilder();
            ring.AddTorus(Vector3.zero, R + 0.03f, 0.016f, 28, 6, Quaternion.Euler(90f, 0f, 0f));
            m_Ring = MeshFactory.FromBuilder("LauncherRing", ring, frame, t).transform;
            m_Ring.localPosition = new Vector3(Launcher.x, Launcher.y, BubbleZ);

            // Bubbles: one shared sphere mesh, a sphere per grid cell, the launcher's two, the one in flight and the fx pool.
            var proto = new MeshBuilder();
            proto.AddSphere(Vector3.zero, R * 0.96f, 18, 12);
            m_Sphere = proto.ToMesh("Bubble");
            var bubbles = new GameObject("Bubbles").transform;
            bubbles.SetParent(t, false);
            for (int r = 0; r < MaxRows; r++)
                for (int c = 0; c < ColsIn(r); c++)
                {
                    var p = CellPos(r, c);
                    var go = MakeBubble("B" + r + "_" + c, bubbles, new Vector3(p.x, p.y, BubbleZ));
                    m_Slot[r, c] = go.transform;
                    m_SlotR[r, c] = go.GetComponent<MeshRenderer>();
                    go.SetActive(false);
                }
            var cur = MakeBubble("CurrentBubble", t, new Vector3(Launcher.x, Launcher.y, BubbleZ));
            m_CurrentB = cur.transform; m_CurrentR = cur.GetComponent<MeshRenderer>();
            var nxt = MakeBubble("NextBubble", t, new Vector3(Launcher.x + 0.44f, Launcher.y - 0.03f, BubbleZ));
            nxt.transform.localScale = Vector3.one * 0.7f;
            m_NextB = nxt.transform; m_NextR = nxt.GetComponent<MeshRenderer>();
            var fly = MakeBubble("FlyingBubble", t, new Vector3(Launcher.x, Launcher.y, BubbleZ));
            m_FlyB = fly.transform; m_FlyR = fly.GetComponent<MeshRenderer>();
            fly.SetActive(false);
            var fxRoot = new GameObject("BubbleFx").transform;
            fxRoot.SetParent(t, false);
            for (int i = 0; i < 48; i++)
            {
                var f = MakeBubble("Fx" + i, fxRoot, Vector3.zero);
                f.SetActive(false);
                m_Fx.Add(new Fx { T = f.transform, R = f.GetComponent<MeshRenderer>() });
            }

            // The aim guide: a dotted line along the predicted path (with its bounces) while the board is aimed at.
            var aimGo = new GameObject("AimLine");
            aimGo.transform.SetParent(t, false);
            m_Aim = aimGo.AddComponent<LineRenderer>();
            m_Aim.useWorldSpace = false;
            m_Aim.positionCount = 0;
            m_Aim.widthMultiplier = 0.03f;
            m_Aim.numCapVertices = 2;
            m_Aim.textureMode = LineTextureMode.Stretch;
            m_Aim.sharedMaterial = Mats.Unlit(new Color(1f, 0.95f, 0.85f, 0.8f), null, true, 1.6f);
            m_Aim.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            m_Aim.receiveShadows = false;

            m_Burst = MakeBurst();

            // Light on the board and the player in front of it.
            var lg = new GameObject("BoardGlow");
            lg.transform.SetParent(t, false);
            lg.transform.localPosition = new Vector3(0f, 0.2f, -1.3f);
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(1f, 0.78f, 0.86f); l.intensity = 1.5f; l.range = 4.6f; l.shadows = LightShadows.None;

            // One big collider over the whole board: the player aims anywhere on it and uses it.
            var col = gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0f, -0.02f);
            col.size = new Vector3(FrameW, FrameH, 0.04f);
            Surface = gameObject.AddComponent<Interactable>();
            Surface.Prompt = "Shoot a bubble";
            Surface.MaxDistance = 3.2f;
            var marker = new GameObject("AimMarker").transform;
            marker.SetParent(t, false);
            marker.localPosition = new Vector3(Launcher.x, Launcher.y + 0.45f, BubbleZ);
            Surface.MarkerPoint = marker;
            Surface.OnUse = who => Shoot(who);
            ResetGame();
        }

        GameObject MakeBubble(string name, Transform parent, Vector3 pos)
        {
            var go = MeshFactory.Make(name, m_Sphere, m_Mats[0], parent, pos);
            var r = go.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        ParticleSystem MakeBurst()
        {
            var go = new GameObject("PopBurst");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
            main.gravityModifier = 0.5f;
            main.maxParticles = 600;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 0f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.06f;
            var colt = ps.colorOverLifetime; colt.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            colt.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = Mats.Particle(Tex.SoftDot, true, Color.white, 1.8f, Mats.StencilNotEqual);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        // ------------------------------------------------------------------ game state
        /// <summary>A fresh game: six rows of clustered colours, nothing cleared.</summary>
        public void ResetGame()
        {
            Won = false;
            Cleared = 0; Popped = 0; Dropped = 0; Shots = 0; Refills = 0;
            Busy = false; m_Flying = false; m_ResolveWait = 0f;
            LastLanded = new Vector2Int(-1, -1);
            m_Rng = new System.Random(UnityEngine.Random.Range(1, int.MaxValue));
            foreach (var f in m_Fx) { f.Live = false; if (f.T != null) f.T.gameObject.SetActive(false); }
            if (m_FlyB != null) m_FlyB.gameObject.SetActive(false);
            if (m_Banner != null) m_Banner.gameObject.SetActive(false);
            Fill();
            CurrentColour = PickColour();
            NextColour = PickColour();
            if (Surface != null) Surface.Usable = true;
            Refresh();
        }

        /// <summary>The board after a win (a revisit once France is verified): cleared, the banner up, nothing to shoot.</summary>
        public void ShowSolved()
        {
            Busy = false; m_Flying = false;
            for (int r = 0; r < MaxRows; r++) for (int c = 0; c < Cols; c++) m_Grid[r, c] = -1;
            foreach (var f in m_Fx) { f.Live = false; if (f.T != null) f.T.gameObject.SetActive(false); }
            if (m_FlyB != null) m_FlyB.gameObject.SetActive(false);
            Won = true;
            if (Surface != null) Surface.Usable = false;
            if (m_Banner != null) m_Banner.gameObject.SetActive(true);
            Refresh();
        }

        void Fill()
        {
            for (int r = 0; r < MaxRows; r++)
                for (int c = 0; c < Cols; c++)
                {
                    if (r >= StartRows || c >= ColsIn(r)) { m_Grid[r, c] = -1; continue; }
                    // Small clusters: now and then the same colour as the bubble to the left, so pairs wait to be matched.
                    int k = m_Rng.Next(ColourCount);
                    if (c > 0 && m_Rng.NextDouble() < 0.3) k = m_Grid[r, c - 1];
                    m_Grid[r, c] = k;
                }
            // No ready-made giant blobs: break up any group of one colour bigger than three (one shot must not clear the board).
            for (int pass = 0; pass < 200; pass++)
            {
                List<Vector2Int> big = null;
                var seen = new HashSet<Vector2Int>();
                for (int r = 0; r < StartRows && big == null; r++)
                    for (int c = 0; c < ColsIn(r) && big == null; c++)
                    {
                        var v = new Vector2Int(c, r);
                        if (m_Grid[r, c] < 0 || seen.Contains(v)) continue;
                        var g = Group(m_Grid, v);
                        foreach (var x in g) seen.Add(x);
                        if (g.Count > 3) big = g;
                    }
                if (big == null) break;
                var cell = big[big.Count / 2];
                int keep = m_Grid[cell.y, cell.x], best = keep, bestN = int.MaxValue;
                for (int k = 0; k < ColourCount; k++)
                {
                    if (k == keep) continue;
                    int n = 0;
                    foreach (var nb in Neighbours(cell.y, cell.x)) if (m_Grid[nb.y, nb.x] == k) n++;
                    n = n * 10 + m_Rng.Next(10);
                    if (n < bestN) { bestN = n; best = k; }
                }
                m_Grid[cell.y, cell.x] = best;
            }
            m_HintDirty = true;
        }

        int PickColour()
        {
            var present = new List<int>();
            for (int r = 0; r < MaxRows; r++) for (int c = 0; c < Cols; c++) { int k = m_Grid[r, c]; if (k >= 0 && !present.Contains(k)) present.Add(k); }
            if (present.Count == 0) return m_Rng.Next(ColourCount);
            return present[m_Rng.Next(present.Count)];
        }

        bool Present(int k)
        {
            for (int r = 0; r < MaxRows; r++) for (int c = 0; c < Cols; c++) if (m_Grid[r, c] == k) return true;
            return false;
        }

        void Refresh()
        {
            for (int r = 0; r < MaxRows; r++)
                for (int c = 0; c < ColsIn(r); c++)
                {
                    int k = m_Grid[r, c];
                    var s = m_Slot[r, c];
                    if (s == null) continue;
                    if (k >= 0) { m_SlotR[r, c].sharedMaterial = m_Mats[k]; if (!s.gameObject.activeSelf) s.gameObject.SetActive(true); s.localScale = Vector3.one; }
                    else if (s.gameObject.activeSelf) s.gameObject.SetActive(false);
                }
            if (m_CurrentR != null)
            {
                m_CurrentR.sharedMaterial = m_Mats[CurrentColour];
                m_CurrentB.gameObject.SetActive(!Won);
                m_NextR.sharedMaterial = m_Mats[NextColour];
                m_NextB.gameObject.SetActive(!Won);
            }
            if (m_Count != null) m_Count.text = Won ? "CLEARED " + Mathf.Max(Cleared, Target) + " / " + Target : "CLEARED " + Cleared + " / " + Target;
            if (m_Hint != null) m_Hint.text = Won ? "" : "3 of a colour pop";
        }

        // ------------------------------------------------------------------ shooting
        void Shoot(Interactor who)
        {
            if (who == null || Won || Busy) return;
            // The point the player aimed at, carried along the aim ray onto the bubbles' plane.
            var hit = who.LastRayEnd;
            var plane = new Plane(transform.forward, transform.TransformPoint(new Vector3(0f, 0f, BubbleZ)));
            var aimAt = hit;
            if (who.RayOrigin != null)
            {
                var o = who.RayOrigin.position;
                var dirW = hit - o;
                if (dirW.sqrMagnitude > 1e-6f)
                {
                    var ray = new Ray(o, dirW.normalized);
                    if (plane.Raycast(ray, out float enter)) aimAt = ray.GetPoint(enter);
                }
            }
            var lp = transform.InverseTransformPoint(aimAt);
            Fire(new Vector2(lp.x, lp.y) - Launcher);
        }

        /// <summary>Fires the current bubble in board direction <paramref name="dir"/> (clamped to point upward).</summary>
        public bool Fire(Vector2 dir)
        {
            if (Won || Busy) return false;
            dir = ClampDir(dir);
            Simulate(dir, m_Path, out m_FlyCell);
            m_FlyColour = CurrentColour;
            m_FlyR.sharedMaterial = m_Mats[m_FlyColour];
            m_FlyB.localPosition = new Vector3(Launcher.x, Launcher.y, BubbleZ);
            m_FlyB.gameObject.SetActive(true);
            m_FlySeg = 0; m_FlyDist = 0f;
            m_Flying = true;
            Busy = true;
            Shots++;
            // The next bubble moves into the launcher.
            CurrentColour = NextColour;
            NextColour = PickColour();
            Refresh();
            m_Barrel.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
            AudioDirector.I?.Play("lever_clunk", LauncherWorld, 0.45f, 1.7f);
            AudioDirector.I?.Play("pp_page", LauncherWorld, 0.35f, 1.5f);
            return true;
        }

        public static Vector2 ClampDir(Vector2 dir)
        {
            float a = dir.sqrMagnitude > 1e-6f ? Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg : 90f;
            if (a < -90f) a += 360f;     // below the launcher on the left: lean left
            a = Mathf.Clamp(a, 9f, 171f);
            return new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
        }

        /// <summary>
        /// The flight from the launcher along <paramref name="dir"/>: the path (with its wall bounces) and the free cell the
        /// bubble sticks in (-1, -1 when there is no room: it would hang below the board).
        /// </summary>
        bool Simulate(Vector2 dir, List<Vector2> path, out Vector2Int cell) => Simulate(m_Grid, dir, path, out cell);

        static bool Simulate(int[,] grid, Vector2 dir, List<Vector2> path, out Vector2Int cell)
        {
            path?.Clear();
            var p = Launcher;
            path?.Add(p);
            dir = dir.normalized;
            float xMin = -W / 2f + R, xMax = W / 2f - R;
            const float step = 0.012f;
            for (int i = 0; i < 2000; i++)
            {
                var n = p + dir * step;
                if (n.x < xMin || n.x > xMax)
                {
                    float wall = n.x < xMin ? xMin : xMax;
                    float t = Mathf.Abs(dir.x) > 1e-5f ? (wall - p.x) / dir.x : 0f;
                    var bp = p + dir * t;
                    path?.Add(bp);
                    dir.x = -dir.x;
                    n = bp + dir * Mathf.Max(0f, step - t);
                }
                p = n;
                if (p.y >= TopY) { p.y = TopY; break; }
                if (Touches(grid, p)) break;
            }
            path?.Add(p);
            cell = Snap(grid, p);
            return cell.x >= 0;
        }

        static bool Touches(int[,] grid, Vector2 p)
        {
            int rr = Mathf.FloorToInt((TopY - p.y) / Pitch);
            const float hit = D * 0.84f;
            for (int r = rr - 1; r <= rr + 2; r++)
            {
                if (r < 0 || r >= MaxRows) continue;
                for (int c = 0; c < ColsIn(r); c++)
                {
                    if (grid[r, c] < 0) continue;
                    if ((CellPos(r, c) - p).sqrMagnitude < hit * hit) return true;
                }
            }
            return false;
        }

        static Vector2Int Snap(int[,] grid, Vector2 p)
        {
            var best = new Vector2Int(-1, -1);
            float bd = float.MaxValue;
            for (int r = 0; r < MaxRows; r++)
                for (int c = 0; c < ColsIn(r); c++)
                {
                    if (grid[r, c] >= 0) continue;
                    if (r != 0 && !HasNeighbour(grid, r, c)) continue;
                    float d = (CellPos(r, c) - p).sqrMagnitude;
                    if (d < bd) { bd = d; best = new Vector2Int(c, r); }
                }
            // Too far from any free cell it could hang from: it would sit below the board.
            if (best.x >= 0 && bd > (D * 1.6f) * (D * 1.6f)) best = new Vector2Int(-1, -1);
            return best;
        }

        static bool HasNeighbour(int[,] grid, int r, int c)
        {
            foreach (var n in Neighbours(r, c)) if (grid[n.y, n.x] >= 0) return true;
            return false;
        }

        /// <summary>The (col, row) cells touching cell (<paramref name="r"/>, <paramref name="c"/>) on the hex grid (odd rows sit half a bubble right).</summary>
        public static IEnumerable<Vector2Int> Neighbours(int r, int c)
        {
            bool odd = (r & 1) == 1;
            int ul = odd ? c : c - 1, ur = odd ? c + 1 : c;
            var all = new[] { new Vector2Int(c - 1, r), new Vector2Int(c + 1, r), new Vector2Int(ul, r - 1), new Vector2Int(ur, r - 1), new Vector2Int(ul, r + 1), new Vector2Int(ur, r + 1) };
            foreach (var n in all) if (Valid(n.y, n.x)) yield return n;
        }

        static List<Vector2Int> Group(int[,] grid, Vector2Int start)
        {
            var res = new List<Vector2Int>();
            int k = grid[start.y, start.x];
            if (k < 0) return res;
            var seen = new HashSet<Vector2Int> { start };
            var q = new Queue<Vector2Int>();
            q.Enqueue(start);
            while (q.Count > 0)
            {
                var v = q.Dequeue();
                res.Add(v);
                foreach (var n in Neighbours(v.y, v.x))
                    if (!seen.Contains(n) && grid[n.y, n.x] == k) { seen.Add(n); q.Enqueue(n); }
            }
            return res;
        }

        /// <summary>Occupied cells no longer connected to the top row.</summary>
        static List<Vector2Int> Floating(int[,] grid)
        {
            var seen = new HashSet<Vector2Int>();
            var q = new Queue<Vector2Int>();
            for (int c = 0; c < ColsIn(0); c++) if (grid[0, c] >= 0) { var v = new Vector2Int(c, 0); seen.Add(v); q.Enqueue(v); }
            while (q.Count > 0)
            {
                var v = q.Dequeue();
                foreach (var n in Neighbours(v.y, v.x))
                    if (!seen.Contains(n) && grid[n.y, n.x] >= 0) { seen.Add(n); q.Enqueue(n); }
            }
            var res = new List<Vector2Int>();
            for (int r = 0; r < MaxRows; r++) for (int c = 0; c < ColsIn(r); c++) if (grid[r, c] >= 0 && !seen.Contains(new Vector2Int(c, r))) res.Add(new Vector2Int(c, r));
            return res;
        }

        // ------------------------------------------------------------------ prediction (tests and the guide)
        /// <summary>What a shot along <paramref name="dir"/> would do now: where it sticks, how many pop and drop.</summary>
        public bool Predict(Vector2 dir, out Vector2Int cell, out int pops, out int drops)
        {
            pops = 0; drops = 0;
            if (!Simulate(m_Grid, ClampDir(dir), null, out cell)) return false;
            var g = (int[,])m_Grid.Clone();
            g[cell.y, cell.x] = CurrentColour;
            var grp = Group(g, cell);
            if (grp.Count >= 3)
            {
                pops = grp.Count;
                foreach (var v in grp) g[v.y, v.x] = -1;
                drops = Floating(g).Count;
            }
            return true;
        }

        /// <summary>The best shot for the current bubble: the direction, the cell it lands in and a score (pops first).</summary>
        public bool BestShot(out Vector2 dir, out Vector2Int cell, out int score)
        {
            if (m_HintDirty)
            {
                m_HintDirty = false;
                m_HintScore = int.MinValue;
                m_HintCell = new Vector2Int(-1, -1);
                for (float a = 10f; a <= 170f; a += 1f)
                {
                    var d = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
                    if (!Predict(d, out var c, out int pops, out int drops)) continue;
                    int s;
                    if (pops >= 3) s = 1000 + pops * 10 + drops * 15;
                    else
                    {
                        int same = 0;
                        foreach (var n in Neighbours(c.y, c.x)) if (m_Grid[n.y, n.x] == CurrentColour) same++;
                        s = same * 40 - c.y * 6;
                        if (c.y >= MaxRows - 1) s -= 2000;
                    }
                    // Prefer straight shots among equals (no bounce surprises).
                    s -= Mathf.RoundToInt(Mathf.Abs(90f - a) * 0.05f);
                    if (s > m_HintScore) { m_HintScore = s; m_HintDir = d; m_HintCell = c; }
                }
            }
            dir = m_HintDir; cell = m_HintCell; score = m_HintScore;
            return m_HintCell.x >= 0;
        }

        /// <summary>A world point on the bubbles' plane along <paramref name="dir"/> from the launcher (where to aim).</summary>
        public Vector3 AimPointFor(Vector2 dir, float distance = 0.75f)
        {
            var p = Launcher + ClampDir(dir) * distance;
            p.x = Mathf.Clamp(p.x, -W / 2f + 0.05f, W / 2f - 0.05f);
            return transform.TransformPoint(new Vector3(p.x, p.y, BubbleZ));
        }

        // ------------------------------------------------------------------ frame
        void Update()
        {
            float dt = Time.deltaTime;
            if (m_Ring != null) m_Ring.localScale = Vector3.one * (1f + 0.05f * Mathf.Sin(Time.time * 4f));
            UpdateAim();
            if (m_Flying) Fly(dt);
            else if (Busy)
            {
                m_ResolveWait -= dt;
                if (m_ResolveWait <= 0f) { Busy = false; m_HintDirty = true; }
            }
            AnimateFx(dt);
        }

        void UpdateAim()
        {
            Interactor aiming = null;
            if (!Won && Game.Rig != null)
                foreach (var h in Game.Rig.Hands) if (h != null && h.Focused == Surface) { aiming = h; break; }
            if (aiming == null || Busy)
            {
                if (m_Aim.positionCount != 0) m_Aim.positionCount = 0;
                return;
            }
            var hit = aiming.LastRayEnd;
            var plane = new Plane(transform.forward, transform.TransformPoint(new Vector3(0f, 0f, BubbleZ)));
            if (aiming.RayOrigin != null)
            {
                var o = aiming.RayOrigin.position;
                var ray = new Ray(o, (hit - o).normalized);
                if (plane.Raycast(ray, out float enter)) hit = ray.GetPoint(enter);
            }
            var lp = transform.InverseTransformPoint(hit);
            var dir = ClampDir(new Vector2(lp.x, lp.y) - Launcher);
            m_Barrel.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
            Simulate(m_Grid, dir, m_AimPath, out _);
            // Show the first stretch of the path (and its first bounce): enough to aim, not a full solution.
            float budget = 1.6f;
            var pts = new List<Vector3>();
            for (int i = 0; i < m_AimPath.Count; i++)
            {
                var a = m_AimPath[i];
                pts.Add(new Vector3(a.x, a.y, BubbleZ - 0.02f));
                if (i + 1 < m_AimPath.Count)
                {
                    float seg = (m_AimPath[i + 1] - a).magnitude;
                    if (seg >= budget)
                    {
                        var e = a + (m_AimPath[i + 1] - a).normalized * budget;
                        pts.Add(new Vector3(e.x, e.y, BubbleZ - 0.02f));
                        break;
                    }
                    budget -= seg;
                }
            }
            m_Aim.positionCount = pts.Count;
            m_Aim.SetPositions(pts.ToArray());
        }

        void Fly(float dt)
        {
            m_FlyDist += Speed * dt;
            while (m_FlySeg < m_Path.Count - 1)
            {
                float seg = (m_Path[m_FlySeg + 1] - m_Path[m_FlySeg]).magnitude;
                if (m_FlyDist <= seg) break;
                m_FlyDist -= seg;
                m_FlySeg++;
                if (m_FlySeg < m_Path.Count - 1) AudioDirector.I?.Play("dial_click", transform.TransformPoint(new Vector3(m_Path[m_FlySeg].x, m_Path[m_FlySeg].y, BubbleZ)), 0.5f, 1.6f);
            }
            if (m_FlySeg >= m_Path.Count - 1)
            {
                m_Flying = false;
                m_FlyB.gameObject.SetActive(false);
                Land();
                return;
            }
            var a = m_Path[m_FlySeg];
            var b = m_Path[m_FlySeg + 1];
            var p = Vector2.MoveTowards(a, b, m_FlyDist);
            m_FlyB.localPosition = new Vector3(p.x, p.y, BubbleZ);
        }

        void Land()
        {
            var cell = m_FlyCell;
            LastLanded = cell;
            int k = m_FlyColour;
            m_ResolveWait = 0.3f;
            if (cell.x < 0 || m_Grid[cell.y, cell.x] >= 0)
            {
                // No room: the bubble fizzles and a fresh board drops in.
                SpawnFx(transform.TransformPoint(m_FlyB.localPosition), k, false);
                Overflow();
                Changed?.Invoke(this);
                return;
            }
            m_Grid[cell.y, cell.x] = k;
            var at = WorldOf(cell.y, cell.x);
            AudioDirector.I?.Play("place_socket", at, 0.55f, 1.35f);
            var grp = Group(m_Grid, cell);
            int popped = 0, dropped = 0;
            if (grp.Count >= 3)
            {
                foreach (var v in grp)
                {
                    m_Grid[v.y, v.x] = -1;
                    SpawnFx(WorldOf(v.y, v.x), k, false);
                    popped++;
                }
                AudioDirector.I?.Play("ui_click", at, 0.9f, 1.25f);
                AudioDirector.I?.Play("drop_soft", at, 0.7f, 1.8f);
                foreach (var v in Floating(m_Grid))
                {
                    SpawnFx(WorldOf(v.y, v.x), m_Grid[v.y, v.x], true);
                    m_Grid[v.y, v.x] = -1;
                    dropped++;
                }
                if (dropped > 0) AudioDirector.I?.Play("drop_soft", CentreWorld, 0.8f, 0.9f);
                m_ResolveWait = 0.45f;
            }
            Popped += popped;
            Dropped += dropped;
            Cleared += popped + dropped;
            if (Remaining == 0 || Cleared >= Target) { Win(); Changed?.Invoke(this); return; }
            bool danger = false;
            for (int c = 0; c < ColsIn(MaxRows - 1); c++) if (m_Grid[MaxRows - 1, c] >= 0) danger = true;
            if (danger) Overflow();
            if (!Present(CurrentColour)) CurrentColour = PickColour();
            if (!Present(NextColour)) NextColour = PickColour();
            m_HintDirty = true;
            Refresh();
            Changed?.Invoke(this);
        }

        void Overflow()
        {
            Refills++;
            Hud.I?.ShowToast("The bubbles reached the danger line! A fresh board drops in (your count is kept).", 3f);
            AudioDirector.I?.Play("drop_hard", CentreWorld, 0.6f, 1.2f);
            for (int r = 0; r < MaxRows; r++)
                for (int c = 0; c < ColsIn(r); c++)
                    if (m_Grid[r, c] >= 0) { SpawnFx(WorldOf(r, c), m_Grid[r, c], true); m_Grid[r, c] = -1; }
            Fill();
            CurrentColour = PickColour();
            NextColour = PickColour();
            m_ResolveWait = 0.6f;
            Refresh();
        }

        void Win()
        {
            Won = true;
            if (Surface != null) Surface.Usable = false;
            if (m_Aim != null) m_Aim.positionCount = 0;
            StartCoroutine(Celebrate());
            Refresh();
            Completed?.Invoke(this);
        }

        IEnumerator Celebrate()
        {
            AudioDirector.I?.Play("ui_confirm", CentreWorld, 0.9f);
            // Every bubble left pops in a cascade, top to bottom, and the banner comes up.
            for (int r = 0; r < MaxRows; r++)
            {
                bool any = false;
                for (int c = 0; c < ColsIn(r); c++)
                    if (m_Grid[r, c] >= 0) { SpawnFx(WorldOf(r, c), m_Grid[r, c], false); m_Grid[r, c] = -1; any = true; }
                if (any)
                {
                    Refresh();
                    AudioDirector.I?.Play("ui_click", CentreWorld, 0.6f, 1.1f + r * 0.08f);
                    yield return new WaitForSeconds(0.09f);
                }
            }
            if (m_Banner != null) m_Banner.gameObject.SetActive(true);
            for (int i = 0; i < 4; i++)
            {
                var ep = new ParticleSystem.EmitParams { position = transform.TransformPoint(new Vector3(UnityEngine.Random.Range(-1.2f, 1.2f), UnityEngine.Random.Range(-0.6f, 0.9f), BubbleZ - 0.1f)), startColor = Palette[i % Palette.Length], applyShapeToPosition = true };
                m_Burst.Emit(ep, 60);
                yield return new WaitForSeconds(0.12f);
            }
            Refresh();
        }

        // ------------------------------------------------------------------ fx
        void SpawnFx(Vector3 world, int k, bool drop)
        {
            if (k < 0) return;
            var f = m_Fx[m_FxNext];
            m_FxNext = (m_FxNext + 1) % m_Fx.Count;
            f.T.position = world;
            f.T.localScale = Vector3.one;
            f.R.sharedMaterial = m_Mats[k];
            f.T.gameObject.SetActive(true);
            f.Age = 0f;
            f.Drop = drop;
            f.Live = true;
            f.Vel = new Vector2(UnityEngine.Random.Range(-0.5f, 0.5f), UnityEngine.Random.Range(0.2f, 0.9f));
            if (!drop && m_Burst != null)
            {
                var ep = new ParticleSystem.EmitParams { position = world, startColor = Palette[k], applyShapeToPosition = true };
                m_Burst.Emit(ep, 14);
            }
        }

        void AnimateFx(float dt)
        {
            foreach (var f in m_Fx)
            {
                if (!f.Live) continue;
                f.Age += dt;
                if (f.Drop)
                {
                    // Cut loose: they fall out of the board, tumbling, and fade.
                    f.Vel.y -= 6f * dt;
                    var lp = f.T.localPosition + new Vector3(f.Vel.x * dt, f.Vel.y * dt, 0f);
                    f.T.localPosition = lp;
                    f.T.localScale = Vector3.one * Mathf.Clamp01(1.4f - f.Age);
                    if (f.Age > 1.3f) { f.Live = false; f.T.gameObject.SetActive(false); }
                }
                else
                {
                    // Pop: a quick swell, then gone.
                    float s = f.Age < 0.08f ? 1f + f.Age * 4f : Mathf.Max(0f, 1.32f - (f.Age - 0.08f) * 7f);
                    f.T.localScale = Vector3.one * s;
                    if (f.Age > 0.27f) { f.Live = false; f.T.gameObject.SetActive(false); }
                }
            }
        }
    }
}
