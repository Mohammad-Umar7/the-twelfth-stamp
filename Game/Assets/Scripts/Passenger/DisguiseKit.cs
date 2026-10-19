using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// Country disguises the Passenger copies from Saeed's memories, worn over the same glass core (the photo-oval
    /// face, document slot and tag fingers always stay visible). No sacred symbols or religious items are used.
    /// </summary>
    public static class DisguiseKit
    {
        public static Disguise ForCell(CellId cell)
        {
            switch (cell)
            {
                case CellId.ApartmentCorrupt: return Disguise.Kandura;
                case CellId.Egypt: return Disguise.ExhibitLinen;
                case CellId.Paris: return Disguise.RainCoat;
                case CellId.NewYork: return Disguise.LostLuggage;
                case CellId.Japan: return Disguise.LanternPaper;
                case CellId.Riyadh: return Disguise.MirageMirror;
                case CellId.Agra: return Disguise.PetalWet;
                case CellId.Rio: return Disguise.Confetti;
                case CellId.London: return Disguise.ClockCoat;
                case CellId.ShopCorrupt: return Disguise.OpticianCoat;
                case CellId.Berlin: return Disguise.Mimic;
                default: return Disguise.None;
            }
        }

        public static void Build(PassengerBody b, Disguise d, Transform root)
        {
            switch (d)
            {
                case Disguise.ExhibitLinen: Linen(b, root); break;
                case Disguise.Kandura: Robe(b, root, new Color(0.93f, 0.92f, 0.88f), "fabric_linen", 0.22f, false); break;
                case Disguise.Mimic: Robe(b, root, new Color(0.93f, 0.92f, 0.88f), "fabric_linen", 0.22f, false); Hair(b); break;
                case Disguise.RainCoat: Robe(b, root, new Color(0.07f, 0.08f, 0.09f), null, 0.75f, true); Umbrella(b); break;
                case Disguise.ClockCoat: Robe(b, root, new Color(0.12f, 0.12f, 0.14f), null, 0.7f, true); Drips(b, root); break;
                case Disguise.OpticianCoat: Robe(b, root, new Color(0.7f, 0.71f, 0.69f), "fabric_linen", 0.3f, true); LensRig(b); break;
                case Disguise.LostLuggage: Luggage(b, root); break;
                case Disguise.LanternPaper: Lantern(b, root); break;
                case Disguise.MirageMirror: Mirrors(b, root); break;
                case Disguise.PetalWet: Scatter(b, new[] { new Color(0.96f, 0.72f, 0.74f), new Color(0.98f, 0.84f, 0.7f) }, 70, 0.035f); break;
                case Disguise.Confetti: Scatter(b, new[] { new Color(0.75f, 0.55f, 0.35f), new Color(0.4f, 0.6f, 0.45f), new Color(0.6f, 0.45f, 0.6f), new Color(0.75f, 0.7f, 0.4f) }, 110, 0.018f); Streamers(b); break;
            }
        }

        // ------------------------------------------------------------------ pieces
        static void Linen(PassengerBody b, Transform root)
        {
            // Old, stained grave linen: overlapping turns so no glass shows between them.
            var linen = Mats.Lit(new Color(0.56f, 0.5f, 0.39f), 0.06f, 0f, "fabric_linen");
            var dark = Mats.Lit(new Color(0.33f, 0.28f, 0.21f), 0.05f, 0f, "fabric_linen");
            float k = b.UsesModel ? 1.2f : 1f; // the modelled limbs are thicker than the procedural ones
            Ribbon(b.Chest, Vector3.up * 0.44f, 0.2f, 0.17f, 0.46f, 8, 0.066f, linen).transform.localScale = new Vector3(1f, 1f, b.UsesModel ? 0.62f : 1f);
            Ribbon(b.Hips, Vector3.up * 0.3f, 0.16f, 0.15f, 0.36f, 6, 0.066f, dark).transform.localScale = new Vector3(1f, 1f, b.UsesModel ? 0.7f : 1f);
            Ribbon(b.UpperArmL, Vector3.zero, 0.055f * k, 0.045f * k, 0.37f, 7, 0.056f, linen);
            Ribbon(b.UpperArmR, Vector3.zero, 0.055f * k, 0.045f * k, 0.37f, 7, 0.056f, linen);
            Ribbon(b.ForearmL, Vector3.zero, 0.045f * k, 0.035f * k, 0.37f, 7, 0.054f, dark);
            Ribbon(b.ForearmR, Vector3.zero, 0.045f * k, 0.035f * k, 0.37f, 7, 0.054f, dark);
            Ribbon(b.ThighL, Vector3.zero, 0.07f * k, 0.055f * k, 0.52f, 9, 0.06f, linen);
            Ribbon(b.ThighR, Vector3.zero, 0.07f * k, 0.055f * k, 0.52f, 9, 0.06f, linen);
            Ribbon(b.ShinL, Vector3.zero, 0.055f * k, 0.04f * k, 0.54f, 9, 0.06f, dark);
            Ribbon(b.ShinR, Vector3.zero, 0.055f * k, 0.04f * k, 0.54f, 9, 0.06f, dark);
            // The head is bound tight; only the photo oval looks out.
            Ribbon(b.Head, new Vector3(0, 0.235f, -0.01f), 0.09f, 0.112f, 0.075f, 3, 0.034f, linen); // crown, not a hat
            Ribbon(b.Head, new Vector3(0, 0.07f, -0.01f), 0.112f, 0.1f, 0.08f, 3, 0.034f, dark);
            Ribbon(b.Neck, new Vector3(0, 0.12f, 0f), 0.065f, 0.075f, 0.12f, 4, 0.04f, dark);
            // Long loose strips that sway and trail.
            var strip = Mats.Cutout(Tex.Albedo("fabric_linen"), new Color(0.5f, 0.44f, 0.34f));
            for (int i = 0; i < 14; i++)
            {
                var parent = i % 4 == 0 ? b.ForearmL : i % 4 == 1 ? b.ForearmR : i % 4 == 2 ? b.Hips : b.Chest;
                float len = 0.32f + (i * 37 % 11) * 0.03f;
                var y = parent == b.Chest ? 0.05f : -0.3f;
                var q = MeshFactory.Quad("Strip", parent, 0.035f, len, strip, new Vector3((i % 7 - 3) * 0.03f, y - len * 0.3f, 0.06f), Quaternion.Euler(0, i * 47f, (i % 3 - 1) * 6f));
                var sway = q.AddComponent<Sway>();
                sway.Amplitude = 8f + i % 5 * 2f;
                sway.Speed = 1.1f + i * 0.09f;
            }
        }

        /// <summary>Flat bandage ribbon spiralling down a limb (starts at top, goes down -Y by length).</summary>
        static GameObject Ribbon(Transform bone, Vector3 top, float r0, float r1, float length, int turns, float width, Material mat)
        {
            var mb = new MeshBuilder();
            int steps = turns * 14;
            for (int i = 0; i < steps; i++)
            {
                float t0 = i / (float)steps, t1 = (i + 1) / (float)steps;
                float a0 = t0 * turns * Mathf.PI * 2f, a1 = t1 * turns * Mathf.PI * 2f;
                float y0 = -t0 * length, y1 = -t1 * length;
                float rr0 = Mathf.Lerp(r0, r1, t0) + 0.006f, rr1 = Mathf.Lerp(r0, r1, t1) + 0.006f;
                var d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0));
                var d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                var p0 = top + d0 * rr0 + Vector3.up * y0;
                var p1 = top + d1 * rr1 + Vector3.up * y1;
                var w = Vector3.up * width * 0.5f;
                mb.AddQuad(p0 - w, p1 - w, p1 + w, p0 + w, new Vector2(t0 * 8, 0), new Vector2(t1 * 8, 0), new Vector2(t1 * 8, 1), new Vector2(t0 * 8, 1));
                mb.AddQuad(p0 + w, p1 + w, p1 - w, p0 - w, new Vector2(t0 * 8, 1), new Vector2(t1 * 8, 1), new Vector2(t1 * 8, 0), new Vector2(t0 * 8, 0));
            }
            return MeshFactory.FromBuilder("Wrap", mb, mat, bone);
        }

        static void Robe(PassengerBody b, Transform root, Color color, string texture, float smoothness, bool open)
        {
            var mat = Mats.Lit(color, smoothness, 0f, texture);
            var prof = new System.Collections.Generic.List<Vector2>
            {
                new Vector2(0.1f, 0.46f), new Vector2(0.22f, 0.4f), new Vector2(0.21f, 0.1f), new Vector2(0.19f, -0.2f),
                new Vector2(0.21f, -0.6f), new Vector2(0.25f, -0.95f), new Vector2(0.27f, -1.0f)
            };
            var mb = new MeshBuilder();
            mb.AddLathe(Vector3.zero, prof, 24, 1f, open ? 115f : 0f, open ? 425f : 360f);
            var robe = MeshFactory.FromBuilder("Robe", mb, mat, b.Chest);
            robe.transform.localPosition = Vector3.zero;
            // Sleeves.
            foreach (var arm in new[] { b.UpperArmL, b.UpperArmR, b.ForearmL, b.ForearmR })
            {
                var s = new MeshBuilder();
                s.AddFrustum(Vector3.zero, 0.062f, 0.058f, 0.36f, 10, false, false);
                var sleeve = MeshFactory.FromBuilder("Sleeve", s, mat, arm);
                sleeve.transform.localPosition = new Vector3(0, -0.36f, 0);
            }
        }

        static void Hair(PassengerBody b)
        {
            var hair = MeshFactory.Sphere("Hair", b.Head, new Vector3(0, 0.19f, -0.025f), 1f, Mats.Lit(new Color(0.03f, 0.03f, 0.035f), 0.3f), 16, 10);
            hair.transform.localScale = new Vector3(0.112f, 0.1f, 0.12f);
        }

        static void Umbrella(PassengerBody b)
        {
            var handle = MeshFactory.Cylinder("UmbrellaShaft", b.PropHoldR, new Vector3(0, -0.05f, 0), 0.008f, 0.95f, Mats.Lit(new Color(0.1f, 0.1f, 0.1f), 0.5f, 0.6f), 8, false);
            handle.transform.localRotation = Quaternion.Euler(0, 0, 0);
            var prof = new System.Collections.Generic.List<Vector2> { new Vector2(0.0f, 0.18f), new Vector2(0.25f, 0.13f), new Vector2(0.48f, 0.0f) };
            var mb = new MeshBuilder();
            mb.AddLathe(Vector3.zero, prof, 16, 1f, 0f, 290f); // torn: a wedge is missing
            var canopy = MeshFactory.FromBuilder("Canopy", mb, Mats.Cutout(Tex.Albedo("paper_ivory"), new Color(0.12f, 0.12f, 0.13f)), handle.transform);
            canopy.transform.localPosition = new Vector3(0, 0.9f, 0);
        }

        static void Drips(PassengerBody b, Transform root)
        {
            var mat = Mats.Glass(new Color(0.7f, 0.8f, 0.85f, 0.45f));
            for (int i = 0; i < 12; i++)
            {
                var parent = i % 2 == 0 ? b.Chest : b.Hips;
                var d = MeshFactory.Sphere("Drip", parent, new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(-0.6f, 0.2f), 0.2f), 0.012f, mat, 8, 6);
                d.transform.localScale = new Vector3(1f, 1.8f, 1f) * 0.012f / 0.012f;
            }
        }

        static void LensRig(PassengerBody b)
        {
            var metal = Mats.Lit(new Color(0.35f, 0.36f, 0.38f), 0.7f, 0.9f, "metal_brushed");
            var band = new MeshBuilder();
            band.AddTorus(Vector3.zero, 0.12f, 0.008f, 28, 6, Quaternion.identity);
            MeshFactory.FromBuilder("HeadBand", band, metal, b.Head).transform.localPosition = new Vector3(0, 0.2f, -0.01f);
            var lensRing = new MeshBuilder();
            lensRing.AddTorus(Vector3.zero, 0.035f, 0.006f, 24, 6, Quaternion.Euler(90, 0, 0));
            MeshFactory.FromBuilder("LensRing", lensRing, metal, b.Head).transform.localPosition = new Vector3(0.04f, 0.16f, 0.13f);
            MeshFactory.Make("Lens", MeshFactory.QuadMesh(0.06f, 0.06f), Mats.Glass(new Color(0.3f, 0.4f, 0.38f, 0.5f)), b.Head, new Vector3(0.04f, 0.16f, 0.131f), Quaternion.Euler(0, 180, 0));
        }

        static void Luggage(PassengerBody b, Transform root)
        {
            var strap = Mats.Lit(new Color(0.16f, 0.12f, 0.09f), 0.35f, 0f, "leather_navy");
            var tagMat = Mats.Lit(new Color(0.86f, 0.8f, 0.64f), 0.2f, 0f, "paper_ivory");
            var str = Mats.Lit(new Color(0.55f, 0.1f, 0.1f), 0.2f);
            foreach (var (bone, y, r) in new[] { (b.Chest, 0.32f, 0.21f), (b.Chest, 0.12f, 0.19f), (b.Hips, 0.15f, 0.17f), (b.ThighL, -0.25f, 0.07f), (b.ThighR, -0.25f, 0.07f), (b.UpperArmL, -0.2f, 0.055f), (b.UpperArmR, -0.2f, 0.055f) })
            {
                var t = new MeshBuilder();
                t.AddTorus(Vector3.zero, r, 0.012f, 24, 4, Quaternion.identity);
                MeshFactory.FromBuilder("Strap", t, strap, bone).transform.localPosition = new Vector3(0, y, 0);
            }
            for (int i = 0; i < 9; i++)
            {
                var parent = i % 3 == 0 ? b.Chest : i % 3 == 1 ? b.Hips : (i % 2 == 0 ? b.UpperArmL : b.UpperArmR);
                var tag = MeshFactory.Box("LuggageTag", parent, new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(-0.1f, 0.3f), 0.2f), new Vector3(0.05f, 0.09f, 0.003f), tagMat, false, 1f, Quaternion.Euler(0, 0, Random.Range(-25f, 25f)));
                MeshFactory.Box("String", tag.transform, new Vector3(0, 0.06f, 0), new Vector3(0.003f, 0.04f, 0.003f), str, false);
                var sway = tag.AddComponent<Sway>();
                sway.Amplitude = 12f; sway.Speed = 1.5f + i * 0.1f;
            }
        }

        static void Lantern(PassengerBody b, Transform root)
        {
            var paper = Mats.Lit(new Color(0.95f, 0.78f, 0.55f), 0.1f, 0f, "paper_shoji", false, new Color(0.9f, 0.45f, 0.18f));
            var rib = Mats.Lit(new Color(0.12f, 0.08f, 0.06f), 0.3f);
            var prof = new System.Collections.Generic.List<Vector2> { new Vector2(0.12f, 0.45f), new Vector2(0.24f, 0.32f), new Vector2(0.26f, 0.12f), new Vector2(0.22f, -0.12f), new Vector2(0.12f, -0.22f) };
            var mb = new MeshBuilder();
            mb.AddLathe(Vector3.zero, prof, 20, 1f, 0f, 300f);
            MeshFactory.FromBuilder("LanternShell", mb, paper, b.Chest);
            for (int i = 0; i < 5; i++)
            {
                var t = new MeshBuilder();
                float y = 0.42f - i * 0.15f;
                float r = Mathf.Lerp(0.14f, 0.25f, Mathf.Sin((i + 0.5f) / 5f * Mathf.PI));
                t.AddTorus(Vector3.zero, r, 0.006f, 24, 4, Quaternion.identity);
                MeshFactory.FromBuilder("Rib", t, rib, b.Chest).transform.localPosition = new Vector3(0, y, 0);
            }
        }

        static void Mirrors(PassengerBody b, Transform root)
        {
            var mirror = Mats.Lit(new Color(0.85f, 0.86f, 0.88f), 0.98f, 1f);
            for (int i = 0; i < 8; i++)
            {
                var parent = i < 4 ? b.Chest : (i % 2 == 0 ? b.UpperArmL : b.UpperArmR);
                var plate = new MeshBuilder();
                plate.AddRoundedPlate(Vector3.zero, 0.12f, 0.16f, 0.02f, 0.006f, Quaternion.identity, 3);
                var go = MeshFactory.FromBuilder("MirrorPlate", plate, mirror, parent);
                go.transform.localPosition = new Vector3((i % 2 == 0 ? -1 : 1) * 0.12f, 0.1f + (i % 4) * 0.08f - (i >= 4 ? 0.3f : 0f), 0.11f);
                go.transform.localRotation = Quaternion.Euler(Random.Range(-15f, 15f), (i % 2 == 0 ? -1 : 1) * 25f, Random.Range(-20f, 20f));
            }
        }

        static void Scatter(PassengerBody b, Color[] colors, int count, float size)
        {
            var bones = new[] { b.Chest, b.Hips, b.UpperArmL, b.UpperArmR, b.ForearmL, b.ForearmR, b.ThighL, b.ThighR, b.Head };
            for (int i = 0; i < count; i++)
            {
                var bone = bones[i % bones.Length];
                var mat = Mats.Lit(colors[i % colors.Length], 0.7f);
                float a = Random.Range(0f, Mathf.PI * 2f);
                float r = bone == b.Chest || bone == b.Hips ? 0.19f : bone == b.Head ? 0.12f : 0.06f;
                var pos = new Vector3(Mathf.Cos(a) * r, Random.Range(-0.35f, bone == b.Chest ? 0.42f : 0f), Mathf.Sin(a) * r);
                if (bone == b.Head) pos.y = Random.Range(0.05f, 0.26f);
                var q = MeshFactory.Quad("Fleck", bone, size, size * 1.4f, mat, pos, Quaternion.LookRotation(-new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a))) * Quaternion.Euler(0, 0, Random.Range(0f, 360f)));
                q.GetComponent<Renderer>().sharedMaterial = mat;
            }
        }

        static void Streamers(PassengerBody b)
        {
            var colors = new[] { new Color(0.7f, 0.5f, 0.35f), new Color(0.45f, 0.62f, 0.5f), new Color(0.62f, 0.5f, 0.66f) };
            for (int i = 0; i < 8; i++)
            {
                var parent = i % 2 == 0 ? b.UpperArmL : b.UpperArmR;
                var s = MeshFactory.Quad("Streamer", parent, 0.02f, 0.5f, Mats.Lit(colors[i % 3], 0.4f), new Vector3((i - 4) * 0.01f, -0.3f, 0.05f), Quaternion.Euler(0, i * 45f, 0));
                var sway = s.AddComponent<Sway>(); sway.Amplitude = 6f; sway.Speed = 0.6f;
            }
        }
    }

    /// <summary>Gentle procedural sway for hanging strips and tags.</summary>
    public class Sway : MonoBehaviour
    {
        public float Amplitude = 8f;
        public float Speed = 1f;
        Quaternion m_Base;
        float m_Offset;

        void Start()
        {
            m_Base = transform.localRotation;
            m_Offset = Random.Range(0f, 10f);
        }

        void Update()
        {
            float a = Mathf.Sin(Time.time * Speed + m_Offset) * Amplitude;
            float b = Mathf.Sin(Time.time * Speed * 0.7f + m_Offset * 2f) * Amplitude * 0.5f;
            transform.localRotation = m_Base * Quaternion.Euler(a, 0, b);
        }
    }
}
