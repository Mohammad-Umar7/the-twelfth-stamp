using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>Reusable architecture and props (modular, intentional shapes with colliders and consistent scale).</summary>
    public static class Kit
    {
        // ------------------------------------------------------------------ architecture
        /// <summary>A building facade (front face toward local -Z) with recessed windows, cornices and optional balconies.</summary>
        public static GameObject Facade(Transform parent, string name, Vector3 pos, float yaw, float width, float floors, float floorHeight, Material wall, Material window, Material trim, bool balconies, bool litWindows, float lit = 0.35f)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = pos;
            root.localRotation = Quaternion.Euler(0, yaw, 0);
            float h = floors * floorHeight;
            MeshFactory.Box("Wall", root, new Vector3(0, h / 2, 0.25f), new Vector3(width, h, 0.5f), wall, true, 1.5f);
            int cols = Mathf.Max(1, Mathf.RoundToInt(width / 2.2f));
            var rnd = new System.Random(name.GetHashCode());
            for (int f = 0; f < floors; f++)
            {
                float y = f * floorHeight;
                // Cornice line.
                MeshFactory.Box("Cornice", root, new Vector3(0, y + floorHeight - 0.08f, -0.05f), new Vector3(width + 0.1f, 0.12f, 0.2f), trim, false);
                for (int c = 0; c < cols; c++)
                {
                    float x = -width / 2 + (c + 0.5f) * width / cols;
                    if (f == 0) continue; // ground floor handled by caller (doors, shopfronts)
                    var winH = floorHeight * 0.55f;
                    var winY = y + floorHeight * 0.45f;
                    MeshFactory.Box("Recess", root, new Vector3(x, winY, -0.005f), new Vector3(1.05f, winH + 0.12f, 0.02f), trim, false);
                    bool isLit = litWindows && rnd.NextDouble() < lit;
                    var glass = MeshFactory.Box("Window", root, new Vector3(x, winY, -0.02f), new Vector3(0.9f, winH, 0.02f), isLit ? Mats.Emissive(new Color(0.3f, 0.25f, 0.18f), new Color(1.2f, 0.75f, 0.4f)) : window, false);
                    glass.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    if (balconies && f == 2)
                    {
                        MeshFactory.Box("Balcony", root, new Vector3(x, y + 0.05f, -0.35f), new Vector3(1.4f, 0.08f, 0.6f), trim, false);
                        Railing(root, new Vector3(x - 0.7f, y + 0.1f, -0.64f), new Vector3(x + 0.7f, y + 0.1f, -0.64f), 0.9f, Mats.Lit(new Color(0.05f, 0.05f, 0.05f), 0.5f, 0.8f));
                    }
                }
            }
            MeshFactory.Box("Roofline", root, new Vector3(0, h + 0.15f, 0.1f), new Vector3(width + 0.2f, 0.3f, 0.7f), trim, false);
            return root.gameObject;
        }

        public static void Railing(Transform parent, Vector3 a, Vector3 b, float height, Material mat, float spacing = 0.12f)
        {
            float len = Vector3.Distance(a, b);
            var dir = (b - a).normalized;
            int n = Mathf.Max(2, Mathf.RoundToInt(len / spacing));
            var mb = new MeshBuilder();
            for (int i = 0; i <= n; i++)
            {
                var p = Vector3.Lerp(a, b, i / (float)n);
                mb.AddBox(p + Vector3.up * height / 2, new Vector3(0.018f, height, 0.018f));
            }
            var mid = (a + b) / 2;
            var rot = Quaternion.LookRotation(dir, Vector3.up);
            mb.AddBox(mid + Vector3.up * height, new Vector3(0.04f, 0.04f, len), rot);
            mb.AddBox(mid + Vector3.up * 0.08f, new Vector3(0.025f, 0.025f, len), rot);
            MeshFactory.FromBuilder("Railing", mb, mat, parent);
        }

        /// <summary>Street lamp. <paramref name="model"/> names a Blender lamp post (e.g. StreetLamp_Paris) to use for the visuals.</summary>
        public static GameObject LampPost(Transform parent, Vector3 pos, Color light, float intensity, Material metal, out Light l, string model = null)
        {
            var root = new GameObject("LampPost").transform;
            root.SetParent(parent, false);
            root.localPosition = pos;
            var foot = MeshFactory.Cylinder("Base", root, Vector3.zero, 0.12f, 0.5f, metal, 12);
            var pole = MeshFactory.Cylinder("Pole", root, new Vector3(0, 0.5f, 0), 0.05f, 3.2f, metal, 10);
            var glow = Mats.Emissive(new Color(0.4f, 0.32f, 0.2f), light * 2.2f);
            if (model != null && Models.Spawn(model, root, Models.Slots(("TS_MetalDark", metal), ("TS_EmissiveWarm", glow))) != null)
            {
                Models.StripVisual(foot);
                Models.StripVisual(pole);
            }
            else
            {
                MeshFactory.Box("Arm", root, new Vector3(0, 3.65f, 0), new Vector3(0.08f, 0.08f, 0.08f), metal, false);
                var lamp = MeshFactory.Frustum("Lantern", root, new Vector3(0, 3.5f, 0), 0.16f, 0.1f, 0.42f, glow, 6);
                lamp.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                MeshFactory.Frustum("Cap", root, new Vector3(0, 3.92f, 0), 0.2f, 0.02f, 0.18f, metal, 6);
            }
            var lg = new GameObject("Light");
            lg.transform.SetParent(root, false);
            lg.transform.localPosition = new Vector3(0, 3.6f, 0);
            l = lg.AddComponent<Light>();
            l.type = LightType.Point; l.color = light; l.intensity = intensity; l.range = 8f; l.shadows = LightShadows.None;
            return root.gameObject;
        }

        public static GameObject Bench(Transform parent, Vector3 pos, float yaw, Material wood, Material metal)
        {
            var root = new GameObject("Bench").transform;
            root.SetParent(parent, false);
            root.localPosition = pos;
            root.localRotation = Quaternion.Euler(0, yaw, 0);
            for (int i = 0; i < 3; i++) MeshFactory.Box("Slat", root, new Vector3(0, 0.45f, -0.15f + i * 0.13f), new Vector3(1.6f, 0.04f, 0.1f), wood, false);
            MeshFactory.Box("Back", root, new Vector3(0, 0.75f, 0.17f), new Vector3(1.6f, 0.3f, 0.04f), wood, false);
            MeshFactory.Box("LegL", root, new Vector3(-0.7f, 0.22f, 0), new Vector3(0.06f, 0.45f, 0.4f), metal, false);
            MeshFactory.Box("LegR", root, new Vector3(0.7f, 0.22f, 0), new Vector3(0.06f, 0.45f, 0.4f), metal, false);
            var col = root.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.45f, 0); col.size = new Vector3(1.6f, 0.9f, 0.5f);
            return root.gameObject;
        }

        /// <summary>Festival verification desk with a ledger, ink pad, lamp and a brass stamp.</summary>
        public static GameObject Desk(Transform parent, Vector3 pos, float yaw, Material wood, Material paper, Material brass, out Transform ledgerTop, out Light lamp)
        {
            var root = new GameObject("VerificationDesk").transform;
            root.SetParent(parent, false);
            root.localPosition = pos;
            root.localRotation = Quaternion.Euler(0, yaw, 0);
            var top = MeshFactory.Box("Top", root, new Vector3(0, 0.98f, 0), new Vector3(1.6f, 0.06f, 0.75f), wood, true);
            var front = MeshFactory.Box("Front", root, new Vector3(0, 0.5f, -0.33f), new Vector3(1.55f, 0.9f, 0.05f), wood, true);
            var sideL = MeshFactory.Box("SideL", root, new Vector3(-0.77f, 0.5f, 0), new Vector3(0.05f, 0.95f, 0.7f), wood, true);
            var sideR = MeshFactory.Box("SideR", root, new Vector3(0.77f, 0.5f, 0), new Vector3(0.05f, 0.95f, 0.7f), wood, true);
            ledgerTop = new GameObject("LedgerTop").transform;
            ledgerTop.SetParent(root, false);
            ledgerTop.localPosition = new Vector3(-0.15f, 1.045f, 0.02f);
            if (DeskModel(root, wood, paper, brass))
            {
                foreach (var go in new[] { top, front, sideL, sideR }) Models.StripVisual(go);
            }
            else
            {
                MeshFactory.Box("Ledger", root, new Vector3(-0.15f, 1.025f, 0.02f), new Vector3(0.5f, 0.03f, 0.36f), paper, false);
                MeshFactory.Box("InkPad", root, new Vector3(0.35f, 1.02f, 0.05f), new Vector3(0.14f, 0.02f, 0.09f), Mats.Lit(new Color(0.35f, 0.05f, 0.05f), 0.6f), false);
                MeshFactory.Cylinder("StampBase", root, new Vector3(0.52f, 1.01f, 0.05f), 0.035f, 0.03f, brass, 14, false);
                MeshFactory.Cylinder("StampHandle", root, new Vector3(0.52f, 1.04f, 0.05f), 0.015f, 0.09f, Mats.Lit(new Color(0.15f, 0.08f, 0.05f), 0.5f), 10, false);
                MeshFactory.Sphere("StampKnob", root, new Vector3(0.52f, 1.14f, 0.05f), 0.025f, Mats.Lit(new Color(0.15f, 0.08f, 0.05f), 0.5f));
                // Desk lamp.
                MeshFactory.Cylinder("LampBase", root, new Vector3(0.6f, 1.01f, 0.25f), 0.07f, 0.02f, brass, 14, false);
                MeshFactory.Cylinder("LampStem", root, new Vector3(0.6f, 1.03f, 0.25f), 0.012f, 0.36f, brass, 8, false);
                var shade = MeshFactory.Frustum("Shade", root, new Vector3(0.6f, 1.35f, 0.22f), 0.12f, 0.05f, 0.12f, Mats.Emissive(new Color(0.2f, 0.25f, 0.18f), new Color(0.6f, 0.45f, 0.2f)), 16);
                shade.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var lg = new GameObject("DeskLamp");
            lg.transform.SetParent(root, false);
            lg.transform.localPosition = new Vector3(0.55f, 1.3f, 0.15f);
            lamp = lg.AddComponent<Light>();
            lamp.type = LightType.Point; lamp.color = new Color(1f, 0.78f, 0.5f); lamp.intensity = 2.5f; lamp.range = 4f; lamp.shadows = LightShadows.None;
            return root.gameObject;
        }

        /// <summary>
        /// The modelled festival desk on a <see cref="Desk"/> root, visitor side (panelled front and cloth) toward the root's
        /// +Z. The model is a 0.76 m desk, but the checkpoint's colliders and the ledger trace sit at the procedural 1.01 m,
        /// so the desk body is stretched to that height and the desk-top pieces go where the procedural ones were: the
        /// ledger (thickened) under the trace, the lamp under the existing lamp light.
        /// </summary>
        static bool DeskModel(Transform root, Material wood, Material paper, Material brass)
        {
            const float top = 1.01f;
            var cloth = Mats.Lit(new Color(0.45f, 0.1f, 0.1f), 0.3f, 0f, "fabric_linen");
            var ink = Mats.Lit(new Color(0.35f, 0.05f, 0.05f), 0.6f);
            var bulb = Mats.Emissive(new Color(1f, 0.85f, 0.6f), new Color(1.4f, 1f, 0.55f));
            var model = Models.Spawn("VerificationDesk", root, Models.Slots(("TS_WoodDark", wood), ("TS_Paper", paper), ("TS_Brass", brass),
                ("TS_Fabric", cloth), ("TS_FabricDark", ink), ("TS_EmissiveWarm", bulb)), Vector3.zero, 0f);
            if (model == null) return false;
            void Place(string child, Vector3 pos, float yaw, Vector3 scale)
            {
                var t = Models.Child(model, child);
                if (t == null) return;
                t.localPosition = pos;
                t.localRotation = Quaternion.Euler(0f, yaw, 0f);
                t.localScale = scale;
            }
            Place("Desk", Vector3.zero, 0f, new Vector3(1f, top / 0.76f, 1f));
            Place("Ledger", new Vector3(-0.15f, top, 0.02f), 0f, new Vector3(1f, 1.8f, 1f));
            Place("InkPad", new Vector3(0.35f, top, 0.05f), 0f, Vector3.one);
            Place("HandStamp", new Vector3(0.52f, top, 0.05f), 0f, Vector3.one);
            Place("Lamp", new Vector3(0.6f, top, 0.25f), 180f, Vector3.one); // its arm reaches back over the desk
            return true;
        }

        public static GameObject Awning(Transform parent, Vector3 pos, float yaw, float width, float depth, float height, Material canvas, Material pole)
        {
            var root = new GameObject("Awning").transform;
            root.SetParent(parent, false);
            root.localPosition = pos;
            root.localRotation = Quaternion.Euler(0, yaw, 0);
            MeshFactory.Box("Canvas", root, new Vector3(0, height, 0), new Vector3(width, 0.04f, depth), canvas, false, 1f, Quaternion.Euler(-8, 0, 0));
            foreach (var x in new[] { -width / 2 + 0.05f, width / 2 - 0.05f })
                foreach (var z in new[] { -depth / 2 + 0.05f, depth / 2 - 0.05f })
                    MeshFactory.Cylinder("Pole", root, new Vector3(x, 0, z), 0.03f, height, pole, 8);
            return root.gameObject;
        }

        public static GameObject Crate(Transform parent, Vector3 pos, float yaw, Vector3 size, Material wood)
        {
            var go = MeshFactory.Box("Crate", parent, pos + Vector3.up * size.y / 2, size, wood, true, 0.8f, Quaternion.Euler(0, yaw, 0));
            return go;
        }

        public static GameObject Column(Transform parent, Vector3 pos, float radius, float height, Material stone, int segments = 16)
        {
            var root = new GameObject("Column").transform;
            root.SetParent(parent, false);
            root.localPosition = pos;
            MeshFactory.Box("Plinth", root, new Vector3(0, 0.15f, 0), new Vector3(radius * 2.6f, 0.3f, radius * 2.6f), stone, true);
            MeshFactory.Cylinder("Shaft", root, new Vector3(0, 0.3f, 0), radius, height - 0.6f, stone, segments);
            MeshFactory.Box("Capital", root, new Vector3(0, height - 0.15f, 0), new Vector3(radius * 2.6f, 0.3f, radius * 2.6f), stone, false);
            return root.gameObject;
        }

        /// <summary>Rain (or rising sand) volume. Particles stay out of open tears (stencil NotEqual).</summary>
        public static ParticleSystem Weather(Transform parent, Vector3 center, Vector3 size, bool rain, Color color, float rate, float speed, bool upward = false)
        {
            var go = new GameObject(rain ? "Rain" : "Particles");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = rain ? 1.2f : 6f;
            main.startSpeed = 0f;
            main.startSize3D = rain;
            if (rain)
            {
                main.startSizeX = 0.012f; main.startSizeY = 0.35f; main.startSizeZ = 1f;
            }
            else main.startSize = new ParticleSystem.MinMaxCurve(0.01f, 0.03f);
            main.startColor = color;
            main.maxParticles = rain ? 2500 : 600;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;
            var em = ps.emission; em.rateOverTime = rate;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = size;
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(upward ? -0.05f : -0.3f, upward ? 0.05f : 0.3f);
            vel.y = new ParticleSystem.MinMaxCurve(upward ? speed * 0.6f : -speed * 1.1f, upward ? speed : -speed * 0.9f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = rain ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            if (rain) { r.velocityScale = 0.015f; r.lengthScale = 1.2f; }
            r.maxParticleSize = rain ? 0.012f : 0.03f;
            r.sharedMaterial = Mats.Particle(rain ? Tex.Streak : Tex.SoftDot, !rain, Color.white, rain ? 0.8f : 1.2f, Mats.StencilNotEqual);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        /// <summary>Large water plane (river, pool) with a ripple normal map.</summary>
        public static GameObject Water(Transform parent, Vector3 center, Vector2 size, Color color, float smoothness = 0.92f)
        {
            var mat = Mats.Lit(color, smoothness, 0f, null, false);
            var n = Tex.Normal("water_ripple");
            if (n != null)
            {
                mat = new Material(Mats.Template("Lit_Normal")) { name = "Water" };
                mat.SetColor("_BaseColor", color);
                mat.SetTexture("_BumpMap", n);
                mat.SetTextureScale("_BumpMap", new Vector2(size.x / 4f, size.y / 4f));
                mat.SetFloat("_BumpScale", 0.35f);
                mat.SetFloat("_Smoothness", smoothness);
            }
            var go = MeshFactory.Box("Water", parent, center, new Vector3(size.x, 0.02f, size.y), mat, false);
            go.AddComponent<WaterDrift>();
            return go;
        }
    }

    /// <summary>Scrolls the water normal map slowly.</summary>
    public class WaterDrift : MonoBehaviour
    {
        Material m_Mat;
        void Start() { var r = GetComponent<Renderer>(); m_Mat = r != null ? r.sharedMaterial : null; }
        void Update()
        {
            if (m_Mat != null && m_Mat.HasProperty("_BumpMap")) m_Mat.SetTextureOffset("_BumpMap", new Vector2(Time.time * 0.01f, Time.time * 0.006f));
        }
    }
}
