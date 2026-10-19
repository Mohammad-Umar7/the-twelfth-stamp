using UnityEngine;

namespace TwelfthStamp
{
    // Riyadh's open-world route (playtest: open the countries out to their landmarks). Straight behind the honest arch a
    // gatehouse breaks the low north wall; its painted doors stay shut until the honest arch is walked through, then swing
    // open onto the city's ground (the vista's, y -0.35) and the long run up the tower's sight line to the stamp waiting in
    // the forecourt in front of it (Landmarks.ArchTower).
    public partial class RiyadhCell
    {
        /// <summary>The arch tower's root (Landmarks.ArchTower); the stamp spot is on its sight line, 45 m in front of it.</summary>
        public static readonly Vector3 TowerPos = new Vector3(20f, -2f, 210f);
        /// <summary>The north gatehouse doors: shut until the honest arch is walked through.</summary>
        public RouteGate Gate;

        /// <summary>
        /// The gatehouse behind the honest arch (centre = its plaza face at ground level): plastered piers and lintel with a
        /// gypsum surround, a triangle frieze and stepped merlons, a lamp over the opening, a stone threshold and a step down
        /// to the city's ground; two painted leaves on their own hinges, and the collider that keeps them shut.
        /// </summary>
        void NorthGate(Transform g, KitBatch k, NajdiMats m, Vector3 centre)
        {
            var f = new Front(centre, Vector3.back); // a runs toward -x
            const float hw = 1.6f, open = 1f, top = 4.2f, lintel = 2.75f, depth = 1.2f;
            foreach (float s in new[] { -1f, 1f })
            {
                f.Box(k, m.MudDark, s * open, s * hw, -0.35f, 0.55f, -depth, 0.06f, 2f);
                f.Box(k, m.Mud, s * open, s * hw, 0.55f, top, -depth, 0f, 2.2f);
                f.Box(k, m.Gypsum, s * open, s * (open + 0.16f), 0f, lintel + 0.16f, 0f, 0.06f, 2f);
                f.Box(k, m.Gypsum, s * open, s * (open + 0.12f), 0f, lintel + 0.12f, -depth - 0.05f, -depth, 2f);
            }
            f.Box(k, m.Mud, -open, open, lintel, top, -depth, 0f, 2.2f);
            f.Box(k, m.Gypsum, -open - 0.16f, open + 0.16f, lintel, lintel + 0.16f, 0f, 0.06f, 2f);
            f.Box(k, m.Gypsum, -hw, hw, top - 0.72f, top - 0.6f, 0f, 0.05f, 2f);
            for (int i = 0; i < 5; i++) TriFace(k.Of(i == 2 ? m.GlowTri : m.Dark), f.P(-0.9f + i * 0.45f, 3.08f, 0.006f), f.Out, 0.2f, 0.24f);
            for (int i = 0; i < 5; i++) Merlon(k, m.Mud, f.P(-1.3f + i * 0.65f, top, -0.6f), f.Rot, 0.42f, 1.1f, 0.17f);
            f.Box(k, m.Trim, -open, open, -0.4f, -0.12f, -depth - 0.05f, 0.1f, 1.5f);
            f.Box(k, m.Trim, -open - 0.1f, open + 0.1f, -0.45f, -0.24f, -depth - 0.5f, -depth - 0.05f, 1.5f);
            Rod(k, m.Iron, f.P(0f, 3.25f, 0f), f.P(0f, 3.25f, 0.26f), 0.025f);
            Lantern(k, m, f.P(0f, 3.05f, 0.26f), 0.75f, 3.25f);
            AddPointLight(g, f.P(0f, 2.85f, 0.5f), new Color(1f, 0.76f, 0.5f), 0.9f, 4f);

            var root = new GameObject("NorthGate").transform;
            root.SetParent(g, false);
            Gate = root.gameObject.AddComponent<RouteGate>();
            Gate.LeafA = NajdiLeaf(root, m, "GateLeafW", f.P(open, 0f, -0.6f), 1f, 0.99f, -0.1f, 2.83f, 1);
            Gate.LeafB = NajdiLeaf(root, m, "GateLeafE", f.P(-open, 0f, -0.6f), -1f, 0.99f, -0.1f, 2.83f, 3);
            Gate.OpenA = -95f;
            Gate.OpenB = 95f;
            var shut = new GameObject("GateShut");
            shut.transform.SetParent(root, false);
            shut.transform.localPosition = f.P(0f, 1.3f, -0.6f);
            var col = shut.AddComponent<BoxCollider>();
            col.size = new Vector3(2f, 2.8f, 0.25f);
            Gate.Blocker = col;
            Gate.SetOpen(false, true);
        }

