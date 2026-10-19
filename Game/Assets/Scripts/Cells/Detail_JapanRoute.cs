using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    // Japan's open-world route (playtest: open the countries out to their landmarks). Once the lantern puzzle is solved the
    // street's end gate swings open onto a walled lane on the pagoda's sight line: stone slabs between raked gravel, garden
    // lanterns, a pine either side of the gate and two maples, down five steps from street level (y 0) to the precinct's
    // approach level (y -1), ending at the front of the precinct's outer gate, behind which the stamp waits in front of the
    // pagoda (the vista's ground and gate are built in Landmarks.cs / Vista.cs).
    public partial class JapanCell
    {
        /// <summary>The pagoda's root (Landmarks.Pagoda); the route and the stamp spot are laid out on its sight line.</summary>
        public static readonly Vector3 PagodaPos = new Vector3(-10f, -1f, 120f);
        /// <summary>The street's end gate: shut until the lantern puzzle is solved.</summary>
        public RouteGate Gate;

        const float LaneHalf = 3.8f;                          // lane wall centre lines either side of the slabs
        const float StepsFrom = 5.5f, StepsTo = 7.5f;         // the flight of steps, metres along the lane
        const float LowY = -0.97f;                            // the lower lane, a touch above the vista ground (-1)
        static readonly Vector3 s_LaneStart = new Vector3(0f, 0f, 13.45f);

        /// <summary>Horizontal unit vector from the pagoda toward the player's spot (the vista's ToViewer).</summary>
        static Vector3 ToStreet => new Vector3(-PagodaPos.x, 0f, -PagodaPos.z).normalized;
        /// <summary>The front of the precinct's outer gate (the vista's A(76.5, 0)), where the lane ends.</summary>
        static Vector3 LaneEnd { get { var p = PagodaPos + ToStreet * 76.5f; p.y = -1f; return p; } }
        static Vector3 LaneDir { get { var d = LaneEnd - s_LaneStart; d.y = 0f; return d.normalized; } }
        static Vector3 LaneRight { get { var u = LaneDir; return new Vector3(u.z, 0f, -u.x); } }
        static float LaneLength { get { var d = LaneEnd - s_LaneStart; d.y = 0f; return d.magnitude; } }
        static Quaternion LaneRot => Quaternion.LookRotation(LaneDir, Vector3.up);
        /// <summary>A point on the lane: t metres along it, s across it (+ to the right), at height y.</summary>
        static Vector3 Lane(float t, float s, float y) => s_LaneStart + LaneDir * t + LaneRight * s + Vector3.up * y;

        /// <summary>
        /// The lane and its colliders, the swinging end-gate leaves, and the street's own side limits (the cell's bounds are
        /// now one big box round the street, the lane and the precinct out to the stamp, so the street keeps its old edges
        /// with walls of its own). Visuals bake in their own batch, lit by the lane's two lights and the failing lamp.
        /// </summary>
        void BuildRoute(StreetCtx c)
        {
            var m = c.M;
            var root = new GameObject("Route").transform;
            root.SetParent(c.Root, false);
            var k = new KitBatch();
            var slabs = Mats.Lit(new Color(0.64f, 0.64f, 0.68f), 0.72f, 0f, "granite_pavers");
            var gravel = Mats.Lit(new Color(0.71f, 0.86f, 1f), 0.3f, 0f, "sand_ripples");
            var maple = Mats.Lit(new Color(0.75f, 0.2f, 0.12f), 0.25f, 0f, "fabric_linen");
            float len = LaneLength;
            var rot = LaneRot;
            void Strip(Material mat, float t0, float t1, float s0, float s1, float top, float depth, float uv)
                => k.Box(mat, Lane((t0 + t1) * 0.5f, (s0 + s1) * 0.5f, top - depth * 0.5f), new Vector3(Mathf.Abs(s1 - s0), depth, Mathf.Abs(t1 - t0)), rot, uv);

            // Slabs down the middle, raked gravel either side behind stone kerbs; street level, five steps, the approach level.
            Strip(slabs, -0.3f, StepsFrom, -1.6f, 1.6f, 0f, 0.3f, 2.4f);
            Strip(slabs, StepsTo, len, -1.6f, 1.6f, LowY, 0.3f, 2.4f);
            foreach (float s in new[] { -1f, 1f })
            {
                Strip(gravel, -0.3f, StepsFrom, s * 1.76f, s * 3.58f, -0.02f, 0.3f, 2f);
                Strip(gravel, StepsTo, len, s * 1.76f, s * 3.58f, LowY - 0.02f, 0.3f, 2f);
                Strip(m.Stone, -0.3f, StepsFrom, s * 1.6f, s * 1.76f, 0.03f, 0.3f, 1f);
                Strip(m.Stone, StepsTo, len, s * 1.6f, s * 1.76f, LowY + 0.03f, 0.3f, 1f);
            }
            for (int i = 0; i < 4; i++)
            {
                float t0 = StepsFrom + i * 0.5f, top = -0.2f * (i + 1);
                Strip(m.Stone, t0, t0 + 0.5f, -3.58f, 3.58f, top, top - LowY + 0.3f, 1.2f);
            }
            // Low earthen walls with tiled copings, stepping down with the stairs (their colliders are tall and invisible).
            foreach (float s in new[] { -1f, 1f })
            {
                Wall(k, m, Lane(-0.25f, s * LaneHalf, 0f), Lane(StepsFrom, s * LaneHalf, 0f), 0.45f, 1.15f);
                Wall(k, m, Lane(StepsFrom, s * LaneHalf, LowY - 0.03f), Lane(len, s * LaneHalf, LowY - 0.03f), 0.45f, 1.2f);
            }
            // Garden lanterns in the gravel, a pine either side of the gate, two maples halfway down.
            foreach (float t in new[] { 10.5f, 16.5f, 22.5f, 28.4f })
                foreach (float s in new[] { -2.66f, 2.66f }) GardenLantern(k, m, Lane(t, s, LowY - 0.02f));
            Pine(k, m, Lane(1.7f, -2.7f, -0.02f), 0.5f, 31);
            Pine(k, m, Lane(1.7f, 2.7f, -0.02f), 0.46f, 37);
            Maple(k, m, maple, Lane(19.5f, -2.95f, LowY - 0.02f), 3.6f, 41);
            Maple(k, m, maple, Lane(19.5f, 2.95f, LowY - 0.02f), 3.3f, 43);
            k.Bake(root, "Route");
            foreach (float t in new[] { 13.5f, 25.5f })
                AddPointLight(c.Root, Lane(t, 0f, LowY + 1.7f), new Color(1f, 0.66f, 0.38f), 1.25f, 6f);

            // Colliders: lane floors (street level, a ramp under the steps, the approach level), tall invisible walls along
            // both sides of the lane, the street's side limits, the gate's posts and walls, and the gate itself.
            Collider Col(string name, Vector3 centre, Vector3 size, Quaternion r, int layer, bool floor)
            {
                var go = new GameObject(name) { layer = layer };
                go.transform.SetParent(root, false);
                go.transform.localPosition = centre;
                go.transform.localRotation = r;
                var col = go.AddComponent<BoxCollider>();
                col.size = size;
                if (floor) go.AddComponent<Surface>().Kind = "stone";
                return col;
            }
            Col("LaneFloorUpper", Lane(2.5f, 0f, -0.15f), new Vector3(7.2f, 0.3f, 6.2f), rot, Layers.Floor, true);
            float run = StepsTo - StepsFrom, drop = -LowY, slope = Mathf.Sqrt(run * run + drop * drop);
            var n = (Vector3.up * run + LaneDir * drop) / slope; // the ramp's surface normal
            Col("LaneSteps", Lane((StepsFrom + StepsTo) * 0.5f, 0f, LowY * 0.5f) - n * 0.15f, new Vector3(7.2f, 0.3f, slope),
                rot * Quaternion.Euler(Mathf.Atan2(drop, run) * Mathf.Rad2Deg, 0f, 0f), Layers.Floor, true);
            Col("LaneFloorLower", Lane((7.4f + len + 0.8f) * 0.5f, 0f, LowY - 0.15f), new Vector3(7.2f, 0.3f, len + 0.8f - 7.4f), rot, Layers.Floor, true);
            foreach (float s in new[] { -1f, 1f })
            {
                Col("LaneWall", Lane((len - 0.1f) * 0.5f, s * (LaneHalf + 0.02f), LowY + 3f), new Vector3(0.5f, 6f, len + 0.7f), rot, Layers.Default, false);
                Col("StreetEdge", new Vector3(s * 2.95f, 3f, 3.575f), new Vector3(0.3f, 6f, 19.15f), Quaternion.identity, Layers.Default, false);
                Col("GateSide", new Vector3(s * 2.95f, 3f, 13.3f), new Vector3(1.6f, 6f, 0.3f), Quaternion.identity, Layers.Default, false);
            }

            // The gate's two leaves, hinged on the posts' inner faces, swing out onto the lane.
            Transform Leaf(string name, Vector3 hinge, float dir)
            {
                var p = new GameObject(name).transform;
                p.SetParent(root, false);
                p.localPosition = hinge;
                var lk = new KitBatch();
                for (int i = 0; i < 4; i++) lk.Box(m.Weathered, new Vector3(dir * (0.27f + i * 0.54f), 1.38f, 0f), new Vector3(0.53f, 2.56f, 0.07f), 1.5f);
                foreach (float y in new[] { 0.45f, 2.3f }) lk.Box(m.Weathered, new Vector3(dir * 1.0775f, y, -0.05f), new Vector3(2.155f, 0.12f, 0.06f));
                lk.Box(m.Black, new Vector3(dir * 2.143f, 1.38f, -0.045f), new Vector3(0.025f, 2.56f, 0.075f));
                for (int r = 0; r < 2; r++)
                    for (int i = 0; i < 4; i++) lk.Of(m.Black).AddSphere(new Vector3(dir * (0.3f + i * 0.5f), 0.45f + r * 1.85f, -0.082f), 0.022f, 6, 4);
                lk.Bake(p, name);
                return p;
            }
            Gate = root.gameObject.AddComponent<RouteGate>();
            Gate.LeafA = Leaf("GateLeafW", new Vector3(-2.155f, 0f, 13.34f), 1f);
            Gate.LeafB = Leaf("GateLeafE", new Vector3(2.155f, 0f, 13.34f), -1f);
            Gate.OpenA = -95f;
            Gate.OpenB = 95f;
            Gate.Blocker = Col("GateShut", new Vector3(0f, 3f, 13.3f), new Vector3(4.3f, 6f, 0.3f), Quaternion.identity, Layers.Default, false);
            Gate.SetOpen(false, true);
        }

        /// <summary>
        /// Whether a person can walk from beyond the end gate to the stamp spot: down the lane, through the precinct's outer
        /// gate (the vista's) and up the lantern approach. The encounter re-checks this on entry (with the cell in place and
        /// physics current) so the stamp is never offered where the way to it is shut.
        /// </summary>
        public bool RouteOpen()
        {
            var o = PagodaPos;
            var ax = ToStreet;
            return RouteGate.PathClear(transform, Lane(5.2f, 0f, 0f), Lane(7.8f, 0f, LowY), Lane(LaneLength - 0.5f, 0f, LowY), o + ax * 70f, o + ax * 16f);
        }

        /// <summary>A square stone garden lantern: base, post, a lit firebox between corner posts, a pyramid roof, a finial.</summary>
        static void GardenLantern(KitBatch k, JapanMats m, Vector3 at)
        {
            k.Box(m.Stone, at + Vector3.up * 0.12f, new Vector3(0.56f, 0.24f, 0.56f), 1.2f);
            k.Box(m.Stone, at + Vector3.up * 0.62f, new Vector3(0.22f, 0.76f, 0.22f), 1.2f);
            k.Box(m.Stone, at + Vector3.up * 1.04f, new Vector3(0.5f, 0.08f, 0.5f), 1.2f);
            k.Box(m.LanternCream, at + Vector3.up * 1.25f, new Vector3(0.3f, 0.32f, 0.3f));
            foreach (float x in new[] { -0.16f, 0.16f })
                foreach (float z in new[] { -0.16f, 0.16f }) k.Box(m.Stone, at + new Vector3(x, 1.25f, z), new Vector3(0.06f, 0.34f, 0.06f), 1.2f);
            k.Of(m.Stone).AddFrustum(at + Vector3.up * 1.42f, 0.42f, 0.05f, 0.24f, 4, true, false, 1.2f, Quaternion.Euler(0f, 45f, 0f));
            k.Of(m.Stone).AddSphere(at + Vector3.up * 1.7f, 0.06f, 8, 5);
        }

        /// <summary>A small maple: a leaning trunk and a crown of red leaf clumps.</summary>
        static void Maple(KitBatch k, JapanMats m, Material leaves, Vector3 at, float h, int seed)
        {
            var rnd = new System.Random(seed);
            k.Of(m.Weathered).AddTube(new List<Vector3> { at, at + new Vector3(0.05f, h * 0.3f, 0.03f), at + new Vector3(-0.08f, h * 0.55f, 0.1f) }, 0.07f, 7);
            for (int i = 0; i < 5; i++)
            {
                var o = new Vector3(((float)rnd.NextDouble() - 0.5f) * 1.4f, h * (0.55f + (float)rnd.NextDouble() * 0.35f), ((float)rnd.NextDouble() - 0.5f) * 1.4f);
                Blob(k.Of(leaves), at + o, 0.75f + (float)rnd.NextDouble() * 0.3f, 0.45f, 9);
            }
        }
    }

    /// <summary>
    /// A gate on a country's open-world route: shut (its blocker collider on) until the country's puzzle is solved, then its
    /// leaves swing open and the way out to the landmark is clear. Leaves are pivots at the hinges, turned about their Y.
    /// </summary>
    public class RouteGate : MonoBehaviour
    {
        public Transform LeafA, LeafB;
        public float OpenA = -95f, OpenB = 95f;
        public Collider Blocker;
        public float Seconds = 1.6f;
        public bool IsOpen { get; private set; }
        float m_T;

        public void SetOpen(bool open, bool instant)
        {
            IsOpen = open;
            if (Blocker != null) Blocker.enabled = !open;
            if (instant) { m_T = open ? 1f : 0f; Apply(); }
        }

        void Update()
        {
            float goal = IsOpen ? 1f : 0f;
            if (m_T == goal) return;
            m_T = Mathf.MoveTowards(m_T, goal, Time.deltaTime / Mathf.Max(0.05f, Seconds));
            Apply();
        }

        void Apply()
        {
            float e = m_T * m_T * (3f - 2f * m_T);
            if (LeafA != null) LeafA.localRotation = Quaternion.Euler(0f, OpenA * e, 0f);
            if (LeafB != null) LeafB.localRotation = Quaternion.Euler(0f, OpenB * e, 0f);
        }

        /// <summary>
        /// True when a person-sized capsule (0.28 m radius, clear of steps up to about 0.35 m) can travel the polyline through
        /// the given floor points (in <paramref name="space"/>'s coordinates) without meeting a wall or anything solid.
        /// </summary>
        public static bool PathClear(Transform space, params Vector3[] floorPoints)
        {
            Physics.SyncTransforms();
            int mask = (1 << Layers.Default) | Layers.FloorMask;
            for (int i = 0; i + 1 < floorPoints.Length; i++)
            {
                var a = space.TransformPoint(floorPoints[i]);
                var b = space.TransformPoint(floorPoints[i + 1]);
                var d = b - a;
                float len = d.magnitude;
                if (len < 0.01f) continue;
                if (Physics.CapsuleCast(a + Vector3.up * 0.65f, a + Vector3.up * 1.45f, 0.28f, d / len, len, mask, QueryTriggerInteraction.Ignore)) return false;
            }
            return true;
        }
    }
}
