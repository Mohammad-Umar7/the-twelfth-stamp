using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// Egypt: the temple run. A 90 m sandstone causeway of three 2 m lanes, walled both sides, runs north from a pylon gate
    /// to a portal yard, east of the oasis camp and pointing at the pyramids. The player runs it themselves: mummies burst
    /// out of tomb doors in the side walls and shuffle down a lane toward them (change lanes), low fallen stones block
    /// single lanes (go round), gold passport coins line the lanes (run through them), and crossing the finish line
    /// before the portal wins. A mummy's touch staggers the player: a flash, a drain of energy and a shove a few metres
    /// back. Lives at the cell's origin, so its local space is the cell's. Every renderer is built once.
    /// </summary>
    public class TempleRun : MonoBehaviour
    {
        public const float RunX = 39f, LaneW = 2f, HalfW = 3f, Z0 = -6f, Z1 = 84f, WallH = 3.4f, YardEnd = Z1 + 10.5f, FloorY = 0.02f;
        public const float HitDrain = 0.3f, CoinRestore = 0.03f, KnockBack = 3.2f;
        public static float LaneX(int lane) => RunX + (lane - 1) * LaneW;
        public static int LaneOf(float x) => Mathf.Clamp(Mathf.RoundToInt((x - RunX) / LaneW) + 1, 0, 2);
        public static Vector3 StartLocal => new Vector3(RunX, FloorY, -12.5f);
        public static Vector3 PortalLocal => new Vector3(RunX, FloorY, Z1 + 7f);

        public sealed class Obstacle { public int Lane; public float Z; public Transform T; }
        public sealed class Coin { public Transform T; public bool Taken; public int Lane; public float Z; }
        public enum MummyState { Hidden, Bursting, Walking, Crumbling, Gone }
        public sealed class Mummy
        {
            public Transform Root, Body, LegL, LegR, ArmL, ArmR, Slab;
            public float DoorZ, X, Z, T, Phase, SlabT;
            public int Side, Lane;
            public MummyState State;
            public Vector3 Home;
            public Quaternion SlabRest;
            public Vector3 SlabRestPos;
        }

        public readonly List<Obstacle> Obstacles = new List<Obstacle>();
        public readonly List<Coin> CoinList = new List<Coin>();
        public readonly List<Mummy> Mummies = new List<Mummy>();
        public VistaInfo StampSpot;
        public Transform Portal;
        public EgyptCues Cues;
        /// <summary>Set by the encounter while the run is being played.</summary>
        public bool Live;
        public int Coins { get; private set; }
        public int CoinTotal => CoinList.Count;
        public int Hits { get; private set; }
        public int Bursts { get; private set; }
        /// <summary>The player is being thrown back by a mummy (the knock is moving the rig).</summary>
        public bool Staggered => m_Knock;
        public event Action<TempleRun> CoinTaken, Struck;

        Material m_PortalMat;
        float m_Invuln, m_LastCoin;
        int m_Streak;
        bool m_Knock;

        static readonly (int lane, float z)[] ObstacleAt = { (1, 5f), (0, 16f), (2, 28f), (1, 40f), (0, 51f), (2, 62f), (1, 71f), (0, 79.5f) };
        static readonly (float z, int side)[] DoorAt = { (10f, -1), (22f, 1), (34f, -1), (46f, 1), (57f, -1), (67f, 1), (76f, -1) };

        public static TempleRun Build(Transform cell, LightingPreset lighting)
        {
            var go = new GameObject("TempleRun");
            go.transform.SetParent(cell, false);
            var run = go.AddComponent<TempleRun>();
            run.Cues = go.AddComponent<EgyptCues>();
            run.Cues.Init();
            run.Construct();
            return run;
        }

        // ------------------------------------------------------------------ build
        void Construct()
        {
            var t = transform;
            var sandstone = Mats.Lit(new Color(0.86f, 0.74f, 0.56f), 0.18f, 0f, "sandstone_blocks");
            var trim = Mats.Lit(new Color(0.92f, 0.82f, 0.64f), 0.2f, 0f, "sandstone_blocks");
            var pavers = Mats.Lit(new Color(0.84f, 0.73f, 0.57f), 0.24f, 0f, "plaza_stone");
            var inlay = Mats.Lit(new Color(0.42f, 0.33f, 0.24f), 0.35f, 0f, "granite_pavers");
            var sand = Mats.Lit(new Color(0.9f, 0.76f, 0.55f), 0.12f, 0f, "sand_ripples");
            var dark = Mats.Lit(new Color(0.02f, 0.015f, 0.012f), 0.05f);
            var gilt = Mats.Lit(new Color(0.9f, 0.7f, 0.32f), 0.75f, 0.95f);
            var granite = Mats.Lit(new Color(1f, 0.78f, 0.7f), 0.45f, 0f, "granite_pavers");
            var bronze = Mats.Lit(new Color(0.55f, 0.4f, 0.22f), 0.6f, 0.9f, "metal_brushed");
            var flame = Mats.Emissive(new Color(1f, 0.7f, 0.3f), new Color(3.2f, 1.6f, 0.45f));
            var finish = Mats.Emissive(new Color(1f, 0.82f, 0.4f), new Color(2.2f, 1.6f, 0.5f));
            var paint = new[] { Mats.Lit(new Color(0.2f, 0.32f, 0.5f), 0.2f), Mats.Lit(new Color(0.62f, 0.25f, 0.16f), 0.2f), Mats.Lit(new Color(0.85f, 0.75f, 0.55f), 0.2f), Mats.Lit(new Color(0.24f, 0.42f, 0.32f), 0.2f) };
            var cedar = Mats.Lit(new Color(0.95f, 0.85f, 0.75f), 0.3f, 0f, "wood_dark");
            var pennant = Mats.Lit(new Color(0.95f, 0.35f, 0.25f), 0.2f, 0f, "fabric_linen");

            // Floors: the forecourt (open to the desert on its west side), the causeway in sections (each lit by its own
            // nearest torches), and the sandy portal yard.
            MeshFactory.Floor("RunForecourt", t, new Vector3(RunX, FloorY, -11.25f), new Vector2(12f, 10.5f), pavers, "stone", 0.3f, 1.2f);
            const int sections = 5;
            float secLen = (Z1 - Z0) / sections;
            for (int i = 0; i < sections; i++)
                MeshFactory.Floor("Causeway" + i, t, new Vector3(RunX, FloorY, Z0 + (i + 0.5f) * secLen), new Vector2(2f * HalfW + 1.2f, secLen), pavers, "stone", 0.3f, 1.2f);
            MeshFactory.Floor("PortalYard", t, new Vector3(RunX, FloorY, (Z1 + YardEnd) / 2f), new Vector2(2f * HalfW + 1.2f, YardEnd - Z1), sand, "sand", 0.3f, 1.6f);

            // The start gate: a pylon over the causeway's mouth, its towers closing the forecourt's north side.
            MonumentKit.Pylon(t, new Vector3(RunX, FloorY, Z0 - 0.4f), 0f, 3f, 7.6f, 7f, sandstone, trim, paint, cedar, pennant, inlay);
            MonumentKit.Wall(t, "ForecourtSouth", new Vector3(RunX, 1.4f, -16.8f), new Vector3(12.6f, 2.8f, 0.6f), sandstone, trim);
            MonumentKit.Wall(t, "ForecourtEast", new Vector3(45.3f, 1.4f, -11.25f), new Vector3(0.6f, 2.8f, 10.5f), sandstone, trim);
            MonumentKit.Obelisk(t, new Vector3(33.8f, FloorY, -15.2f), 5.2f, granite, trim, gilt);
            MonumentKit.Obelisk(t, new Vector3(33.8f, FloorY, -8.8f), 5.2f, granite, trim, gilt);
            var sign = TextFactory.World(t, "THE CAUSEWAY OF THE SUN", 0.26f, new Color(0.35f, 0.22f, 0.1f), TMPro.TextAlignmentOptions.Center, true, 6f);
            sign.transform.localPosition = new Vector3(RunX, 5.16f, Z0 - 1.2f);

            // The causeway walls (with the yard's), pilasters, painted bands, torches.
            for (int s = -1; s <= 1; s += 2)
            {
                float x = RunX + s * (HalfW + 0.3f);
                for (int i = 0; i < sections; i++)
                    MonumentKit.Wall(t, "RunWall", new Vector3(x, WallH / 2f, Z0 + 0.7f + (i + 0.5f) * (Z1 - Z0 - 0.7f) / sections), new Vector3(0.6f, WallH, (Z1 - Z0 - 0.7f) / sections + 0.02f), sandstone, trim);
                MonumentKit.Wall(t, "YardWall", new Vector3(x, WallH / 2f, (Z1 + YardEnd) / 2f), new Vector3(0.6f, WallH, YardEnd - Z1 + 0.02f), sandstone, trim);
            }
            MonumentKit.Wall(t, "YardNorth", new Vector3(RunX, WallH / 2f, YardEnd + 0.3f), new Vector3(2f * HalfW + 1.2f, WallH, 0.6f), sandstone, trim);
            var lights = new List<(Vector3 pos, Color c)>();
            for (int i = 0; i < sections; i++)
            {
                var k = new KitBatch();
                float z0 = Z0 + i * secLen, z1 = z0 + secLen;
                // Lane lines of dark inlay, so the three lanes read underfoot.
                foreach (float lx in new[] { RunX - LaneW / 2f, RunX + LaneW / 2f })
                    k.Box(inlay, new Vector3(lx, FloorY + 0.004f, (z0 + z1) / 2f), new Vector3(0.09f, 0.012f, secLen - 0.2f));
                for (float z = z0 + 1.5f; z < z1; z += 4.5f)
                    k.Box(inlay, new Vector3(RunX, FloorY + 0.004f, z), new Vector3(2f * HalfW - 0.2f, 0.012f, 0.06f));
                for (int s = -1; s <= 1; s += 2)
                {
                    float face = RunX + s * HalfW;
                    for (float z = Mathf.Ceil((z0 + 0.1f) / 9f) * 9f; z < z1; z += 9f)
                    {
                        if (NearDoor(z, s, 2.2f)) continue;
                        // A pilaster with a painted band under its capital.
                        k.Box(sandstone, new Vector3(face - s * 0.08f, (WallH + 0.5f) / 2f, z), new Vector3(0.16f, WallH + 0.5f, 0.6f), 2f);
                        k.Box(trim, new Vector3(face - s * 0.1f, WallH + 0.35f, z), new Vector3(0.24f, 0.18f, 0.72f), 2f);
                        for (int b = 0; b < 4; b++)
                            k.Box(paint[b], new Vector3(face - s * 0.17f, WallH - 0.5f + b * 0.09f, z), new Vector3(0.012f, 0.07f, 0.56f));
                        // A torch on every other pilaster, alternating sides.
                        if (Mathf.RoundToInt(z / 9f) % 2 == (s > 0 ? 0 : 1))
                        {
                            var bowl = new Vector3(face - s * 0.42f, 2.25f, z);
                            k.Box(bronze, new Vector3(face - s * 0.27f, 2.05f, z), new Vector3(0.3f, 0.06f, 0.08f));
                            k.Of(bronze).AddFrustum(bowl - Vector3.up * 0.12f, 0.06f, 0.16f, 0.14f, 12);
                            k.Of(flame).AddFrustum(bowl + Vector3.up * 0.02f, 0.12f, 0.02f, 0.3f, 10);
                            lights.Add((bowl + new Vector3(-s * 0.3f, 0.45f, 0f), new Color(1f, 0.66f, 0.32f)));
                        }
                    }
                    // A painted frieze along the top of the wall.
                    for (int b = 0; b < 3; b++)
                        k.Box(paint[(b + i) % paint.Length], new Vector3(face - s * 0.005f, WallH - 0.2f - b * 0.12f, (z0 + z1) / 2f), new Vector3(0.01f, 0.06f, secLen - 0.3f));
                }
                k.Bake(t, "RunSection" + i);
            }
            foreach (var (pos, c) in lights)
            {
                var lg = new GameObject("TorchLight");
                lg.transform.SetParent(t, false);
                lg.transform.localPosition = pos;
                var l = lg.AddComponent<Light>();
                l.type = LightType.Point; l.color = c; l.intensity = 1.4f; l.range = 6.5f; l.shadows = LightShadows.None;
                Flicker.Add(l, 0.18f, 8f);
            }

            // The finish line, two small obelisks at it, and the portal with its stamp spot.
            MeshFactory.Box("FinishLine", t, new Vector3(RunX, FloorY + 0.006f, Z1), new Vector3(2f * HalfW, 0.014f, 0.3f), finish, false);
            var fin = TextFactory.World(t, "FINISH", 0.5f, new Color(1f, 0.85f, 0.45f), TMPro.TextAlignmentOptions.Center, true, 3f);
            fin.transform.localPosition = new Vector3(RunX, FloorY + 0.02f, Z1 - 1.2f);
            fin.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            MonumentKit.Obelisk(t, new Vector3(RunX - HalfW + 0.45f, FloorY, Z1 + 0.6f), 3.4f, granite, trim, gilt);
            MonumentKit.Obelisk(t, new Vector3(RunX + HalfW - 0.45f, FloorY, Z1 + 0.6f), 3.4f, granite, trim, gilt);
            BuildPortal(PortalLocal, trim);

            // Tomb doors (each hides a mummy), the fallen stones and the passport coins.
            var linen = Mats.Lit(new Color(0.84f, 0.78f, 0.64f), 0.12f, 0f, "fabric_linen");
            var strip = Mats.Lit(new Color(0.62f, 0.54f, 0.42f), 0.12f, 0f, "fabric_linen");
            var eye = Mats.Emissive(new Color(0.7f, 1f, 0.5f), new Color(1.6f, 2.6f, 0.8f));
            foreach (var (z, side) in DoorAt) BuildDoor(z, side, sandstone, trim, dark, paint, linen, strip, eye);
            var drum = Mats.Lit(new Color(0.88f, 0.76f, 0.58f), 0.2f, 0f, "sandstone_blocks");
            for (int i = 0; i < ObstacleAt.Length; i++) BuildObstacle(ObstacleAt[i].lane, ObstacleAt[i].z, i, drum, trim);
            BuildCoins(gilt);
        }

        static bool NearDoor(float z, int side, float within)
        {
            foreach (var d in DoorAt) if (d.side == side && Mathf.Abs(d.z - z) < within) return true;
            return false;
        }

        void BuildDoor(float z, int side, Material stone, Material trim, Material dark, Material[] paint, Material linen, Material strip, Material eye)
        {
            var t = transform;
            float face = RunX + side * HalfW;
            var k = new KitBatch();
            // A dark doorway with a proud frame, a lintel with a painted band and a cavetto cap.
            k.Box(dark, new Vector3(face - side * 0.006f, 1.1f + FloorY, z), new Vector3(0.012f, 2.2f, 1.2f));
            foreach (float dz in new[] { -0.74f, 0.74f })
                k.Box(stone, new Vector3(face - side * 0.08f, 1.2f + FloorY, z + dz), new Vector3(0.16f, 2.4f, 0.28f), 2f);
            k.Box(stone, new Vector3(face - side * 0.09f, 2.52f + FloorY, z), new Vector3(0.18f, 0.32f, 1.9f), 2f);
            k.Box(trim, new Vector3(face - side * 0.14f, 2.74f + FloorY, z), new Vector3(0.28f, 0.12f, 2.1f), 2f);
            for (int b = 0; b < 6; b++)
                k.Box(paint[b % paint.Length], new Vector3(face - side * 0.185f, 2.52f + FloorY, z - 0.75f + b * 0.3f), new Vector3(0.01f, 0.16f, 0.2f));
            k.Bake(t, "TombDoor");

            // The sealing slab: hinged at its foot on the lane side, it falls into the lane when the mummy bursts out.
            var hinge = new GameObject("TombSlab").transform;
            hinge.SetParent(t, false);
            hinge.localPosition = new Vector3(face - side * 0.16f, FloorY, z);
            MeshFactory.Box("Slab", hinge, new Vector3(side * 0.06f, 1.08f, 0f), new Vector3(0.12f, 2.16f, 1.16f), stone, false, 2f);
            MeshFactory.Box("SlabSeal", hinge, new Vector3(-side * 0.005f, 1.3f, 0f), new Vector3(0.012f, 0.3f, 0.3f), trim, false);

            var m = BuildMummy(new Vector3(face - side * 0.35f, FloorY, z), linen, strip, eye);
            m.DoorZ = z;
            m.Side = side;
            m.Slab = hinge;
            m.SlabRest = hinge.localRotation;
            m.SlabRestPos = hinge.localPosition;
            m.Home = m.Root.localPosition;
            m.Root.gameObject.SetActive(false);
            Mummies.Add(m);
        }

        Mummy BuildMummy(Vector3 at, Material linen, Material strip, Material eye)
        {
            var m = new Mummy();
            m.Root = new GameObject("Mummy").transform;
            m.Root.SetParent(transform, false);
            m.Root.localPosition = at;
            m.Body = new GameObject("Body").transform;
            m.Body.SetParent(m.Root, false);
            var b = m.Body;
            // Legs from hip pivots, wrapped feet.
            Transform Leg(float x)
            {
                var p = new GameObject("Leg").transform;
                p.SetParent(m.Root, false);
                p.localPosition = new Vector3(x, 0.86f, 0f);
                MeshFactory.Frustum("Shin", p, new Vector3(0f, -0.86f, 0f), 0.085f, 0.118f, 0.86f, linen, 10);
                MeshFactory.Box("Foot", p, new Vector3(0f, -0.83f, 0.05f), new Vector3(0.13f, 0.07f, 0.25f), strip, false);
                for (int i = 0; i < 3; i++)
                    MeshFactory.Box("LegWrap", p, new Vector3(0f, -0.66f + i * 0.24f, 0f), new Vector3(0.24f, 0.03f, 0.24f), strip, false, 1f, Quaternion.Euler(i * 9f - 9f, i * 30f, 7f));
                return p;
            }
            m.LegL = Leg(-0.11f);
            m.LegR = Leg(0.11f);
            // Torso, chest, wraps, head with glowing eyes.
            MeshFactory.Frustum("Torso", b, new Vector3(0f, 0.8f, 0f), 0.2f, 0.24f, 0.66f, linen, 12);
            var chest = MeshFactory.Sphere("Chest", b, new Vector3(0f, 1.36f, 0.02f), 0.24f, linen, 14, 10);
            chest.transform.localScale = new Vector3(1.1f, 0.6f, 0.82f);
            for (int i = 0; i < 5; i++)
                MeshFactory.Box("Wrap", b, new Vector3(0f, 0.88f + i * 0.12f, 0f), new Vector3(0.47f, 0.035f, 0.42f), strip, false, 1f, Quaternion.Euler(i % 2 == 0 ? 8f : -6f, i * 19f, i % 2 == 0 ? 6f : -8f));
            MeshFactory.Cylinder("Neck", b, new Vector3(0f, 1.44f, 0f), 0.07f, 0.12f, linen, 8, false);
            var head = MeshFactory.Sphere("Head", b, new Vector3(0f, 1.63f, 0f), 0.135f, linen, 14, 10);
            head.transform.localScale = new Vector3(0.92f, 1.12f, 1f);
            for (int i = 0; i < 3; i++)
                MeshFactory.Box("HeadWrap", b, new Vector3(0f, 1.56f + i * 0.07f, 0f), new Vector3(0.27f, 0.026f, 0.29f), strip, false, 1f, Quaternion.Euler(i * 7f - 7f, i * 35f, i * 9f - 9f));
            MeshFactory.Sphere("EyeL", b, new Vector3(-0.045f, 1.655f, 0.118f), 0.021f, eye, 8, 6);
            MeshFactory.Sphere("EyeR", b, new Vector3(0.045f, 1.655f, 0.118f), 0.021f, eye, 8, 6);
            // Arms stretched out in front (pivots at the shoulders, the arm along the pivot's +Y turned forward).
            Transform Arm(float x)
            {
                var p = new GameObject("Arm").transform;
                p.SetParent(b, false);
                p.localPosition = new Vector3(x, 1.38f, 0.02f);
                p.localRotation = Quaternion.Euler(95f, 0f, 0f);
                MeshFactory.Cylinder("Upper", p, Vector3.zero, 0.062f, 0.6f, linen, 8, false);
                MeshFactory.Sphere("Hand", p, new Vector3(0f, 0.64f, 0f), 0.062f, strip, 8, 6);
                MeshFactory.Box("Trail", p, new Vector3(0f, 0.32f, 0.09f), new Vector3(0.05f, 0.34f, 0.006f), strip, false, 1f, Quaternion.Euler(-12f, 0f, 0f));
                return p;
            }
            m.ArmL = Arm(-0.26f);
            m.ArmR = Arm(0.26f);
            foreach (var r in m.Root.GetComponentsInChildren<MeshRenderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return m;
        }

        void BuildObstacle(int lane, float z, int i, Material stone, Material trim)
        {
            var t = transform;
            var o = new GameObject("FallenStone").transform;
            o.SetParent(t, false);
            o.localPosition = new Vector3(LaneX(lane), FloorY, z);
            if (i % 2 == 0)
            {
                // A fallen column drum on its side, with a broken capital beside it.
                var b = new MeshBuilder();
                b.AddCylinder(new Vector3(-0.85f, 0.45f, 0f), 0.45f, 1.7f, 18, 2f, Quaternion.Euler(0f, 0f, -90f));
                MeshFactory.FromBuilder("Drum", b, stone, o);
                var cap = new MeshBuilder();
                cap.AddFrustum(new Vector3(0.62f, 0f, 0.28f), 0.3f, 0.42f, 0.3f, 14);
                MeshFactory.FromBuilder("BrokenCapital", cap, trim, o);
            }
            else
            {
                // A toppled block with a chipped corner and rubble.
                MeshFactory.Box("Block", o, new Vector3(0f, 0.42f, 0f), new Vector3(1.8f, 0.84f, 0.85f), stone, false, 2f, Quaternion.Euler(0f, 4f, 0f));
                MeshFactory.Box("Chip", o, new Vector3(0.75f, 0.9f, 0.1f), new Vector3(0.4f, 0.2f, 0.5f), trim, false, 2f, Quaternion.Euler(10f, 20f, 15f));
                MeshFactory.Box("Rubble", o, new Vector3(-0.6f, 0.08f, -0.55f), new Vector3(0.3f, 0.16f, 0.25f), stone, false, 2f, Quaternion.Euler(0f, 35f, 8f));
            }
            var col = o.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.45f, 0f);
            col.size = new Vector3(1.86f, 0.9f, 0.9f);
            Obstacles.Add(new Obstacle { Lane = lane, Z = z, T = o });
        }

        public bool ObstacleNear(int lane, float zFrom, float zTo)
        {
            foreach (var o in Obstacles) if (o.Lane == lane && o.Z >= zFrom && o.Z <= zTo) return true;
            return false;
        }

        void BuildCoins(Material gold)
        {
            // One disc mesh (it faces along the lane, so it shows its face to a runner) with a passport embossed on it.
            var disc = new MeshBuilder();
            disc.AddCylinder(new Vector3(0f, 0f, -0.018f), 0.17f, 0.036f, 22, 1f, Quaternion.Euler(90f, 0f, 0f));
            var discMesh = disc.ToMesh("PassportCoin");
            var emb = new MeshBuilder();
            foreach (float z in new[] { -0.021f, 0.021f })
            {
                emb.AddBox(new Vector3(0f, 0f, z), new Vector3(0.12f, 0.16f, 0.008f));
                emb.AddBox(new Vector3(0f, 0.02f, z * 1.25f), new Vector3(0.05f, 0.05f, 0.008f));
            }
            var embMesh = emb.ToMesh("PassportEmblem");
            var coinMat = Mats.Lit(new Color(1f, 0.8f, 0.3f), 0.85f, 1f, null, false, new Color(0.7f, 0.45f, 0.08f));
            var navy = Mats.Lit(new Color(0.08f, 0.12f, 0.3f), 0.6f, 0.2f);
            // Runs of three along the lanes, switching lane between runs, never through a fallen stone.
            int[] plan = { 1, 0, 2, 1, 2, 0, 1, 2, 0, 1, 2, 1 };
            int run = 0;
            for (float zs = -1.5f; zs < Z1 - 3f; zs += 7.2f, run++)
            {
                int lane = plan[run % plan.Length];
                for (int tries = 0; tries < 3 && ObstacleNear(lane, zs - 1.5f, zs + 4.7f); tries++) lane = (lane + 1) % 3;
                for (int i = 0; i < 3; i++)
                {
                    float z = zs + i * 1.6f;
                    if (ObstacleNear(lane, z - 1.4f, z + 1.4f)) continue;
                    var c = new GameObject("PassportCoin").transform;
                    c.SetParent(transform, false);
                    c.localPosition = new Vector3(LaneX(lane), FloorY + 1.05f, z);
                    var face = MeshFactory.Make("Disc", discMesh, coinMat, c);
                    face.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    var e = MeshFactory.Make("Emblem", embMesh, navy, c);
                    e.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    CoinList.Add(new Coin { T = c, Lane = lane, Z = z });
                }
            }
        }

        void BuildPortal(Vector3 at, Material stone)
        {
            // The same portal as the Riyadh maze's, with its own stamp spot in the ring.
            Portal = new GameObject("Portal").transform;
            Portal.SetParent(transform, false);
            Portal.localPosition = at;
            MeshFactory.Box("PillarL", Portal, new Vector3(-1.75f, 1.7f, 0f), new Vector3(0.45f, 3.4f, 0.45f), stone, true);
            MeshFactory.Box("PillarR", Portal, new Vector3(1.75f, 1.7f, 0f), new Vector3(0.45f, 3.4f, 0.45f), stone, true);
            MeshFactory.Box("Lintel", Portal, new Vector3(0f, 3.55f, 0f), new Vector3(4.0f, 0.4f, 0.5f), stone, true);
            var ring = new MeshBuilder();
            ring.AddTorus(Vector3.zero, 1.35f, 0.09f, 40, 8, Quaternion.Euler(90f, 0f, 0f));
            var ringGo = MeshFactory.FromBuilder("Ring", ring, Mats.Emissive(new Color(0.6f, 0.9f, 1f), new Color(1.2f, 2.2f, 3f)), Portal);
            ringGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            m_PortalMat = Mats.UnlitInstance(new Color(0.5f, 0.85f, 1f, 0.55f), Tex.Ring, true, 1.6f);
            for (int i = 0; i < 2; i++)
            {
                var disc = MeshFactory.Quad("Swirl" + i, Portal, 2.6f, 2.6f, m_PortalMat, new Vector3(0f, 1.6f, 0.02f * i), Quaternion.identity);
                disc.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var lg = new GameObject("PortalLight");
            lg.transform.SetParent(Portal, false);
            lg.transform.localPosition = new Vector3(0f, 1.6f, -1.2f);
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(0.55f, 0.85f, 1f); l.intensity = 2.2f; l.range = 8f; l.shadows = LightShadows.None;
            var spot = new GameObject("PortalStampSpot");
            spot.transform.SetParent(transform, false);
            StampSpot = spot.AddComponent<VistaInfo>();
            StampSpot.HasStampSpot = true;
            StampSpot.Reachable = true;
            StampSpot.StampLocal = at + new Vector3(0f, 1.6f, 0f);
            StampSpot.GroundY = at.y;
        }

        // ------------------------------------------------------------------ state
        /// <summary>A fresh run: every coin back, every mummy back in its tomb, the slabs shut.</summary>
        public void ResetRun()
        {
            StopAllCoroutines();
            m_Knock = false;
            Coins = 0; Hits = 0; Bursts = 0; m_Streak = 0; m_Invuln = 0f;
            foreach (var c in CoinList) { c.Taken = false; c.T.gameObject.SetActive(true); }
            foreach (var m in Mummies) HideMummy(m);
        }

        /// <summary>After the run is won (a revisit): the tombs open and empty, no coins left.</summary>
        public void ShowFinished()
        {
            StopAllCoroutines();
            m_Knock = false;
            Live = false;
            foreach (var c in CoinList) { c.Taken = true; c.T.gameObject.SetActive(false); }
            foreach (var m in Mummies)
            {
                HideMummy(m);
                m.State = MummyState.Gone;
                m.Slab.gameObject.SetActive(false);
            }
        }

        void HideMummy(Mummy m)
        {
            m.State = MummyState.Hidden;
            m.T = 0f;
            m.SlabT = 0f;
            m.Root.localPosition = m.Home;
            m.Root.localRotation = Quaternion.Euler(0f, m.Side < 0 ? 90f : -90f, 0f);
            m.Root.localScale = Vector3.one;
            m.Root.gameObject.SetActive(false);
            m.Slab.gameObject.SetActive(true);
            m.Slab.localRotation = m.SlabRest;
            m.Slab.localPosition = m.SlabRestPos;
        }

        /// <summary>Art review: mummy <paramref name="i"/> stands in the lane just out of its open tomb, its slab fallen.</summary>
        public void PoseForReview(int i)
        {
            var m = Mummies[Mathf.Clamp(i, 0, Mummies.Count - 1)];
            m.State = MummyState.Gone; // posed, not playing
            m.X = LaneX(m.Side < 0 ? 0 : 2);
            m.Z = m.DoorZ - 1.2f;
            m.Root.gameObject.SetActive(true);
            m.Root.localPosition = new Vector3(m.X, FloorY, m.Z);
            m.Root.localRotation = Quaternion.Euler(0f, 180f, 0f);
            m.Root.localScale = Vector3.one;
            Pose(m, 0.9f, 1f);
            m.Slab.gameObject.SetActive(true);
            m.Slab.localPosition = m.SlabRestPos;
            m.Slab.localRotation = m.SlabRest * Quaternion.Euler(0f, 0f, 90f * m.Side);
        }

        public float Progress(Vector3 local) => Mathf.InverseLerp(Z0, Z1, local.z);
        public static bool InRun(Vector3 local) => Mathf.Abs(local.x - RunX) < HalfW + 0.6f && local.z > Z0 - 1.5f && local.z < YardEnd;
        /// <summary>The player has crossed the finish line (cell-local feet).</summary>
        public static bool Finished(Vector3 local) => local.z > Z1 + 0.5f && Mathf.Abs(local.x - RunX) < HalfW + 0.6f && local.z < YardEnd + 0.5f;

        // ------------------------------------------------------------------ frame
        void Update()
        {
            float dt = Time.deltaTime;
            float spin = Time.time * 160f;
            foreach (var c in CoinList)
                if (!c.Taken) c.T.localRotation = Quaternion.Euler(0f, spin + c.Z * 25f, 0f);
            if (Portal != null)
            {
                int i = 0;
                foreach (Transform ch in Portal)
                    if (ch.name.StartsWith("Swirl")) ch.localRotation = Quaternion.Euler(0f, 0f, Time.time * (i++ == 0 ? 40f : -25f));
                if (m_PortalMat != null) m_PortalMat.SetColor("_Color", new Color(0.5f, 0.85f, 1f, 0.45f + 0.15f * Mathf.Sin(Time.time * 2.2f)));
            }
            foreach (var m in Mummies) if (m.State == MummyState.Bursting || m.State == MummyState.Crumbling) Animate(m, dt, Vector3.zero);
            if (!Live || Game.Rig == null || Game.Paused) return;
            var story = Game.Story;
            if (story == null || story.Busy || story.Failing) return;
            var feet = transform.InverseTransformPoint(Game.Rig.Feet);
            bool inRun = InRun(feet);
            if (m_Invuln > 0f) m_Invuln -= dt;

            foreach (var m in Mummies)
            {
                if (m.State == MummyState.Hidden && inRun && feet.z > m.DoorZ - 13f && feet.z < m.DoorZ - 2f) Burst(m, LaneOf(feet.x));
                if (m.State == MummyState.Walking) Walk(m, dt, feet);
            }

            if (inRun && m_Invuln <= 0f && !m_Knock)
                foreach (var m in Mummies)
                    if (m.State == MummyState.Walking && Mathf.Abs(m.X - feet.x) < 0.72f && Mathf.Abs(m.Z - feet.z) < 0.62f) { Strike(m, feet); break; }

            foreach (var c in CoinList)
            {
                if (c.Taken) continue;
                var d = c.T.localPosition - feet; d.y = 0f;
                if (d.sqrMagnitude < 0.8f * 0.8f) Take(c);
            }
        }

        void Burst(Mummy m, int targetLane)
        {
            m.State = MummyState.Bursting;
            m.T = 0f;
            m.Lane = targetLane;
            m.X = m.Home.x; m.Z = m.DoorZ;
            Bursts++;
            var door = transform.TransformPoint(new Vector3(RunX + m.Side * HalfW, 1.1f, m.DoorZ));
            AudioDirector.I?.Play(UnityEngine.Random.value < 0.5f ? "scare_egy_1" : "scare_egy_2", door, 0.85f);
            Cues?.Dust(door + Vector3.down * 0.9f, 26);
            Hud.I?.ShowCaption("a tomb door bursts open", 1.8f);
        }

        void Walk(Mummy m, float dt, Vector3 feet)
        {
            // Down its lane toward the player, sidestepping fallen stones (and slowly drifting toward the player's lane).
            if (ObstacleNear(m.Lane, m.Z - 1.7f, m.Z + 0.3f))
            {
                int pl = LaneOf(feet.x);
                int best = -1;
                foreach (int cand in new[] { m.Lane - 1, m.Lane + 1 })
                {
                    if (cand < 0 || cand > 2 || ObstacleNear(cand, m.Z - 1.7f, m.Z + 1f)) continue;
                    if (best < 0 || Mathf.Abs(cand - pl) < Mathf.Abs(best - pl)) best = cand;
                }
                if (best >= 0) m.Lane = best;
            }
            float tx = LaneX(m.Lane);
            m.X = Mathf.MoveTowards(m.X, tx, 1.5f * dt);
            // Still in front of the stone it is stepping round: it waits for the sidestep before going on.
            int cur = LaneOf(m.X);
            bool blocked = cur != m.Lane && ObstacleNear(cur, m.Z - 1.1f, m.Z + 0.2f);
            if (!blocked) m.Z -= 1.3f * dt;
            Animate(m, dt, feet);
            // Gone past the player, or back at the gate: it crumbles.
            if (m.Z < feet.z - 6f || m.Z < Z0 - 0.5f) Crumble(m);
        }

        void Animate(Mummy m, float dt, Vector3 feet)
        {
            m.T += dt;
            UpdateSlab(m, dt);
            switch (m.State)
            {
                case MummyState.Bursting:
                {
                    // The mummy lurches out of the dark doorway behind the falling slab, into the lane, and turns.
                    if (m.T > 0.22f && !m.Root.gameObject.activeSelf) m.Root.gameObject.SetActive(true);
                    float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((m.T - 0.22f) / 0.65f));
                    m.X = Mathf.Lerp(m.Home.x, LaneX(m.Side < 0 ? 0 : 2), k);
                    float yaw = Mathf.Lerp(m.Side < 0 ? 90f : -90f, 180f, Mathf.Clamp01((m.T - 0.65f) / 0.4f));
                    m.Root.localPosition = new Vector3(m.X, FloorY, m.Z);
                    m.Root.localRotation = Quaternion.Euler(0f, yaw, 0f);
                    Pose(m, m.T * 7f, 1f);
                    if (m.T >= 1.1f)
                    {
                        m.State = MummyState.Walking;
                        AudioDirector.I?.Play("scare_any_growl", m.Root.position + Vector3.up * 1.5f, 0.55f, 0.9f);
                    }
                    break;
                }
                case MummyState.Walking:
                {
                    m.Phase += dt * 5.2f;
                    m.Root.localPosition = new Vector3(m.X, FloorY, m.Z);
                    m.Root.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    Pose(m, m.Phase, 1f);
                    break;
                }
                case MummyState.Crumbling:
                {
                    float s = Mathf.Clamp01(m.T / 0.7f);
                    m.Root.localScale = new Vector3(1f + s * 0.3f, 1f - s, 1f + s * 0.3f);
                    if (s >= 1f) { m.State = MummyState.Gone; m.Root.gameObject.SetActive(false); m.Slab.gameObject.SetActive(false); }
                    break;
                }
            }
        }

        /// <summary>The slab falls into the lane (gravity-ish), lies there a moment, then sinks away under the paving.</summary>
        void UpdateSlab(Mummy m, float dt)
        {
            if (!m.Slab.gameObject.activeSelf) return;
            float before = m.SlabT;
            m.SlabT += dt;
            float s = Mathf.Clamp01(m.SlabT / 0.42f);
            m.Slab.localRotation = m.SlabRest * Quaternion.Euler(0f, 0f, 90f * m.Side * s * s);
            if (m.SlabT >= 0.42f && before < 0.42f)
            {
                Cues?.Dust(transform.TransformPoint(new Vector3(RunX + m.Side * (HalfW - 1.1f), 0.1f, m.DoorZ)), 34);
                AudioDirector.I?.Play("drop_hard", transform.TransformPoint(new Vector3(RunX + m.Side * (HalfW - 1f), 0.2f, m.DoorZ)), 0.7f, 0.7f);
            }
            float sink = Mathf.Clamp01((m.SlabT - 1.8f) / 0.8f);
            m.Slab.localPosition = m.SlabRestPos + Vector3.down * sink * 0.16f;
            if (sink >= 1f) m.Slab.gameObject.SetActive(false);
        }

        static void Pose(Mummy m, float phase, float amount)
        {
            // A stiff shuffle: legs swing short, the body rocks side to side, the outstretched arms bob.
            float s = Mathf.Sin(phase);
            m.LegL.localRotation = Quaternion.Euler(s * 18f * amount, 0f, 0f);
            m.LegR.localRotation = Quaternion.Euler(-s * 18f * amount, 0f, 0f);
            m.Body.localPosition = new Vector3(0f, Mathf.Abs(s) * 0.03f, 0f);
            m.Body.localRotation = Quaternion.Euler(5f, 0f, s * 5f);
            m.ArmL.localRotation = Quaternion.Euler(95f + Mathf.Sin(phase * 0.5f) * 7f, 0f, 0f);
            m.ArmR.localRotation = Quaternion.Euler(95f - Mathf.Sin(phase * 0.5f + 1f) * 7f, 0f, 0f);
        }

        void Crumble(Mummy m)
        {
            if (m.State == MummyState.Crumbling || m.State == MummyState.Gone) return;
            m.State = MummyState.Crumbling;
            m.T = 0f;
            Cues?.Dust(m.Root.position + Vector3.up * 0.6f, 30);
            AudioDirector.I?.Play("drop_soft", m.Root.position + Vector3.up, 0.6f, 0.6f);
        }

        void Strike(Mummy m, Vector3 feet)
        {
            Hits++;
            m_Invuln = 1.6f;
            Crumble(m);
            var rig = Game.Rig;
            Game.Story?.DrainVitality(HitDrain);
            PostFx.I?.Flash(0.85f);
            Cues?.HitFlash();
            AudioDirector.I?.Play("pass_sting", rig.Head.position + rig.Head.forward * 0.5f, 0.75f, 1.25f);
            AudioDirector.I?.Play("breath_gasp_" + UnityEngine.Random.Range(1, 4), rig.Head.position, 0.9f);
            AudioDirector.Haptic(HandSide.Left, 0.8f, 0.25f);
            AudioDirector.Haptic(HandSide.Right, 0.8f, 0.25f);
            Hud.I?.ShowCaption("the mummy's grip throws you back", 2.2f);
            Struck?.Invoke(this);
            if (Game.Story == null || !Game.Story.Failing) StartCoroutine(Knock(feet));
        }

        /// <summary>Back down the lane a few metres (stopping short of a fallen stone behind the player), over a quarter
        /// second on desktop; in one step in VR (moving the view smoothly there is uncomfortable).</summary>
        IEnumerator Knock(Vector3 feet)
        {
            m_Knock = true;
            var to = feet;
            to.z = Mathf.Max(feet.z - KnockBack, -15f);
            int lane = LaneOf(feet.x);
            foreach (var o in Obstacles)
                if (o.Lane == lane && Mathf.Abs(feet.x - LaneX(lane)) < 1.25f && o.Z < feet.z && o.Z + 0.85f > to.z) to.z = o.Z + 0.85f;
            to.x = Mathf.Clamp(to.x, RunX - HalfW + 0.35f, RunX + HalfW - 0.35f);
            var rig = Game.Rig;
            if (rig is DesktopRig desk)
            {
                float yaw = desk.Yaw, pitch = desk.Pitch;
                var from = feet;
                for (float t = 0f; t < 1f; t += Time.deltaTime / 0.26f)
                {
                    var p = Vector3.Lerp(from, to, 1f - (1f - t) * (1f - t));
                    p.y = from.y;
                    desk.PlaceAt(transform.TransformPoint(p), yaw);
                    desk.SetPitch(pitch + Mathf.Sin(t * Mathf.PI) * -6f);
                    yield return null;
                }
                desk.PlaceAt(transform.TransformPoint(new Vector3(to.x, feet.y, to.z)), yaw);
                desk.SetPitch(pitch);
            }
            else if (rig != null)
            {
                rig.PlaceAt(transform.TransformPoint(new Vector3(to.x, feet.y, to.z)), rig.Head.eulerAngles.y);
                yield return null;
            }
            yield return new WaitForSeconds(0.25f);
            m_Knock = false;
        }

        void Take(Coin c)
        {
            c.Taken = true;
            c.T.gameObject.SetActive(false);
            Coins++;
            m_Streak = Time.time - m_LastCoin < 2.2f ? m_Streak + 1 : 0;
            m_LastCoin = Time.time;
            AudioDirector.I?.Play("coin_token", c.T.position, 0.75f, 1f + Mathf.Min(m_Streak, 6) * 0.07f);
            Cues?.Sparkle(c.T.position);
            Game.Story?.RestoreVitality(CoinRestore);
            CoinTaken?.Invoke(this);
        }

        /// <summary>Where the guide points: a few metres down the lane of the next coins, or the portal near the end.</summary>
        public Vector3 WaypointLocal(Vector3 feet)
        {
            if (feet.z > Z1 - 6f) return PortalLocal + Vector3.up * 1.2f;
            float z = Mathf.Max(feet.z, Z0 - 1f) + 7f;
            int lane = LaneOf(feet.x);
            foreach (var c in CoinList)
                if (!c.Taken && c.Z > feet.z + 1f && c.Z < feet.z + 9f) { lane = c.Lane; z = c.Z; break; }
            if (ObstacleNear(lane, z - 1.2f, z + 1.2f)) lane = (lane + 1) % 3;
            return new Vector3(LaneX(lane), FloorY + 1.1f, Mathf.Min(z, Z1 + 1f));
        }

        /// <summary>The nearest mummy coming down the player's lane ahead of them (for the guide's warning), or null.</summary>
        public Mummy ThreatAhead(Vector3 feet, float range)
        {
            Mummy best = null;
            float bd = range;
            foreach (var m in Mummies)
            {
                if (m.State != MummyState.Walking) continue;
                float dz = m.Z - feet.z;
                if (dz < -0.5f || dz > bd || Mathf.Abs(m.X - feet.x) > 1.1f) continue;
                bd = dz; best = m;
            }
            return best;
        }
    }
}
