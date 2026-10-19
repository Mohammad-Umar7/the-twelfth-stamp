using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    // The way out of the festival street to the tower (open world: once France's seal is verified, its stamp waits in the
    // garden at the far end of the bridge and the player runs for it). A gap in the parapet at the street's north end and
    // three steps lead down to a cobbled lower quay (bollards, mooring rings, a moored barge); a wide stair climbs beside
    // the old quay wall to a stone bridge of four arches; the bridge lands on the east quay (y 4.6), fenced above the river
    // by a new parapet. Every edge over water is fenced: the cell's river has no floor, and the vista's river (beyond
    // z 54) does, between 6 m quay walls, so an invisible blocker also keeps VR teleports out of it. Visual pieces are
    // batched per region (lights pick per renderer); colliders are invisible boxes.
    public partial class ParisCell
    {
        const float WalkQuayY = -0.6f, WalkDeckY = 4.6f, WalkDeckZ0 = 34.8f, WalkDeckZ1 = 41.2f, WalkX0 = 4.1f, WalkX1 = 54f;
        const float WalkQuayX1 = 7.6f, WalkQuayZ0 = 10.4f, WalkStairZ0 = 25.4f, WalkGapZ0 = 10.9f, WalkGapZ1 = 12.3f, WalkBase = -2.4f;

        /// <summary>
        /// Builds the river walk: the parapet gap and steps, the lower quay, the stair to the bridge, the bridge, the east
        /// quay's parapet, the west bank seen from the bridge, a moored barge, lamps, rain that follows the player off the
        /// street, and the fences; then tells the vista its stamp spot can be reached. Call after the landmark is built.
        /// </summary>
        void BuildRiverWalk(Transform g)
        {
            var root = new GameObject("RiverWalk").transform;
            root.SetParent(g, false);
            var stone = Mats.Lit(new Color(0.8f, 0.76f, 0.68f), 0.3f, 0f, "limestone_blocks");
            var quay = Mats.Lit(new Color(0.58f, 0.55f, 0.5f), 0.45f, 0f, "limestone_blocks");
            var trim = Mats.Lit(new Color(0.86f, 0.82f, 0.74f), 0.3f, 0f, "limestone_blocks");
            var granite = Mats.Lit(new Color(0.52f, 0.51f, 0.49f), 0.5f, 0f, "granite_pavers");
            var cobble = Mats.Lit(new Color(0.95f, 0.95f, 1f), 0.6f, 0f, "cobble_wet");
            var iron = Mats.Lit(new Color(0.06f, 0.07f, 0.07f), 0.55f, 0.8f);
            var lantern = Mats.Emissive(new Color(0.3f, 0.24f, 0.16f), new Color(1.25f, 0.82f, 0.42f));
            var puddle = Mats.Lit(new Color(0.03f, 0.035f, 0.045f), 0.96f, 0f);

            var quayKit = new KitBatch();
            OpenParapet(g, root, quayKit, stone, trim, granite, quay);
            LowerQuay(root, quayKit, quay, cobble, granite, iron, lantern, stone, trim);
            QuayStair(root, quayKit, quay, granite, stone, trim, iron, lantern);
            quayKit.Bake(root, "RiverWalkQuay");

            var bridgeKit = new KitBatch();
            WalkBridge(root, bridgeKit, stone, trim, granite, iron, lantern);
            bridgeKit.Bake(root, "RiverWalkBridge");

            var eastKit = new KitBatch();
            EastQuayParapet(root, eastKit, stone, trim, iron, lantern);
            eastKit.Bake(root, "RiverWalkEast");

            var westKit = new KitBatch();
            WestBank(westKit, stone, trim, granite, iron, lantern);
            westKit.Bake(root, "RiverWalkWestBank", false);

            var bargeKit = new KitBatch();
            Barge(bargeKit, iron, lantern);
            bargeKit.Bake(root, "RiverWalkBarge", false);

            UrbanKit.Puddles(root, new Vector3(5.7f, WalkQuayY, 18.5f), new Vector2(2.4f, 12f), 6, 811, puddle);
            UrbanKit.Puddles(root, new Vector3(29f, WalkDeckY, 38f), new Vector2(44f, 4.2f), 9, 812, puddle);

            // Four practical lights along the way (the lanterns themselves are emissive).
            var warm = new Color(1f, 0.78f, 0.5f);
            AddPointLight(root, new Vector3(5.9f, 2.8f, 18.5f), warm, 1.4f, 8f);
            AddPointLight(root, new Vector3(6.4f, 7.8f, 36.6f), warm, 1.5f, 9f);
            AddPointLight(root, new Vector3(30.8f, 8.4f, 38f), warm, 1.4f, 10f);
            AddPointLight(root, new Vector3(55.6f, 8.2f, 38f), warm, 1.4f, 9f);

            // The street's rain stays over the street; off it, a smaller rain keeps over the player.
            var rain = Kit.Weather(root, new Vector3(0f, 7f, 0f), new Vector3(18f, 0.5f, 18f), true, new Color(0.75f, 0.8f, 0.9f, 0.4f), 650f, 9f);
            rain.gameObject.AddComponent<RouteRain>();

            // Fences that used to be the old bounds: the dark alley mouth stays shut (the street floor ends behind it).
            WalkSolid(root, "AlleyBlock", new Vector3(-3.6f, 2f, 12.4f), new Vector3(1.7f, 4f, 0.3f), false);
            // The vista's river (beyond z 54) is solid ground between 6 m quay walls: keep VR teleports out of it.
            WalkSolid(root, "VistaRiverBlocker", new Vector3(29.05f, 0.8f, 98.25f), new Vector3(49.7f, 4.8f, 88.5f), false);

            Landmarks.MarkReachable(this);
        }

        // ------------------------------------------------------------------ colliders
        /// <summary>An invisible box collider: walkable (Floor layer, stone footsteps) or a barrier (Default layer).</summary>
        static GameObject WalkSolid(Transform parent, string name, Vector3 center, Vector3 size, bool floor, Quaternion? rot = null)
        {
            var go = new GameObject(name) { layer = floor ? Layers.Floor : Layers.Default };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = rot ?? Quaternion.identity;
            go.AddComponent<BoxCollider>().size = size;
            if (floor) go.AddComponent<Surface>().Kind = "stone";
            return go;
        }

        /// <summary>A walkable slab whose top surface runs along the line a..b (a stair's pitch line), <paramref name="width"/> across.</summary>
        static void WalkRamp(Transform parent, string name, Vector3 a, Vector3 b, float width)
        {
            var d = b - a;
            float len = d.magnitude;
            var dir = d / len;
            var side = Vector3.Cross(Vector3.up, dir).normalized;
            var normal = Vector3.Cross(dir, side).normalized;
            const float thick = 0.3f;
            WalkSolid(parent, name, (a + b) * 0.5f - normal * (thick * 0.5f), new Vector3(width, thick, len), true, Quaternion.LookRotation(dir, normal));
        }

        /// <summary>A barrier whose underside runs along the line a..b (a stair's balustrade), <paramref name="height"/> tall.</summary>
        static void WalkSlopedWall(Transform parent, string name, Vector3 a, Vector3 b, float thickness, float height)
        {
            var d = b - a;
            float len = d.magnitude;
            var dir = d / len;
            var side = Vector3.Cross(Vector3.up, dir).normalized;
            var normal = Vector3.Cross(dir, side).normalized;
            WalkSolid(parent, name, (a + b) * 0.5f + normal * (height * 0.5f), new Vector3(thickness, height, len), false, Quaternion.LookRotation(dir, normal));
        }

        // ------------------------------------------------------------------ the street end
        /// <summary>
        /// Opens the parapet at the street's north end (z 10.9..12.3): the parapet and coping are shortened, a newel and a
        /// pier frame the gap, the bookseller's box that stood there goes, and three steps lead down to the quay.
        /// </summary>
        void OpenParapet(Transform g, Transform root, KitBatch k, Material stone, Material trim, Material granite, Material quay)
        {
            const float end = WalkGapZ0 - 0.25f;
            var parapet = g.Find("Parapet");
            if (parapet != null)
            {
                parapet.localScale = new Vector3(1f, 1f, (end + 10f) / 23f);
                parapet.localPosition = new Vector3(parapet.localPosition.x, parapet.localPosition.y, (end - 10f) * 0.5f);
            }
            var coping = g.Find("Coping");
            if (coping != null)
            {
                coping.localScale = new Vector3(1f, 1f, (end + 10.1f) / 23.2f);
                coping.localPosition = new Vector3(coping.localPosition.x, coping.localPosition.y, (end - 10.1f) * 0.5f);
            }
            var gone = new List<GameObject>();
            foreach (Transform c in g)
                if (c.name == "Bouquiniste" && c.localPosition.z > end - 0.5f && c.localPosition.z < WalkGapZ1 + 0.5f) gone.Add(c.gameObject);
            foreach (var go in gone) Object.DestroyImmediate(go);

            // A newel ends the parapet; a pier closes the corner by the north wall, down to the quay.
            k.Box(stone, new Vector3(3.85f, 0.56f, end + 0.13f), new Vector3(0.56f, 1.12f, 0.32f), 2.5f);
            k.Box(trim, new Vector3(3.85f, 1.16f, end + 0.13f), new Vector3(0.66f, 0.1f, 0.42f), 2.5f);
            WalkSolid(root, "GapNewel", new Vector3(3.85f, 0.6f, end + 0.13f), new Vector3(0.56f, 1.2f, 0.32f), false);
            k.Box(stone, new Vector3(3.85f, (WalkBase + 1.12f) * 0.5f, 12.6f), new Vector3(0.56f, 1.12f - WalkBase, 0.62f), 2.5f);
            k.Box(trim, new Vector3(3.85f, 1.16f, 12.6f), new Vector3(0.66f, 0.1f, 0.72f), 2.5f);
            WalkSolid(root, "GapPier", new Vector3(3.85f, (WalkBase + 1.2f) * 0.5f, 12.6f), new Vector3(0.56f, 1.2f - WalkBase, 0.62f), false);
            // The embankment under the street's edge, seen from the quay.
            k.Box(quay, new Vector3(3.9f, (WalkBase - 0.3f) * 0.5f, 1.15f), new Vector3(0.4f, -0.3f - WalkBase, 22.3f), 2.5f);
            WalkSolid(root, "Embankment", new Vector3(3.9f, (WalkBase - 0.3f) * 0.5f, 1.15f), new Vector3(0.4f, -0.3f - WalkBase, 22.3f), false);

            // Three steps down from the street's edge (x 4.1, y 0) to the quay (-0.6), granite treads on stone.
            float z0 = end + 0.29f, z1 = WalkGapZ1 - 0.02f, zc = (z0 + z1) * 0.5f, w = z1 - z0;
            for (int i = 0; i < 3; i++)
            {
                float x0 = WalkX0 + i * 0.32f, top = -0.15f * (i + 1);
                k.Box(quay, new Vector3(x0 + 0.16f, (top + WalkQuayY) * 0.5f, zc), new Vector3(0.32f, top - WalkQuayY, w));
                k.Box(granite, new Vector3(x0 + 0.16f, top - 0.025f, zc), new Vector3(0.34f, 0.05f, w));
            }
            WalkRamp(root, "GapSteps", new Vector3(WalkX0 - 0.05f, 0f, zc), new Vector3(WalkX0 + 1.2f, WalkQuayY, zc), w);
        }

        // ------------------------------------------------------------------ the lower quay
        /// <summary>
        /// The cobbled lower quay (x 4.1..7.6, z 10.4..25.4, top -0.6) under the old quay wall: a granite kerb with bollards
        /// and chains and mooring rings on the water side, a railing at the south end, a bench and two wall lanterns; the
        /// wall (z 12.9..55.5, up to the west bank at 4.6) carries a balustrade and runs on to meet the vista's west quay.
        /// </summary>
        void LowerQuay(Transform root, KitBatch k, Material quay, Material cobble, Material granite, Material iron, Material lantern, Material stone, Material trim)
        {
            const float z0 = WalkQuayZ0, z1 = WalkStairZ0, zc = (z0 + z1) * 0.5f, len = z1 - z0;
            float xc = (WalkX0 + WalkQuayX1) * 0.5f, wx = WalkQuayX1 - WalkX0;
            k.Box(quay, new Vector3(xc, (WalkBase + WalkQuayY - 0.02f) * 0.5f, zc), new Vector3(wx, WalkQuayY - 0.02f - WalkBase, len), 2.5f);
            k.Box(cobble, new Vector3(xc - 0.15f, WalkQuayY - 0.01f, zc), new Vector3(wx - 0.3f, 0.02f, len), 1f);
            WalkSolid(root, "QuayFloor", new Vector3(xc, (WalkBase + WalkQuayY) * 0.5f, zc), new Vector3(wx, WalkQuayY - WalkBase, len), true);
            // Water side: kerb, bollards and chain, mooring rings, the fence (no visible rail: the bollards and chain read as one).
            k.Box(granite, new Vector3(WalkQuayX1 - 0.15f, WalkQuayY + 0.04f, zc), new Vector3(0.3f, 0.1f, len));
            var bollards = new List<Vector3>();
            for (float z = z0 + 0.9f; z < z1 - 0.3f; z += 2.75f) bollards.Add(new Vector3(WalkQuayX1 - 0.17f, WalkQuayY + 0.09f, z));
            foreach (var b in bollards) Bollard(k, iron, b);
            for (int i = 0; i + 1 < bollards.Count; i++) Chain(k, iron, bollards[i] + Vector3.up * 0.48f, bollards[i + 1] + Vector3.up * 0.48f, 0.16f);
            for (float z = z0 + 2.2f; z < z1; z += 5.5f) k.Of(iron).AddTorus(new Vector3(WalkQuayX1 + 0.025f, WalkQuayY - 0.45f, z), 0.12f, 0.018f, 14, 4, Quaternion.Euler(0f, 0f, 90f));
            WalkSolid(root, "QuayEdge", new Vector3(WalkQuayX1 - 0.12f, WalkQuayY + 0.6f, zc), new Vector3(0.24f, 1.2f, len + 0.2f), false);
            UrbanKit.Railing(k, iron, new Vector3(WalkX0 + 0.05f, WalkQuayY, z0 + 0.08f), new Vector3(WalkQuayX1 - 0.02f, WalkQuayY, z0 + 0.08f), 0.95f, Vector3.zero);
            WalkSolid(root, "QuaySouth", new Vector3(xc, WalkQuayY + 0.6f, z0 + 0.08f), new Vector3(wx, 1.2f, 0.2f), false);

            // The old quay wall, rusticated, with its coping and a balustrade along the west bank above.
            const float wz0 = 12.9f, wz1 = 55.5f;
            float wzc = (wz0 + wz1) * 0.5f, wl = wz1 - wz0;
            k.Box(quay, new Vector3(3.85f, (WalkBase + WalkDeckY) * 0.5f, wzc), new Vector3(0.5f, WalkDeckY - WalkBase, wl), 2.5f);
            k.Box(trim, new Vector3(3.85f, WalkDeckY + 0.06f, wzc), new Vector3(0.66f, 0.12f, wl + 0.1f), 2.5f);
            for (int i = 0; i < 8; i++) k.Box(iron, new Vector3(4.104f, WalkQuayY + 0.5f + i * 0.62f, wzc), new Vector3(0.01f, 0.03f, wl));
            WalkBalustrade(k, stone, trim, new Vector3(3.85f, 0f, wz0), new Vector3(3.85f, 0f, wz1), WalkDeckY + 0.12f);
            WalkSolid(root, "QuayWall", new Vector3(3.85f, (WalkBase + WalkDeckY + 1.2f) * 0.5f, wzc), new Vector3(0.5f, WalkDeckY + 1.2f - WalkBase, wl), false);

            // A stone bench against the wall, a drain grating, two lanterns on brackets.
            k.Box(stone, new Vector3(4.42f, WalkQuayY + 0.22f, 17.2f), new Vector3(0.62f, 0.44f, 1.9f));
            k.Box(trim, new Vector3(4.44f, WalkQuayY + 0.46f, 17.2f), new Vector3(0.66f, 0.06f, 1.96f));
            k.Box(iron, new Vector3(6.2f, WalkQuayY + 0.005f, 21.8f), new Vector3(0.5f, 0.012f, 0.5f));
            foreach (float z in new[] { 15.2f, 22.4f }) WallLantern(k, iron, lantern, new Vector3(WalkX0, 2.3f, z));
        }

        /// <summary>
        /// The wide stair from the quay up beside the wall to the bridge deck (31 risers, z 25.4..34.8, x 4.1..7.6): stone steps
        /// with granite treads, a sloped stringer and balustrade on the water side, a lamp on the newel at its foot.
        /// </summary>
        void QuayStair(Transform root, KitBatch k, Material quay, Material granite, Material stone, Material trim, Material iron, Material lantern)
        {
            const int risers = 31;
            float rise = (WalkDeckY - WalkQuayY) / risers, run = (WalkDeckZ0 - WalkStairZ0) / risers;
            float xc = (WalkX0 + WalkQuayX1) * 0.5f, wx = WalkQuayX1 - WalkX0;
            for (int i = 0; i < risers; i++)
            {
                float z0 = WalkStairZ0 + i * run, top = WalkQuayY + (i + 1) * rise;
                k.Box(quay, new Vector3(xc, (top + WalkBase) * 0.5f, z0 + run * 0.5f), new Vector3(wx, top - WalkBase, run), 2.5f);
                k.Box(granite, new Vector3(xc, top - 0.03f, z0 + run * 0.5f), new Vector3(wx, 0.06f, run + 0.02f));
            }
            // The pitch line through the middle of each tread; a landing slab under the top step.
            WalkRamp(root, "QuayStair", new Vector3(xc, WalkQuayY - 0.17f, WalkStairZ0 - run * 0.5f - 0.3f), new Vector3(xc, WalkDeckY, WalkDeckZ0 - run * 0.5f), wx);
            WalkSolid(root, "StairHead", new Vector3(xc, WalkDeckY - 0.3f, WalkDeckZ0 - 0.1f), new Vector3(wx, 0.6f, 0.4f), true);
            // Water side: a sloped stringer wall, balusters at every step under a sloped rail, a newel and lamp at the foot.
            float Y(float z) => WalkQuayY + (z - WalkStairZ0) * rise / run;
            const float sx = WalkQuayX1 + 0.16f;
            var stringer = new List<Vector2>
            {
                new Vector2(WalkStairZ0 - 0.5f, WalkBase), new Vector2(WalkDeckZ0, WalkBase), new Vector2(WalkDeckZ0, WalkDeckY + 0.25f),
                new Vector2(WalkStairZ0, WalkQuayY + 0.25f), new Vector2(WalkStairZ0 - 0.5f, WalkQuayY + 0.25f),
            };
            k.Of(quay).AddExtrudedXY(stringer, 0.32f, new Vector3(sx, 0f, 0f), Quaternion.Euler(0f, -90f, 0f), 2.5f);
            for (int i = 0; i < risers; i++)
            {
                float z = WalkStairZ0 + (i + 0.5f) * run, b = Y(z) + 0.25f;
                k.Box(stone, new Vector3(sx, b + 0.31f, z), new Vector3(0.09f, 0.62f, 0.09f));
                k.Box(stone, new Vector3(sx, b + 0.2f, z), new Vector3(0.14f, 0.22f, 0.14f));
            }
            var r0 = new Vector3(sx, Y(WalkStairZ0) + 0.25f + 0.68f, WalkStairZ0);
            var r1 = new Vector3(sx, Y(WalkDeckZ0) + 0.25f + 0.68f, WalkDeckZ0);
            k.Box(trim, (r0 + r1) * 0.5f, new Vector3(0.38f, 0.12f, (r1 - r0).magnitude), Quaternion.LookRotation((r1 - r0).normalized, Vector3.up));
            WalkSlopedWall(root, "StairRail", new Vector3(sx, Y(WalkStairZ0) + 0.25f, WalkStairZ0), new Vector3(sx, Y(WalkDeckZ0) + 0.25f, WalkDeckZ0), 0.36f, 1.0f);
            var newel = new Vector3(sx, WalkQuayY, WalkStairZ0 - 0.3f);
            k.Box(stone, newel + Vector3.up * 0.6f, new Vector3(0.5f, 1.2f, 0.5f), 2.5f);
            k.Box(trim, newel + Vector3.up * 1.24f, new Vector3(0.6f, 0.08f, 0.6f), 2.5f);
            WalkSolid(root, "StairNewel", newel + Vector3.up * 0.6f, new Vector3(0.5f, 1.2f, 0.5f), false);
            WalkLamp(k, iron, lantern, newel + Vector3.up * 1.28f, 2.9f);
        }

        // ------------------------------------------------------------------ the bridge
        /// <summary>
        /// A stone bridge of four arches (z 34.8..41.2) from the quay wall (x 4.1) to the east quay (x 54), deck at 4.6:
        /// an abutment, spandrels with semicircular arches springing above the water, cutwaters at the piers, voussoir
        /// rings and keystones, a cornice, turned balustrades on piers (with colliders), lamp standards clear of the line
        /// from the street to the tower, granite paving.
        /// </summary>
        void WalkBridge(Transform root, KitBatch k, Material stone, Material trim, Material granite, Material iron, Material lantern)
        {
            const float zc = (WalkDeckZ0 + WalkDeckZ1) * 0.5f, width = WalkDeckZ1 - WalkDeckZ0, body = WalkDeckY - 0.09f;
            WalkSolid(root, "BridgeDeck", new Vector3((WalkX0 + WalkX1) * 0.5f, WalkDeckY - 0.45f, zc), new Vector3(WalkX1 - WalkX0, 0.9f, width), true);
            k.Box(granite, new Vector3((WalkX0 + WalkX1) * 0.5f, WalkDeckY - 0.045f, zc), new Vector3(WalkX1 - WalkX0, 0.09f, width), 2f);
            // The abutment under the stair head.
            k.Box(stone, new Vector3((WalkX0 + WalkQuayX1) * 0.5f, (WalkBase + body) * 0.5f, zc), new Vector3(WalkQuayX1 - WalkX0, body - WalkBase, width), 2.5f);
            // Four bays: equal 9.4 m arches between 2.2 m piers (centres 19.2, 30.8, 42.4), springing at -1.0, crowns at 3.7.
            const float span = 9.4f, R = span * 0.5f, spring = 3.7f - R;
            float[] open = { 8.7f, 20.3f, 31.9f, 43.5f };
            for (int b = 0; b < 4; b++)
            {
                float xa = open[b], xb = xa + span, xcen = xa + R;
                float xl = b == 0 ? WalkQuayX1 : xa - 1.1f, xr = b == 3 ? WalkX1 : xb + 1.1f;
                var poly = new List<Vector2> { new Vector2(xl, WalkBase), new Vector2(xa, WalkBase) };
                const int seg = 16;
                for (int i = 0; i <= seg; i++)
                {
                    float a = Mathf.PI * (1f - i / (float)seg);
                    poly.Add(new Vector2(xcen + Mathf.Cos(a) * R, spring + Mathf.Sin(a) * R));
                }
                poly.Add(new Vector2(xb, WalkBase));
                poly.Add(new Vector2(xr, WalkBase));
                poly.Add(new Vector2(xr, body));
                poly.Add(new Vector2(xl, body));
                k.Of(stone).AddExtrudedXY(poly, width, new Vector3(0f, 0f, zc), Quaternion.identity, 2.5f);
                // Voussoir rings and keystones on both faces.
                foreach (float fz in new[] { WalkDeckZ0 - 0.04f, WalkDeckZ1 + 0.04f })
                {
                    k.Of(trim).AddArch(new Vector3(xcen, spring, fz), R, R + 0.5f, 0.08f, seg, Quaternion.identity, 2f);
                    k.Box(trim, new Vector3(xcen, spring + R + 0.28f, fz + (fz < zc ? -0.03f : 0.03f)), new Vector3(0.55f, 0.7f, 0.14f));
                }
                // A cutwater on each face of the pier to the east of this bay.
                if (b < 3)
                {
                    float px = xb + 1.1f;
                    var south = new List<Vector2> { new Vector2(px - 1.1f, WalkDeckZ0), new Vector2(px, WalkDeckZ0 - 1.3f), new Vector2(px + 1.1f, WalkDeckZ0) };
                    var north = new List<Vector2> { new Vector2(px - 1.1f, WalkDeckZ1), new Vector2(px + 1.1f, WalkDeckZ1), new Vector2(px, WalkDeckZ1 + 1.3f) };
                    k.Of(stone).AddPrism(south, WalkBase, 0.4f, 2.5f);
                    k.Of(stone).AddPrism(north, WalkBase, 0.4f, 2.5f);
                    var southCap = new List<Vector2> { new Vector2(px - 1.0f, WalkDeckZ0), new Vector2(px, WalkDeckZ0 - 1.15f), new Vector2(px + 1.0f, WalkDeckZ0) };
                    var northCap = new List<Vector2> { new Vector2(px - 1.0f, WalkDeckZ1), new Vector2(px + 1.0f, WalkDeckZ1), new Vector2(px, WalkDeckZ1 + 1.15f) };
                    k.Of(trim).AddPrism(southCap, 0.4f, 0.58f, 2f);
                    k.Of(trim).AddPrism(northCap, 0.4f, 0.58f, 2f);
                }
            }
            // The cornice over the arches, both faces.
            foreach (float fz in new[] { WalkDeckZ0 - 0.08f, WalkDeckZ1 + 0.08f })
                k.Box(trim, new Vector3((WalkQuayX1 + WalkX1) * 0.5f, 4.32f, fz), new Vector3(WalkX1 - WalkQuayX1, 0.16f, 0.2f), 2f);
            // Balustrades (the south one starts where the stair arrives), aligned piers every 5.8 m, and their colliders.
            const float bs = WalkDeckZ0 + 0.23f, bn = WalkDeckZ1 - 0.23f;
            WalkBalustrade(k, stone, trim, new Vector3(WalkQuayX1, 0f, bs), new Vector3(WalkX1, 0f, bs), WalkDeckY);
            WalkBalustrade(k, stone, trim, new Vector3(WalkX0, 0f, bn), new Vector3(WalkQuayX1, 0f, bn), WalkDeckY);
            WalkBalustrade(k, stone, trim, new Vector3(WalkQuayX1, 0f, bn), new Vector3(WalkX1, 0f, bn), WalkDeckY);
            WalkSolid(root, "BridgeRailS", new Vector3((WalkQuayX1 + WalkX1) * 0.5f, WalkDeckY + 0.6f, bs), new Vector3(WalkX1 - WalkQuayX1, 1.2f, 0.46f), false);
            WalkSolid(root, "BridgeRailN", new Vector3((WalkX0 + WalkX1) * 0.5f, WalkDeckY + 0.6f, bn), new Vector3(WalkX1 - WalkX0, 1.2f, 0.46f), false);
            // Lamp standards on the piers, kept off the street's line of sight to the tower (x 25..37 here).
            foreach (float lx in new[] { WalkX0, 13.4f, 19.2f, 42.4f, 48.2f, WalkX1 })
                foreach (float lz in new[] { bs, bn })
                {
                    if (lx == WalkX0 && lz == bs) continue; // the stair arrives there
                    WalkLamp(k, iron, lantern, new Vector3(lx, WalkDeckY + 1.08f, lz), 3.0f);
                }
        }

        // ------------------------------------------------------------------ the far bank and the west bank
        /// <summary>A stone parapet along the east quay's edge (x 54..54.45) wherever the bounds let the player walk, open
        /// where the bridge lands; its colliders keep everyone out of the river's walled channel.</summary>
        void EastQuayParapet(Transform root, KitBatch k, Material stone, Material trim, Material iron, Material lantern)
        {
            foreach (var (z0, z1) in new[] { (-8.4f, WalkDeckZ0), (WalkDeckZ1, 142.4f) })
            {
                float zc = (z0 + z1) * 0.5f, len = z1 - z0;
                k.Box(stone, new Vector3(54.225f, WalkDeckY + 0.45f, zc), new Vector3(0.45f, 0.9f, len), 2.5f);
                k.Box(trim, new Vector3(54.225f, WalkDeckY + 0.94f, zc), new Vector3(0.6f, 0.09f, len + 0.2f), 2.5f);
                WalkSolid(root, "EastParapet", new Vector3(54.225f, WalkDeckY + 0.6f, zc), new Vector3(0.45f, 1.2f, len), false);
            }
        }

        /// <summary>
        /// The west bank above the quay wall, seen from the bridge and over the wall (no colliders, not reachable): an upper
        /// quay at 4.6 that meets the vista's west quay at z 55, plane trees, lamps, and a row of limestone blocks with
        /// mansards and lit windows facing the river.
        /// </summary>
        void WestBank(KitBatch k, Material stone, Material trim, Material granite, Material iron, Material lantern)
        {
            var zinc = Mats.Lit(new Color(0.85f, 0.9f, 0.97f), 0.55f, 0.6f, "metal_brushed");
            var glassDark = Mats.Lit(new Color(0.04f, 0.05f, 0.065f), 0.92f, 0.2f);
            var glassLit = Mats.Emissive(new Color(0.3f, 0.25f, 0.18f), new Color(0.75f, 0.47f, 0.25f));
            var awning = Mats.Lit(new Color(0.46f, 0.08f, 0.07f), 0.25f, 0f, "fabric_linen");
            var bark = Mats.Lit(new Color(0.75f, 0.7f, 0.62f), 0.3f, 0f, "wood_dark");
            var leaf = Mats.Lit(new Color(0.16f, 0.24f, 0.13f), 0.3f);
            k.Box(granite, new Vector3(-2.7f, WalkDeckY - 0.1f, 34.2f), new Vector3(12.6f, 0.2f, 42.6f), 4f);
            k.Box(granite, new Vector3(-4f, WalkDeckY - 0.1f, 68f), new Vector3(10f, 0.2f, 25f), 4f);
            var rnd = new System.Random(1871);
            foreach (var t in new[] { new Vector3(0.6f, 0f, 17f), new Vector3(0.6f, 0f, 24.5f), new Vector3(0.6f, 0f, 32f), new Vector3(0.6f, 0f, 39.5f), new Vector3(0.6f, 0f, 47f), new Vector3(-3.5f, 0f, 61f), new Vector3(-3.5f, 0f, 72f) })
            {
                var foot = new Vector3(t.x, WalkDeckY, t.z);
                k.Of(bark).AddFrustum(foot, 0.26f, 0.18f, 4.6f, 7);
                for (int i = 0; i < 4; i++)
                {
                    float a = i * 1.9f + (float)rnd.NextDouble();
                    k.Of(leaf).AddSphere(foot + new Vector3(Mathf.Cos(a) * 1.1f, 6.2f + i * 0.8f, Mathf.Sin(a) * 1.1f), 1.7f + (float)rnd.NextDouble() * 0.6f, 9, 7);
                }
            }
            foreach (float z in new[] { 20.5f, 33.5f, 46.5f }) WalkLamp(k, iron, lantern, new Vector3(2.6f, WalkDeckY, z), 3.6f);
            // Riverside blocks, fronts at x -9 facing the river.
            float zb = 13.2f;
            int n = 0;
            while (zb < 80f)
            {
                float len = Mathf.Min(11.5f + (float)rnd.NextDouble() * 4.5f, 80.6f - zb);
                RiversideBlock(k, stone, trim, zinc, glassDark, glassLit, awning, iron, zb, zb + len, 5 + (n++ % 2), rnd);
                zb += len + 0.5f;
            }
        }

        /// <summary>One riverside block (front at x -9, facing +X): shopfronts, storeys of tall windows (a third lit), a
        /// balcony on the second and top floors, a cornice, a zinc mansard with dormers and chimneys.</summary>
        static void RiversideBlock(KitBatch k, Material stone, Material trim, Material zinc, Material dark, Material lit, Material awning, Material iron,
            float z0, float z1, int floors, System.Random rnd)
        {
            const float front = -9f, depth = 12f, ground = 4.2f, floorH = 3.1f;
            float baseY = WalkDeckY, h = ground + (floors - 1) * floorH, zc = (z0 + z1) * 0.5f, len = z1 - z0;
            k.Box(stone, new Vector3(front - depth * 0.5f, baseY + h * 0.5f, zc), new Vector3(depth, h, len), 4f);
            // Ground floor: shopfronts with a warm glow under red awnings, doors between.
            int bays = Mathf.Max(2, Mathf.FloorToInt(len / 2.6f));
            float bay = len / bays;
            for (int i = 0; i < bays; i++)
            {
                float z = z0 + (i + 0.5f) * bay;
                if (i % 3 == 1) { k.Box(dark, new Vector3(front + 0.02f, baseY + 1.3f, z), new Vector3(0.04f, 2.6f, 1.2f)); continue; }
                k.Box(lit, new Vector3(front + 0.02f, baseY + 1.5f, z), new Vector3(0.04f, 2.4f, bay - 0.5f));
                k.Box(awning, new Vector3(front + 0.55f, baseY + 3.0f, z), new Vector3(1.1f, 0.04f, bay - 0.3f), Quaternion.Euler(0f, 0f, -16f));
            }
            k.Box(trim, new Vector3(front + 0.08f, baseY + ground - 0.1f, zc), new Vector3(0.24f, 0.22f, len + 0.04f), 2f);
            // Storeys: tall windows (a third lit), a string course each floor, balconies on the second and the top floor.
            int cols = Mathf.Max(2, Mathf.FloorToInt(len / 2.4f));
            float pitch = len / cols;
            for (int f = 1; f < floors; f++)
            {
                float y = baseY + ground + (f - 1) * floorH;
                k.Box(trim, new Vector3(front + 0.05f, y + 0.05f, zc), new Vector3(0.12f, 0.1f, len), 2f);
                for (int c = 0; c < cols; c++)
                    k.Box(rnd.NextDouble() < 0.34 ? lit : dark, new Vector3(front + 0.02f, y + 1.3f, z0 + (c + 0.5f) * pitch), new Vector3(0.04f, 1.95f, Mathf.Min(1.1f, pitch * 0.5f)));
                if (f == 1 || f == floors - 1)
                {
                    k.Box(trim, new Vector3(front + 0.3f, y + 0.12f, zc), new Vector3(0.6f, 0.12f, len - 0.2f), 2f);
                    k.Box(iron, new Vector3(front + 0.58f, y + 0.62f, zc), new Vector3(0.03f, 0.9f, len - 0.3f));
                }
            }
            // Cornice, mansard, dormers, chimneys.
            k.Box(trim, new Vector3(front - 0.05f, baseY + h + 0.17f, zc), new Vector3(0.6f, 0.34f, len + 0.2f), 2f);
            var slope = Quaternion.Euler(0f, 0f, 24f);
            k.Box(zinc, new Vector3(front - 0.55f, baseY + h + 1.75f, zc), new Vector3(0.1f, 3.0f, len), slope, 2f);
            k.Box(zinc, new Vector3(front - depth * 0.55f, baseY + h + 3.05f, zc), new Vector3(depth - 1.4f, 0.1f, len), 2f);
            for (float z = z0 + 1.6f; z < z1 - 1f; z += 3.2f)
            {
                k.Box(zinc, new Vector3(front - 0.75f, baseY + h + 1.25f, z), new Vector3(0.9f, 1.2f, 1.0f));
                k.Box(rnd.NextDouble() < 0.3 ? lit : dark, new Vector3(front - 0.29f, baseY + h + 1.2f, z), new Vector3(0.02f, 0.75f, 0.6f));
            }
            foreach (float z in new[] { z0 + 1.2f, z1 - 1.6f }) k.Box(stone, new Vector3(front - 3.5f, baseY + h + 3.8f, z), new Vector3(0.5f, 1.5f, 0.8f));
        }

        /// <summary>A living barge moored at the quay (x 8.3..12.7, z 12.8..33.4): a black hull with a pointed bow, a green
        /// cabin with warm portholes, a wheelhouse at the stern, pots on the roof, ropes to the bollards.</summary>
        static void Barge(KitBatch k, Material iron, Material warm)
        {
            var hull = Mats.Lit(new Color(0.07f, 0.08f, 0.1f), 0.5f, 0.3f);
            var wood = Mats.Lit(new Color(1f, 0.92f, 0.85f), 0.45f, 0f, "wood_dark");
            var paint = Mats.Lit(new Color(0.1f, 0.22f, 0.16f), 0.45f);
            var dark = Mats.Lit(new Color(0.04f, 0.05f, 0.065f), 0.92f, 0.2f);
            var rope = Mats.Lit(new Color(0.55f, 0.47f, 0.34f), 0.2f, 0f, "fabric_linen");
            var leaf = Mats.Lit(new Color(0.16f, 0.24f, 0.13f), 0.3f);
            var pot = Mats.Lit(new Color(0.62f, 0.36f, 0.24f), 0.3f, 0f, "plaster_terracotta");
            var plan = new List<Vector2> { new Vector2(8.3f, 15f), new Vector2(9.3f, 12.8f), new Vector2(11.7f, 12.8f), new Vector2(12.7f, 15f), new Vector2(12.7f, 33.4f), new Vector2(8.3f, 33.4f) };
            k.Of(hull).AddPrism(plan, -2.0f, 0.2f, 2f);
            k.Box(wood, new Vector3(8.36f, 0.26f, 24.2f), new Vector3(0.14f, 0.14f, 18.4f));
            k.Box(wood, new Vector3(12.64f, 0.26f, 24.2f), new Vector3(0.14f, 0.14f, 18.4f));
            // Cabin with portholes on both sides, its roof, the wheelhouse.
            k.Box(paint, new Vector3(10.5f, 0.95f, 23f), new Vector3(3.2f, 1.5f, 13f));
            k.Box(wood, new Vector3(10.5f, 1.74f, 23f), new Vector3(3.4f, 0.08f, 13.2f));
            var rnd = new System.Random(48);
            for (float z = 17.3f; z < 29f; z += 1.6f)
                foreach (float x in new[] { 8.885f, 12.115f })
                    k.Box(rnd.NextDouble() < 0.65 ? warm : dark, new Vector3(x, 1.05f, z), new Vector3(0.03f, 0.42f, 0.7f));
            k.Box(paint, new Vector3(10.5f, 1.5f, 31.4f), new Vector3(2.6f, 2.6f, 2.6f));
            k.Box(wood, new Vector3(10.5f, 2.84f, 31.4f), new Vector3(2.9f, 0.08f, 2.9f));
            foreach (float x in new[] { 9.185f, 11.815f }) k.Box(warm, new Vector3(x, 2.05f, 31.4f), new Vector3(0.03f, 0.8f, 1.9f));
            k.Box(warm, new Vector3(10.5f, 2.05f, 30.085f), new Vector3(1.9f, 0.8f, 0.03f));
            // Pots on the roof, a coil of rope on the bow deck, mooring lines to the quay bollards.
            for (int i = 0; i < 4; i++)
            {
                var p = new Vector3(9.4f + (i % 2) * 2.2f, 1.78f, 18f + i * 2.8f);
                k.Box(pot, p + Vector3.up * 0.14f, new Vector3(0.36f, 0.28f, 0.36f));
                k.Of(leaf).AddSphere(p + Vector3.up * 0.48f, 0.26f, 8, 6);
            }
            k.Of(rope).AddTorus(new Vector3(10.5f, 0.24f, 14.6f), 0.28f, 0.05f, 16, 5, Quaternion.identity);
            foreach (var (from, to) in new[] { (new Vector3(7.43f, 0.0f, 14.1f), new Vector3(8.6f, 0.3f, 15.6f)), (new Vector3(7.43f, 0.0f, 25.05f), new Vector3(8.5f, 0.3f, 32.6f)) })
                k.Box(rope, (from + to) * 0.5f, new Vector3(0.035f, 0.035f, (to - from).magnitude), Quaternion.LookRotation((to - from).normalized, Vector3.up));
        }

        // ------------------------------------------------------------------ small pieces
        /// <summary>A turned stone balustrade from a to b on base height y: plinth, balusters, handrail, piers every ~5.8 m.</summary>
        static void WalkBalustrade(KitBatch k, Material stone, Material trim, Vector3 a, Vector3 b, float y)
        {
            a.y = 0f; b.y = 0f;
            var d = b - a;
            float len = d.magnitude;
            var dir = d / len;
            var rot = Quaternion.LookRotation(dir, Vector3.up);
            var mid = (a + b) * 0.5f;
            k.Box(stone, mid + Vector3.up * (y + 0.08f), new Vector3(0.46f, 0.16f, len), rot, 2.5f);
            k.Box(trim, mid + Vector3.up * (y + 0.86f), new Vector3(0.5f, 0.13f, len + 0.04f), rot, 2.5f);
            int n = Mathf.Max(2, Mathf.RoundToInt(len / 0.28f));
            for (int i = 0; i < n; i++)
            {
                var p = a + dir * ((i + 0.5f) * len / n);
                k.Box(stone, p + Vector3.up * (y + 0.475f), new Vector3(0.09f, 0.62f, 0.09f), rot);
                k.Box(stone, p + Vector3.up * (y + 0.36f), new Vector3(0.14f, 0.22f, 0.14f), rot);
            }
            int m = Mathf.Max(1, Mathf.RoundToInt(len / 5.8f));
            for (int i = 0; i <= m; i++)
            {
                var p = a + dir * (i * len / m);
                k.Box(stone, p + Vector3.up * (y + 0.5f), new Vector3(0.6f, 1.0f, 0.6f), rot, 2.5f);
                k.Box(trim, p + Vector3.up * (y + 1.04f), new Vector3(0.68f, 0.08f, 0.68f), rot, 2.5f);
            }
        }

        /// <summary>A Parisian cast-iron lamp standard: base, fluted column with collars, a six-sided lantern, cap, finial.</summary>
        static void WalkLamp(KitBatch k, Material iron, Material glass, Vector3 foot, float pole)
        {
            var b = k.Of(iron);
            b.AddCylinder(foot, 0.17f, 0.22f, 12);
            b.AddFrustum(foot + Vector3.up * 0.22f, 0.13f, 0.075f, 0.5f, 12);
            b.AddCylinder(foot + Vector3.up * 0.72f, 0.065f, pole - 0.72f, 10);
            foreach (float h in new[] { 0.72f, 1.5f, pole }) b.AddCylinder(foot + Vector3.up * (h - 0.03f), 0.095f, 0.06f, 12);
            var top = foot + Vector3.up * pole;
            b.AddFrustum(top, 0.07f, 0.13f, 0.12f, 10);
            k.Of(glass).AddFrustum(top + Vector3.up * 0.12f, 0.12f, 0.19f, 0.46f, 6);
            b.AddFrustum(top + Vector3.up * 0.58f, 0.23f, 0.04f, 0.22f, 6);
            b.AddSphere(top + Vector3.up * 0.84f, 0.045f, 8, 6);
        }

        /// <summary>A lantern on a bracket from a wall facing +X at <paramref name="wall"/>.</summary>
        static void WallLantern(KitBatch k, Material iron, Material glass, Vector3 wall)
        {
            k.Box(iron, wall + new Vector3(0.03f, 0f, 0f), new Vector3(0.06f, 0.36f, 0.2f));
            k.Box(iron, wall + new Vector3(0.3f, 0.12f, 0f), new Vector3(0.56f, 0.04f, 0.04f));
            var hang = wall + new Vector3(0.55f, -0.36f, 0f);
            k.Of(iron).AddFrustum(hang + Vector3.up * 0.4f, 0.17f, 0.03f, 0.16f, 6);
            k.Of(glass).AddFrustum(hang, 0.09f, 0.15f, 0.4f, 6);
            k.Of(iron).AddCylinder(hang - Vector3.up * 0.04f, 0.07f, 0.04f, 8);
        }

        static void Bollard(KitBatch k, Material iron, Vector3 foot)
        {
            k.Of(iron).AddCylinder(foot, 0.13f, 0.08f, 12);
            k.Of(iron).AddFrustum(foot + Vector3.up * 0.08f, 0.1f, 0.085f, 0.38f, 12);
            k.Of(iron).AddSphere(foot + Vector3.up * 0.5f, 0.1f, 10, 6, 1f, 0f, 90f);
            k.Of(iron).AddCylinder(foot + Vector3.up * 0.44f, 0.11f, 0.04f, 12);
        }

        /// <summary>A sagging chain between two posts (short links as small boxes along a catenary-ish curve).</summary>
        static void Chain(KitBatch k, Material iron, Vector3 a, Vector3 b, float sag)
        {
            const int links = 14;
            for (int i = 0; i < links; i++)
            {
                float t0 = i / (float)links, t1 = (i + 1) / (float)links;
                var p0 = Vector3.Lerp(a, b, t0) + Vector3.down * sag * 4f * t0 * (1f - t0);
                var p1 = Vector3.Lerp(a, b, t1) + Vector3.down * sag * 4f * t1 * (1f - t1);
                k.Box(iron, (p0 + p1) * 0.5f, new Vector3(0.025f, 0.025f, (p1 - p0).magnitude), Quaternion.LookRotation((p1 - p0).normalized, Vector3.up));
            }
        }
    }

    /// <summary>
    /// Rain that keeps over the player once they leave the festival street (which has its own rain), only while Paris is
    /// the world the player stands in: it must never fall around them in another country while Paris is a pocket.
    /// </summary>
    public class RouteRain : MonoBehaviour
    {
        ParticleSystem m_Ps;
        Cell m_Cell;

        void Start()
        {
            m_Ps = GetComponent<ParticleSystem>();
            m_Cell = GetComponentInParent<Cell>();
        }

        void LateUpdate()
        {
            if (m_Ps == null || m_Cell == null) return;
            bool on = false;
            if (m_Cell.Mode == CellMode.Active && Game.Rig != null)
            {
                var feet = Game.Rig.Feet;
                var local = m_Cell.transform.InverseTransformPoint(feet);
                on = !(local.x < 4.15f && local.z < 13f);
                if (on) transform.position = feet + Vector3.up * 7f;
            }
            var em = m_Ps.emission;
            if (em.enabled != on) em.enabled = on;
        }
    }
}