        /// <summary>One painted Najdi door leaf on a pivot at its hinge, reaching dir (+1 / -1) along x, painted on its plaza side.</summary>
        static Transform NajdiLeaf(Transform parent, NajdiMats m, string name, Vector3 hinge, float dir, float width, float bottom, float height, int colour)
        {
            var p = new GameObject(name).transform;
            p.SetParent(parent, false);
            p.localPosition = hinge;
            var k = new KitBatch();
            k.Box(m.Door, new Vector3(dir * width * 0.5f, bottom + height * 0.5f, 0f), new Vector3(width, height, 0.07f), 1.2f);
            var face = Vector3.back;
            foreach (float at in new[] { 0.22f, 0.5f, 0.78f })
            {
                float vb = bottom + height * at;
                k.Box(m.PaintWhite, new Vector3(dir * width * 0.5f, vb - 0.004f, -0.037f), new Vector3(width - 0.08f, 0.016f, 0.004f));
                k.Box(m.PaintWhite, new Vector3(dir * width * 0.5f, vb + 0.178f, -0.037f), new Vector3(width - 0.08f, 0.016f, 0.004f));
                const int n = 6;
                float step = (width - 0.1f) / n;
                for (int i = 0; i < n; i++)
                {
                    float x = dir * (0.05f + (i + 0.5f) * step);
                    TriFace(k.Of(m.Paint[(colour + i) % m.Paint.Length]), new Vector3(x, vb + 0.012f, -0.038f), face, step * 0.9f, 0.14f);
                    k.Box(m.Iron, new Vector3(x, vb - 0.025f, -0.042f), new Vector3(0.024f, 0.024f, 0.014f));
                }
                colour++;
            }
            var ring = Quaternion.FromToRotation(Vector3.up, face);
            var knob = new Vector3(dir * (width - 0.14f), bottom + height * 0.42f, -0.035f);
            k.Of(m.Brass).AddCylinder(knob, 0.02f, 0.02f, 8, 1f, ring);
            k.Of(m.Brass).AddTorus(knob + face * 0.025f + Vector3.down * 0.05f, 0.055f, 0.008f, 12, 4, ring);
            k.Bake(p, name);
            return p;
        }

        /// <summary>
        /// Colliders for the open plaza (the cell's bounds are now one box round the plaza and the run to the stamp): the
        /// paved apron underfoot one step down round the plaza (so no gap between the plaza's edge and a wall can trap anyone),
        /// the gatehouse threshold and step, the north wall either side of the gatehouse, and the east parapet.
        /// </summary>
        void RouteColliders(Transform g)
        {
            var root = new GameObject("RouteColliders").transform;
            root.SetParent(g, false);
            void Col(string name, Vector3 centre, Vector3 size, bool floor)
            {
                var go = new GameObject(name) { layer = floor ? Layers.Floor : Layers.Default };
                go.transform.SetParent(root, false);
                go.transform.localPosition = centre;
                go.AddComponent<BoxCollider>().size = size;
                if (floor) go.AddComponent<Surface>().Kind = "stone";
            }
            Col("Apron", new Vector3(0.2f, -0.26f, 3f), new Vector3(19.6f, 0.28f, 19.4f), true);
            Col("GateThreshold", new Vector3(4f, -0.26f, 13.025f), new Vector3(2f, 0.28f, 1.35f), true);
            Col("GateStep", new Vector3(4f, -0.345f, 13.925f), new Vector3(2.2f, 0.21f, 0.45f), true);
            Col("NorthWallW", new Vector3(-3.275f, 1.6f, 12.9f), new Vector3(11.45f, 3.9f, 0.6f), false);
            Col("NorthWallE", new Vector3(7.475f, 1.6f, 12.9f), new Vector3(3.85f, 3.9f, 0.6f), false);
            Col("GatePierW", new Vector3(2.7f, 1.9f, 13.05f), new Vector3(0.6f, 4.5f, 1.2f), false);
            Col("GatePierE", new Vector3(5.3f, 1.9f, 13.05f), new Vector3(0.6f, 4.5f, 1.2f), false);
            Col("EastParapet", new Vector3(8.8f, 1.7f, 3f), new Vector3(0.42f, 3.6f, 19.2f), false);
        }

        /// <summary>
        /// Whether a person can walk from beyond the north gatehouse to the stamp spot in front of the tower (the city's
        /// ground and whatever stands on it are the vista's). The encounter re-checks this on entry so the stamp is never
        /// offered where the way to it is shut.
        /// </summary>
        public bool RouteOpen()
        {
            var ax = new Vector3(-TowerPos.x, 0f, -TowerPos.z).normalized;
            var stamp = TowerPos + ax * 45f;
            stamp.y = -0.35f;
            return RouteGate.PathClear(transform, new Vector3(4f, -0.12f, 13.45f), new Vector3(4f, -0.35f, 15f), stamp);
        }
    }
}
