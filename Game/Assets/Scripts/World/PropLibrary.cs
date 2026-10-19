using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>Hero props modelled procedurally from the approved references (glasses sheet A, passport sheet B).</summary>
    public static class PropLibrary
    {
        static Material Gunmetal => Mats.Lit(new Color(0.42f, 0.43f, 0.44f), 0.72f, 0.9f, "metal_brushed");
        static Material BlackTip => Mats.Lit(new Color(0.03f, 0.03f, 0.035f), 0.55f);
        static Material Lens => Mats.Glass(new Color(0.16f, 0.2f, 0.19f, 0.72f), 0.96f);
        static Material CaseNavy => Mats.Lit(new Color(0.08f, 0.1f, 0.2f), 0.38f, 0f, "leather_navy");
        static Material CaseLining => Mats.Lit(new Color(0.88f, 0.84f, 0.74f), 0.15f, 0f, "fabric_linen");
        static Material CaseBrass => Mats.Lit(new Color(0.7f, 0.52f, 0.26f), 0.65f, 0.9f);
        /// <summary>Glossy dark glass that stays opaque (camera and phone lenses), so the prop still shows through a tear.</summary>
        static Material DarkGlass => Mats.Lit(new Color(0.03f, 0.04f, 0.05f), 0.95f, 0.2f);

        // ------------------------------------------------------------------ Blender models (optional)
        /// <summary>
        /// Resources/Models/&lt;name&gt; with its TS_* slots on the game's materials, turned 180 degrees to face -Z like the
        /// procedural props (see <see cref="Models.Spawn(string, Transform, System.Func{string, Material}, Vector3, float, float)"/>).
        /// Returns null when the model is missing, so callers keep the procedural mesh.
        /// </summary>
        static GameObject Model(string name, Transform parent, System.Func<string, Material> slot) => Models.Spawn(name, parent, slot);

        static Material GlassesSlot(string slot)
        {
            if (slot.Contains("TS_Gunmetal")) return Gunmetal;
            if (slot.Contains("TS_Lens")) return Lens;
            if (slot.Contains("TS_NosePad")) return Mats.Glass(new Color(0.85f, 0.88f, 0.85f, 0.6f));
            if (slot.Contains("TS_TempleTip")) return BlackTip;
            return null;
        }

        static Material CaseSlot(string slot)
        {
            if (slot.Contains("TS_LeatherNavy")) return CaseNavy;
            if (slot.Contains("TS_Lining")) return CaseLining;
            if (slot.Contains("TS_Brass")) return CaseBrass;
            return null;
        }

        /// <summary>Opens or closes a glasses case made by <see cref="GlassesCase"/> (Blender model or procedural).</summary>
        public static void SetCaseOpen(GameObject caseGo, bool open)
        {
            if (caseGo == null) return;
            var lid = caseGo.transform.Find("Lid");
            if (lid != null) { lid.localRotation = Quaternion.Euler(open ? 105f : 0f, 0, 0); return; }
            foreach (var t in caseGo.GetComponentsInChildren<Transform>(true))
                if (t.name == "CaseLid") { t.localRotation = Quaternion.Euler(open ? -105f : 0f, 0, 0); return; } // model frame: negative opens
        }

        /// <summary>
        /// Vintage optician's glasses: softly squared smoke lenses, double bridge, gunmetal, black temple tips and a
        /// small notch on the right hinge only. ~14 cm wide. Origin at the bridge centre; lenses face -Z (toward the
        /// viewer when worn) unless folded.
        /// </summary>
        public static GameObject Glasses(Transform parent, bool folded, float scale = 1f)
        {
            var root = new GameObject("Glasses");
            root.transform.SetParent(parent, false);
            root.transform.localScale = Vector3.one * scale;
            var model = Model(folded ? "Glasses_Folded" : "Glasses_Open", root.transform, GlassesSlot);
            if (model != null)
            {
                foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                {
                    // The ending cracks every renderer named "Lens"; lenses also cast no shadow.
                    if (r.name.StartsWith("Lens")) { r.name = "Lens"; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
                }
                return root;
            }
            var metal = Gunmetal;
            const float lw = 0.052f, lh = 0.042f, gap = 0.018f;
            foreach (var side in new[] { -1f, 1f })
            {
                var c = new Vector3(side * (gap / 2 + lw / 2), 0, 0);
                var rim = MeshBuilder.RoundedRect(lw, lh, 0.012f, 4);
                var path = new List<Vector3>();
                foreach (var p in rim) path.Add(c + new Vector3(p.x, p.y, 0));
                path.Add(path[0]);
                var mb = new MeshBuilder();
                mb.AddTube(path, 0.0016f, 6, false);
                MeshFactory.FromBuilder("Rim", mb, metal, root.transform);
                var lens = new MeshBuilder();
                lens.AddRoundedPlate(c, lw - 0.002f, lh - 0.002f, 0.011f, 0.0015f, Quaternion.identity, 4);
                var l = MeshFactory.FromBuilder("Lens", lens, Lens, root.transform);
                l.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                // End pieces (hinges).
                var hinge = new Vector3(side * (gap / 2 + lw + 0.003f), lh * 0.3f, 0.002f);
                MeshFactory.Box("Hinge", root.transform, hinge, new Vector3(0.007f, 0.008f, 0.006f), metal, false);
                if (side > 0) MeshFactory.Box("HingeNotch", root.transform, hinge + new Vector3(0.0035f, 0.0045f, 0), new Vector3(0.0025f, 0.002f, 0.0062f), BlackTip, false);
                // Temples.
                var tStart = hinge + new Vector3(side * 0.003f, 0, 0.003f);
                Vector3 dir = folded ? new Vector3(-side, 0, 0.12f).normalized : new Vector3(side * 0.06f, -0.05f, 1f).normalized;
                float len = folded ? 0.1f : 0.13f;
                var tEnd = tStart + dir * len;
                var t = new MeshBuilder();
                t.AddTube(new List<Vector3> { tStart, tEnd }, 0.0014f, 5, true);
                MeshFactory.FromBuilder("Temple", t, metal, root.transform);
                var tipDir = folded ? dir : new Vector3(side * 0.05f, -0.6f, 0.8f).normalized;
                var tip = new MeshBuilder();
                tip.AddTube(new List<Vector3> { tEnd, tEnd + tipDir * 0.035f }, 0.0024f, 6, true);
                MeshFactory.FromBuilder("TempleTip", tip, BlackTip, root.transform);
                // Nose pad.
                MeshFactory.Box("NosePad", root.transform, new Vector3(side * (gap / 2 + 0.002f), -0.012f, 0.008f), new Vector3(0.004f, 0.01f, 0.006f), Mats.Glass(new Color(0.85f, 0.88f, 0.85f, 0.6f)), false);
            }
            // Double bridge: top bar and the nose bridge arch.
            var top = new MeshBuilder();
            top.AddTube(new List<Vector3> { new Vector3(-gap / 2 - 0.012f, lh / 2 + 0.003f, 0), new Vector3(gap / 2 + 0.012f, lh / 2 + 0.003f, 0) }, 0.0016f, 6, true);
            MeshFactory.FromBuilder("TopBar", top, metal, root.transform);
            var arch = new List<Vector3>();
            for (int i = 0; i <= 8; i++)
            {
                float a = Mathf.PI * i / 8f;
                arch.Add(new Vector3(-Mathf.Cos(a) * gap / 2, lh * 0.12f + Mathf.Sin(a) * 0.006f, 0));
            }
            var nb = new MeshBuilder();
            nb.AddTube(arch, 0.0015f, 6, true);
            MeshFactory.FromBuilder("NoseBridge", nb, metal, root.transform);
            return root;
        }

        public static GameObject GlassesCase(Transform parent, bool open)
        {
            var root = new GameObject("GlassesCase");
            root.transform.SetParent(parent, false);
            if (Model("GlassesCase", root.transform, CaseSlot) != null)
            {
                SetCaseOpen(root, open);
                var mc = root.AddComponent<BoxCollider>();
                mc.center = new Vector3(0, 0.022f, 0); mc.size = new Vector3(0.165f, 0.045f, 0.066f);
                return root;
            }
            var navy = Mats.Lit(new Color(0.08f, 0.1f, 0.2f), 0.38f, 0f, "leather_navy");
            var lining = Mats.Lit(new Color(0.88f, 0.84f, 0.74f), 0.15f, 0f, "fabric_linen");
            var brass = Mats.Lit(new Color(0.7f, 0.52f, 0.26f), 0.65f, 0.9f);
            var prof = new List<Vector2> { new Vector2(0.0f, 0.0f), new Vector2(0.03f, 0.002f), new Vector2(0.034f, 0.012f), new Vector2(0.034f, 0.02f) };
            var bottom = new MeshBuilder();
            bottom.AddLathe(Vector3.zero, prof, 20);
            var b = MeshFactory.FromBuilder("Bottom", bottom, navy, root.transform);
            b.transform.localScale = new Vector3(2.35f, 1f, 1f);
            MeshFactory.Box("Lining", root.transform, new Vector3(0, 0.012f, 0), new Vector3(0.14f, 0.004f, 0.055f), lining, false);
            var lid = new GameObject("Lid").transform;
            lid.SetParent(root.transform, false);
            lid.localPosition = new Vector3(0, 0.02f, 0.034f);
            var top = new MeshBuilder();
            var tprof = new List<Vector2> { new Vector2(0.034f, 0.0f), new Vector2(0.034f, 0.008f), new Vector2(0.026f, 0.018f), new Vector2(0.0f, 0.021f) };
            top.AddLathe(new Vector3(0, 0, -0.034f), tprof, 20);
            var t = MeshFactory.FromBuilder("LidShell", top, navy, lid);
            t.transform.localScale = new Vector3(2.35f, 1f, 1f);
            lid.localRotation = Quaternion.Euler(open ? 105f : 0f, 0, 0);
            MeshFactory.Box("Clasp", root.transform, new Vector3(0, 0.02f, -0.035f), new Vector3(0.014f, 0.008f, 0.004f), brass, false);
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.02f, 0); col.size = new Vector3(0.165f, 0.045f, 0.07f);
            return root;
        }

        public static GameObject PassportClosed(Transform parent)
        {
            var root = new GameObject("PassportClosed");
            root.transform.SetParent(parent, false);
            var cover = Tex.Art("passport_cover");
            var navy = Mats.Lit(new Color(0.08f, 0.1f, 0.2f), 0.35f, 0f, "leather_navy");
            var paper = Mats.Lit(new Color(0.92f, 0.89f, 0.8f), 0.15f, 0f, "paper_ivory");
            // The model stands upright, cover toward +Z, spine on its origin. Lay it on its back with the head of the cover
            // toward +Z and centre it, like the procedural booklet. Its raised gold emblem replaces the printed cover art.
            var model = Models.Spawn("Passport_Closed", root.transform, Models.Slots(("TS_LeatherNavy", navy), ("TS_Paper", paper), ("TS_Gold", CaseBrass)),
                new Vector3(-0.05425f, 0.0043f, 0f), Quaternion.Euler(-90f, 180f, 0f), Vector3.one);
            if (model == null)
            {
                MeshFactory.Box("Cover", root.transform, new Vector3(0, 0.006f, 0), new Vector3(0.105f, 0.012f, 0.148f), navy, false);
                MeshFactory.Box("Pages", root.transform, new Vector3(0.002f, 0.006f, 0), new Vector3(0.1f, 0.0105f, 0.143f), paper, false);
                if (cover != null) MeshFactory.Quad("CoverArt", root.transform, 0.104f, 0.147f, Mats.Printed(cover, Color.white, 0.3f), new Vector3(0, 0.0122f, 0), Quaternion.Euler(90, 0, 0));
            }
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.006f, 0); col.size = new Vector3(0.11f, 0.02f, 0.15f);
            return root;
        }

        public static GameObject VintageCamera(Transform parent)
        {
            var root = new GameObject("Camera");
            root.transform.SetParent(parent, false);
            var leather = Mats.Lit(new Color(0.06f, 0.06f, 0.065f), 0.35f, 0f, "leather_navy");
            var chrome = Mats.Lit(new Color(0.75f, 0.76f, 0.78f), 0.85f, 1f, "metal_brushed");
            // Lenses and finder windows read as dark glass from outside (opaque, so the camera still shows through a tear).
            if (Model("VintageCamera", root.transform, Models.Slots(("TS_Leather", leather), ("TS_Chrome", chrome), ("TS_Glass", DarkGlass))) == null)
            {
                MeshFactory.Box("Body", root.transform, new Vector3(0, 0.04f, 0), new Vector3(0.14f, 0.08f, 0.04f), leather, false);
                MeshFactory.Box("TopPlate", root.transform, new Vector3(0, 0.085f, 0), new Vector3(0.14f, 0.012f, 0.04f), chrome, false);
                MeshFactory.Cylinder("Lens", root.transform, new Vector3(0, 0.04f, -0.02f), 0.024f, 0.045f, chrome, 20, false).transform.localRotation = Quaternion.Euler(-90, 0, 0);
                MeshFactory.Cylinder("Glass", root.transform, new Vector3(0, 0.04f, -0.066f), 0.018f, 0.002f, Mats.Glass(new Color(0.2f, 0.25f, 0.3f, 0.8f)), 20, false).transform.localRotation = Quaternion.Euler(-90, 0, 0);
                MeshFactory.Cylinder("Dial", root.transform, new Vector3(0.045f, 0.091f, 0), 0.01f, 0.008f, chrome, 12, false);
            }
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.045f, -0.01f); col.size = new Vector3(0.145f, 0.095f, 0.09f);
            return root;
        }

        public static GameObject Postcard(Transform parent, int index)
        {
            var root = new GameObject("Postcard" + index);
            root.transform.SetParent(parent, false);
            var tex = Tex.Art("postcard_" + Mathf.Clamp(index, 1, 4));
            MeshFactory.Box("Card", root.transform, new Vector3(0, 0.0008f, 0), new Vector3(0.15f, 0.0016f, 0.1f), Mats.Lit(new Color(0.92f, 0.9f, 0.84f), 0.2f), false);
            MeshFactory.Quad("Picture", root.transform, 0.148f, 0.098f, Mats.Printed(tex, tex != null ? Color.white : new Color(0.6f, 0.55f, 0.5f), 0.25f), new Vector3(0, 0.0017f, 0), Quaternion.Euler(90, 0, 0));
            var col = root.AddComponent<BoxCollider>();
            col.size = new Vector3(0.15f, 0.01f, 0.1f);
            return root;
        }

        public static GameObject Token(Transform parent)
        {
            var root = new GameObject("FestivalToken");
            root.transform.SetParent(parent, false);
            var brass = Mats.Lit(new Color(0.78f, 0.6f, 0.3f), 0.7f, 0.95f, "metal_brushed");
            // The model coin stands on its edge, obverse toward +Z: lay it flat, obverse up, resting on the root.
            if (Models.Spawn("FestivalToken", root.transform, Models.Slots(("TS_Brass", brass)), new Vector3(0f, 0.0013f, 0f), Quaternion.Euler(-90f, 0f, 0f), Vector3.one) == null)
            {
                MeshFactory.Cylinder("Coin", root.transform, Vector3.zero, 0.016f, 0.003f, brass, 24, false);
                var ring = new MeshBuilder();
                ring.AddTorus(new Vector3(0, 0.003f, 0), 0.012f, 0.0012f, 24, 4, Quaternion.identity);
                MeshFactory.FromBuilder("Emboss", ring, brass, root.transform);
            }
            var col = root.AddComponent<SphereCollider>();
            col.radius = 0.03f;
            return root;
        }

        public static GameObject Phone(Transform parent, out Material screen)
        {
            var root = new GameObject("Phone");
            root.transform.SetParent(parent, false);
            var body = Mats.Lit(new Color(0.05f, 0.05f, 0.06f), 0.85f, 0.3f);
            screen = Mats.UnlitInstance(new Color(0.02f, 0.02f, 0.025f, 1f), null, false, 1f);
            // The model phone stands upright, screen toward +Z: lay it on its back, screen up and its top toward +Z like the
            // procedural phone, with the frame (not the camera bump) on the table. The screen keeps the animated material.
            var model = Models.Spawn("Phone", root.transform, Models.Slots(("TS_MetalDark", body), ("TS_Glass", DarkGlass)),
                new Vector3(0f, 0.0056f, 0f), Quaternion.Euler(-90f, 180f, 0f), Vector3.one);
            var display = Models.Child(model, "Screen")?.GetComponent<Renderer>();
            if (display != null)
            {
                display.sharedMaterial = screen;
                display.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            else
            {
                if (model != null) Object.DestroyImmediate(model);
                MeshFactory.Box("Body", root.transform, new Vector3(0, 0.004f, 0), new Vector3(0.075f, 0.008f, 0.155f), body, false);
                MeshFactory.Quad("Screen", root.transform, 0.068f, 0.145f, screen, new Vector3(0, 0.0085f, 0), Quaternion.Euler(90, 0, 0));
            }
            var col = root.AddComponent<BoxCollider>();
            col.size = new Vector3(0.08f, 0.02f, 0.16f);
            return root;
        }

        /// <summary>
        /// The modelled portable radio for New York's noisemaker (and the one carried to Berlin), on a root whose origin is
        /// the bottom centre and whose front faces -Z. False when the model is missing: build the procedural one.
        /// </summary>
        public static bool NoisemakerModel(Transform root)
        {
            var leather = Mats.Lit(new Color(0.5f, 0.35f, 0.2f), 0.4f, 0f, "leather_navy");
            var chrome = Mats.Lit(new Color(0.75f, 0.7f, 0.6f), 0.6f, 0.9f, "metal_brushed");
            var dial = Mats.Lit(new Color(0.86f, 0.79f, 0.62f), 0.35f);
            var cover = Mats.Glass(new Color(0.7f, 0.75f, 0.75f, 0.18f));
            return Model("Noisemaker", root, Models.Slots(("TS_Leather", leather), ("TS_Chrome", chrome), ("TS_EmissiveWarm", dial), ("TS_Glass", cover))) != null;
        }

        /// <summary>Make a prop grabbable (adds rigidbody, hold pose).</summary>
        public static Grabbable MakeGrabbable(GameObject go, string itemId, string prompt, bool essential = false)
        {
            if (go.GetComponent<Collider>() == null) go.AddComponent<BoxCollider>();
            // Not `??`: in the editor a missing component comes back as a fake null that `??` does not catch.
            var rb = go.GetComponent<Rigidbody>();
            if (rb == null) rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.mass = 0.3f;
            var g = go.AddComponent<Grabbable>();
            g.ItemId = itemId;
            g.Prompt = prompt;
            g.Essential = essential;
            if (essential) StoryDirector.I?.RegisterEssential(g);
            return g;
        }
    }
}
