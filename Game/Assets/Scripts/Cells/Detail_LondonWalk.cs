using UnityEngine;

namespace TwelfthStamp
{
    // The way to the landmark (open world: after the seal is verified the stamp waits on the far embankment, between the
    // palace and Westminster Bridge, and the player runs to it with the Passenger behind). The promenade runs on north to
    // the bridge (Detail_London.cs extends it); stone steps climb against the embankment wall to a landing over the
    // bridge's parapet; the player crosses on the bridge deck (the vista's, walkable) and goes down a second flight at the
    // far end onto the embankment, where railings keep everyone off the river's edge.
    public partial class LondonCell
    {
        /// <summary>
        /// Heights: promenade 0, bridge deck 5.4 (its parapets 1 m higher, z 104..104.4 and 119.6..120), far embankment 1.0.
        /// The near steps rise along +Z at x -4.2..-1.2 from z 89.9 to a landing at 6.55 (z 102.8..104.8, just over the
        /// parapet), then drop 7 steps onto the deck. The far steps leave the deck's end (x 92) eastward, z 110..114, and
        /// reach the embankment at x 100.4. The stamp hangs at (95, 2.5, 98).
        /// </summary>
        void BuildBridgeWalk(Transform g, LonPalette p)
        {
            var k = new KitBatch();
            var tread = p.Granite;
            var wall = p.Stone;

            // ---- The near steps: three flights and two landings against the embankment wall.
            const float x0 = -4.2f, x1 = -1.2f, sx = (x0 + x1) / 2f, sw = x1 - x0, t = 0.3f, zA = 89.9f, top = 6.55f;
            const float r = top / 38f;
            float zB = zA + 12f * t + 1.2f, zC = zB + 12f * t + 1.2f, zTop = zC + 11f * t, zDown = 104.8f;
            void Step(float z0, float y0, float y1)
            {
                k.Box(tread, new Vector3(sx, (y0 + y1) / 2f, z0 + t / 2f), new Vector3(sw, y1 - y0, t), 1.2f);
                k.Box(wall, new Vector3(x1 + 0.125f, (y0 + y1 + 1f) / 2f, z0 + t / 2f), new Vector3(0.25f, y1 + 1f - y0, t), 2f);
                k.Box(wall, new Vector3(x0 - 0.1f, (y0 + y1 + 1f) / 2f, z0 + t / 2f), new Vector3(0.2f, y1 + 1f - y0, t), 2f);
            }
            void Landing(float z0, float z1, float y)
            {
                k.Box(tread, new Vector3(sx, y / 2f, (z0 + z1) / 2f), new Vector3(sw, y, z1 - z0), 1.2f);
                k.Box(wall, new Vector3(x1 + 0.125f, (y + 1f) / 2f, (z0 + z1) / 2f), new Vector3(0.25f, y + 1f, z1 - z0), 2f);
                k.Box(wall, new Vector3(x0 - 0.1f, (y + 1f) / 2f, (z0 + z1) / 2f), new Vector3(0.2f, y + 1f, z1 - z0), 2f);
            }
            for (int i = 1; i <= 12; i++) Step(zA + (i - 1) * t, 0f, i * r);
            Landing(zA + 12f * t, zB, 13f * r);
            for (int i = 14; i <= 25; i++) Step(zB + (i - 14) * t, 0f, i * r);
            Landing(zB + 12f * t, zC, 26f * r);
            for (int i = 27; i <= 37; i++) Step(zC + (i - 27) * t, 0f, i * r);
            // The landing over the parapet: masonry up to it south of the bridge, a slab across the parapet's top.
            k.Box(tread, new Vector3(sx, 3.21f, (zTop + 104f) / 2f), new Vector3(sw, 6.42f, 104f - zTop), 1.2f);
            k.Box(tread, new Vector3(sx, 6.485f, (zTop + zDown) / 2f), new Vector3(sw, 0.13f, zDown - zTop), 1.2f);
            foreach (var (wx, ww) in new[] { (x1 + 0.125f, 0.25f), (x0 - 0.1f, 0.2f) })
            {
                k.Box(wall, new Vector3(wx, 3.775f, (zTop + 104f) / 2f), new Vector3(ww, 7.55f, 104f - zTop), 2f);
                k.Box(wall, new Vector3(wx, 6.475f, (104f + zDown) / 2f), new Vector3(ww, 2.15f, zDown - 104f), 2f);
            }
            // Down onto the bridge pavement.
            const float r2 = 1.15f / 7f;
            for (int j = 1; j <= 6; j++)
            {
                float zj = zDown + (j - 1) * t, y1 = top - j * r2;
                k.Box(tread, new Vector3(sx, (5.4f + y1) / 2f, zj + t / 2f), new Vector3(sw, y1 - 5.4f, t), 1.2f);
                k.Box(wall, new Vector3(x1 + 0.125f, (5.4f + y1 + 1f) / 2f, zj + t / 2f), new Vector3(0.25f, y1 + 1f - 5.4f, t), 2f);
            }
            // Lanterns at the foot and on the landing, and one real light over the flights.
            Lantern(k, p, new Vector3(x1 + 0.125f, r + 1f + 0.36f, zA + 0.15f), 1.2f); // sits on the wall (its cone reaches 0.36 below the centre)
            Lantern(k, p, new Vector3(x1 + 0.125f, top + 1f + 0.36f, zTop + 0.2f), 1.2f);
            Lantern(k, p, new Vector3(x0 - 0.1f, top + 1f + 0.36f, zTop + 0.2f), 1.2f);
            AddPointLight(g, new Vector3(sx, 4.6f, 96.5f), new Color(1f, 0.8f, 0.55f), 1.6f, 9f);
            // The promenade ends at the bridge: a balustrade across the walk below the arch.
            SpearRailing(k, p.Iron, new Vector3(-0.95f, 0f, 103.35f), new Vector3(4.45f, 0f, 103.35f), 1.1f);

            // ---- On the bridge: the road closed to the west, balustrades where the vista's parapets stop over the far abutment.
            SpearRailing(k, p.Iron, new Vector3(-4.45f, 5.4f, 104.45f), new Vector3(-4.45f, 5.4f, 119.55f), 1.2f);
            foreach (var bz in new[] { 104.125f, 119.875f })
            {
                k.Box(p.Parapet, new Vector3(88.5f, 5.9f, bz), new Vector3(7f, 1f, 0.25f), 2.5f);
                Col(g, "AbutmentRail", new Vector3(88.5f, 6.0f, bz), new Vector3(7f, 1.2f, 0.25f), Quaternion.identity, Layers.Default);
            }
            foreach (var (z0, z1) in new[] { (104.25f, 110f), (114f, 119.75f) })
            {
                k.Box(p.Parapet, new Vector3(91.9f, 5.9f, (z0 + z1) / 2f), new Vector3(0.2f, 1f, z1 - z0), 2.5f);
                Col(g, "DeckEndRail", new Vector3(91.9f, 6.0f, (z0 + z1) / 2f), new Vector3(0.2f, 1.2f, z1 - z0), Quaternion.identity, Layers.Default);
            }

            // ---- The far steps: two flights east off the deck's end down to the embankment.
            const float fz0 = 110f, fz1 = 114f, fzc = (fz0 + fz1) / 2f, fw = fz1 - fz0, r3 = 4.4f / 26f;
            void FarStep(float xs, float y1)
            {
                k.Box(tread, new Vector3(xs + t / 2f, (1f + y1) / 2f, fzc), new Vector3(t, y1 - 1f, fw), 1.2f);
                foreach (var wz in new[] { fz0 - 0.125f, fz1 + 0.125f })
                    k.Box(wall, new Vector3(xs + t / 2f, (1f + y1 + 1f) / 2f, wz), new Vector3(t, y1, 0.25f), 2f);
            }
            for (int i = 1; i <= 12; i++) FarStep(92f + (i - 1) * t, 5.4f - i * r3);
            k.Box(tread, new Vector3(96.2f, (1f + 3.2f) / 2f, fzc), new Vector3(1.2f, 2.2f, fw), 1.2f);
            foreach (var wz in new[] { fz0 - 0.125f, fz1 + 0.125f }) k.Box(wall, new Vector3(96.2f, 2.6f, wz), new Vector3(1.2f, 3.2f, 0.25f), 2f);
            for (int i = 14; i <= 25; i++) FarStep(96.8f + (i - 14) * t, 5.4f - i * r3);
            Lantern(k, p, new Vector3(100.25f, 5.4f - 25f * r3 + 1f + 0.36f, fz0 - 0.125f), 1.2f);
            Lantern(k, p, new Vector3(100.25f, 5.4f - 25f * r3 + 1f + 0.36f, fz1 + 0.125f), 1.2f);
            AddPointLight(g, new Vector3(97f, 4.2f, 107.5f), new Color(1f, 0.8f, 0.55f), 1.5f, 10f);
            // Railings on the far embankment: along the river's edge and across it south of the stamp (the palace beyond).
            // Plain bars: they're seen from ten metres or more, and spears would double their triangles.
            SpearRailing(k, p.Iron, new Vector3(79.35f, 1f, 86f), new Vector3(79.35f, 1f, 120.2f), 1.1f, false);
            SpearRailing(k, p.Iron, new Vector3(79.35f, 1f, 85.95f), new Vector3(107.2f, 1f, 85.95f), 1.1f, false);
            k.Bake(g, "BridgeWalk");

            // ---- Colliders. Floors (Floor layer, stone): the promenade's northern run, the flights as ramps, the landings.
            Col(g, "PromenadeNorth", new Vector3(0f, -0.15f, 57.15f), new Vector3(9f, 0.3f, 92.5f), Quaternion.identity, Layers.Floor, "stone");
            float alpha = Mathf.Atan2(r, t) * Mathf.Rad2Deg;
            void Flight(float z0, float y0, int treads)
            {
                // A thick slab whose top runs through the treads' midpoints (from z0, half a riser above y0).
                float run = treads * t, len = Mathf.Sqrt(run * run + (treads * r) * (treads * r));
                var up = Quaternion.Euler(-alpha, 0f, 0f) * Vector3.up;
                var mid = new Vector3(sx, y0 + r / 2f + treads * r / 2f, z0 + run / 2f);
                Col(g, "Steps", mid - up * 1f, new Vector3(sw, 2f, len), Quaternion.Euler(-alpha, 0f, 0f), Layers.Floor, "stone");
            }
            Flight(zA, 0f, 12);
            Col(g, "Landing", new Vector3(sx, 13f * r - 0.15f, zA + 12f * t + 0.6f), new Vector3(sw, 0.3f, 1.2f), Quaternion.identity, Layers.Floor, "stone");
            Flight(zB, 13f * r, 12);
            Col(g, "Landing", new Vector3(sx, 26f * r - 0.15f, zB + 12f * t + 0.6f), new Vector3(sw, 0.3f, 1.2f), Quaternion.identity, Layers.Floor, "stone");
            Flight(zC, 26f * r, 11);
            Col(g, "Landing", new Vector3(sx, top - 0.065f, (zTop + zDown) / 2f), new Vector3(sw, 0.13f, zDown - zTop), Quaternion.identity, Layers.Floor, "stone");
            {
                float beta = Mathf.Atan2(r2, t) * Mathf.Rad2Deg, run = 6f * t, len = Mathf.Sqrt(run * run + 1f * 1f);
                var q = Quaternion.Euler(beta, 0f, 0f);
                var mid = new Vector3(sx, top - r2 / 2f - 3f * r2, zDown + run / 2f);
                Col(g, "Steps", mid - q * Vector3.up * 0.25f, new Vector3(sw, 0.5f, len), q, Layers.Floor, "stone");
            }
            {
                float beta = Mathf.Atan2(r3, t) * Mathf.Rad2Deg, run = 12f * t, len = Mathf.Sqrt(run * run + (12f * r3) * (12f * r3));
                var q = Quaternion.Euler(beta, 90f, 0f);
                foreach (var (xs, ys) in new[] { (92f, 5.4f), (96.8f, 3.2f) })
                {
                    var mid = new Vector3(xs + run / 2f, ys - r3 / 2f - 6f * r3, fzc);
                    Col(g, "Steps", mid - q * Vector3.up * 1f, new Vector3(fw, 2f, len), q, Layers.Floor, "stone");
                }
                Col(g, "Landing", new Vector3(96.2f, 3.05f, fzc), new Vector3(1.2f, 0.3f, fw), Quaternion.identity, Layers.Floor, "stone");
            }
            // Walls nobody climbs (Default layer, tall enough that a VR teleport arc cannot clear them).
            Col(g, "StepsSide", new Vector3(x1 + 0.125f, 4f, (zA + zDown + 6f * t) / 2f), new Vector3(0.25f, 8f, zDown + 6f * t - zA), Quaternion.identity, Layers.Default);
            Col(g, "PromenadeEnd", new Vector3(1.825f, 2.5f, 103.35f), new Vector3(5.55f, 5f, 0.3f), Quaternion.identity, Layers.Default);
            Col(g, "RiverSide", new Vector3(4.45f, 2.5f, 48.35f), new Vector3(0.3f, 5f, 109.7f), Quaternion.identity, Layers.Default);
            foreach (var wz in new[] { fz0 - 0.125f, fz1 + 0.125f })
                Col(g, "FarStepsSide", new Vector3(96.2f, 4.5f, wz), new Vector3(8.4f, 7f, 0.25f), Quaternion.identity, Layers.Default);
            Col(g, "FarRiverRail", new Vector3(79.35f, 2.5f, 103.1f), new Vector3(0.3f, 3f, 34.2f), Quaternion.identity, Layers.Default);
            Col(g, "FarSouthRail", new Vector3(93.27f, 2.5f, 85.95f), new Vector3(27.85f, 3f, 0.3f), Quaternion.identity, Layers.Default);
            // A catcher at promenade height over the river and the land behind the wall (invisible): the Passenger's floor
            // probe finds it when it comes from behind a wall or cuts across the water, instead of dropping to the river.
            Col(g, "Catcher", new Vector3(26.75f, -0.05f, 54.5f), new Vector3(101.5f, 0.04f, 133f), Quaternion.identity, Layers.Default);
            AddBounds(new Vector3(51.35f, 0f, 56.85f), new Vector3(111.3f, 9f, 126.7f), 9f);
            Landmarks.MarkReachable(this);
        }

        static GameObject Col(Transform g, string name, Vector3 center, Vector3 size, Quaternion rot, int layer, string surface = null)
        {
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(g, false);
            go.transform.localPosition = center;
            go.transform.localRotation = rot;
            go.AddComponent<BoxCollider>().size = size;
            if (surface != null) go.AddComponent<Surface>().Kind = surface;
            return go;
        }
    }
}
