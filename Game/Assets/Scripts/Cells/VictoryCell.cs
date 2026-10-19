using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// The Victory Ground, where Easy and Medium end: a garden plaza at golden hour under a sky of fireworks. A giant
    /// gold stamp turns above the podium with the level's own stamps circling it, every country's banner flies on a
    /// ring of festoon lights, and three mini puzzles wait around the podium (Bell Echo, Lantern Row, Compass Dials).
    /// Each one solved lights a star on the podium; all three set off the grand finale. The results book by the
    /// arrival point ends the level at any time.
    /// </summary>
    public class VictoryCell : Cell
    {
        protected override void Build()
        {
            Id = CellId.VictoryGround;
            Lighting = new LightingPreset
            {
                SunColor = new Color(1f, 0.74f, 0.5f), SunIntensity = 1.25f, SunEuler = new Vector3(14f, 200f, 0f), ShadowStrength = 0.7f,
                AmbientSky = new Color(0.5f, 0.52f, 0.72f), AmbientEquator = new Color(0.68f, 0.52f, 0.44f), AmbientGround = new Color(0.28f, 0.26f, 0.2f),
                FogColor = new Color(0.86f, 0.62f, 0.58f), FogDensity = 0.009f,
                SkyZenith = new Color(0.22f, 0.26f, 0.58f), SkyHorizon = new Color(1f, 0.66f, 0.48f), SkyGround = new Color(0.45f, 0.38f, 0.34f),
                SunDiscColor = new Color(1f, 0.82f, 0.56f, 0.95f), SunSize = 0.01f, CloudColor = new Color(1f, 0.72f, 0.66f, 0.35f), Stars = 0.15f,
                PostExposure = 0.15f, Saturation = 14f, Contrast = 10f, ColorFilter = new Color(1f, 0.97f, 0.93f), Bloom = 1.0f, Vignette = 0.2f
            };
            Ambience = "amb_india_garden";
            Music = "mus_home";
            AddSun();

            var enc = gameObject.AddComponent<VictoryEncounter>();
            SetEncounter(enc);
            var g = Group("Ground");

            // Lawn, a stone plaza with a gold inlay ring, hedges and trees, hills in the haze.
            MeshFactory.Floor("Lawn", g, Vector3.zero, new Vector2(90f, 90f), Mats.Lit(new Color(0.62f, 0.82f, 0.42f), 0.2f, 0f, "grass_short"), "grass", 0.2f, 8f);
            var stone = Mats.Lit(new Color(0.9f, 0.84f, 0.74f), 0.35f, 0f, "tile_terrazzo");
            var gold = Mats.Lit(new Color(0.95f, 0.74f, 0.32f), 0.82f, 1f);
            MeshFactory.Cylinder("PlazaEdge", g, new Vector3(0, 0.002f, 0), 12.4f, 0.03f, Mats.Lit(new Color(0.7f, 0.64f, 0.56f), 0.3f), 64, false, 4f);
            MeshFactory.Cylinder("Plaza", g, new Vector3(0, 0.004f, 0), 12f, 0.035f, stone, 64, false, 4f);
            MeshFactory.Cylinder("InlayGold", g, new Vector3(0, 0.006f, 0), 6.4f, 0.036f, gold, 64, false);
            MeshFactory.Cylinder("InlayStone", g, new Vector3(0, 0.008f, 0), 6.1f, 0.036f, stone, 64, false, 2f);
            var hedge = Mats.Lit(new Color(0.2f, 0.38f, 0.2f), 0.15f);
            var leaf = Mats.Lit(new Color(0.3f, 0.48f, 0.22f), 0.15f);
            var bark = Mats.Lit(new Color(0.3f, 0.2f, 0.14f), 0.2f);
            for (int i = 0; i < 20; i++)
            {
                float a = i / 20f * Mathf.PI * 2f + 0.08f;
                var p = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * (24f + (i % 3) * 1.6f);
                float s = 0.85f + (i * 37 % 10) / 20f;
                MeshFactory.Cylinder("Trunk", g, p, 0.22f * s, 2.2f * s, bark, 8, true);
                var crown = MeshFactory.Sphere("Crown", g, p + Vector3.up * (3.2f * s), 1.9f * s, i % 2 == 0 ? leaf : hedge, 14, 10);
                crown.transform.localScale = new Vector3(1f, 1.15f, 1f);
            }
            for (int i = 0; i < 36; i++)
            {
                float a = i / 36f * Mathf.PI * 2f;
                var p = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 19f;
                MeshFactory.Box("Hedge", g, p + Vector3.up * 0.45f, new Vector3(3.2f, 0.9f, 0.9f), hedge, true, 1f, Quaternion.Euler(0, a * Mathf.Rad2Deg + 90f, 0));
            }
            var hill = Mats.Backdrop(new Color(0.42f, 0.4f, 0.5f), Lighting);
            for (int i = 0; i < 9; i++)
            {
                float a = i / 9f * Mathf.PI * 2f;
                var h = MeshFactory.Sphere("Hill", g, new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 120f, 38f + (i % 3) * 10f, hill, 16, 8);
                h.transform.localScale = new Vector3(1.6f, 0.35f + (i % 2) * 0.12f, 1f);
            }

            BuildPodium(enc, gold, stone);
            BuildBanners(enc);
            enc.Bells = BuildBells(enc, Station(new Vector3(-8.5f, 0, 4.5f)));
            enc.Lanterns = BuildLanterns(enc, Station(new Vector3(8.5f, 0, 4.5f)));
            enc.Dials = BuildDials(enc, Station(new Vector3(0f, 0, 10.5f)));
            BuildResultsBook(enc);

            enc.FireworksRoot = Group("Fireworks");
            SetArrival(new Vector3(0, 0, -11f), 0f);
            AddBounds(Vector3.zero, new Vector3(36f, 6f, 36f));
        }

        /// <summary>A station's root: at <paramref name="pos"/>, its +z pointing away from the podium so a player walking
        /// out from the centre reads its sign and faces its puzzle.</summary>
        Transform Station(Vector3 pos)
        {
            var t = Group("Station");
            t.localPosition = pos;
            var away = pos; away.y = 0f;
            t.localRotation = Quaternion.LookRotation(away.normalized, Vector3.up);
            MeshFactory.Cylinder("Dais", t, new Vector3(0, 0.01f, 0.2f), 2.9f, 0.08f, Mats.Lit(new Color(0.82f, 0.74f, 0.62f), 0.4f, 0f, "tile_terrazzo"), 40, false, 2f);
            return t;
        }

        static TextMeshPro Label(Transform parent, string text, float size, Color color, Vector3 pos, float width, bool serif = true)
        {
            var t = TextFactory.World(parent, text, size, color, TextAlignmentOptions.Center, serif, width);
            t.transform.localPosition = pos;
            return t;
        }

        static void Sign(Transform station, string title, string body, Color accent)
        {
            var wood = Mats.Lit(new Color(0.36f, 0.23f, 0.15f), 0.45f, 0f, "wood_dark");
            var root = new GameObject("Sign").transform;
            root.SetParent(station, false);
            root.localPosition = new Vector3(-2.35f, 0f, -1.2f);
            root.localRotation = Quaternion.Euler(0f, -18f, 0f);
            MeshFactory.Box("LegL", root, new Vector3(-0.6f, 0.55f, 0.04f), new Vector3(0.07f, 1.1f, 0.07f), wood, true);
            MeshFactory.Box("LegR", root, new Vector3(0.6f, 0.55f, 0.04f), new Vector3(0.07f, 1.1f, 0.07f), wood, true);
            MeshFactory.Box("Board", root, new Vector3(0f, 1.35f, 0.04f), new Vector3(1.5f, 0.85f, 0.05f), wood, false);
            MeshFactory.Box("Face", root, new Vector3(0f, 1.35f, 0.012f), new Vector3(1.38f, 0.74f, 0.01f), Mats.Lit(new Color(0.95f, 0.91f, 0.82f), 0.2f), false);
            MeshFactory.Box("Stripe", root, new Vector3(0f, 1.66f, 0.006f), new Vector3(1.38f, 0.1f, 0.006f), Mats.Lit(accent, 0.3f), false);
            Label(root, title.ToUpperInvariant(), 0.075f, Color.white, new Vector3(0f, 1.66f, 0f), 1.3f, false).fontStyle = FontStyles.Bold;
            Label(root, body, 0.052f, new Color(0.18f, 0.15f, 0.14f), new Vector3(0f, 1.32f, 0f), 1.26f);
        }

        /// <summary>A soft additive glow (halo behind the grand stamp and the orbiting stamps).</summary>
        public static Material AdditiveGlow(Color c)
        {
            var m = Mats.UnlitInstance(c, Tex.SoftDot, true, 1.5f);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            return m;
        }

        static Material Glow(Color c) => new Material(Mats.Emissive(c, Color.black, 0.6f));

        Interactable Usable(GameObject go, string prompt, float distance = 3.2f)
        {
            var it = go.AddComponent<Interactable>();
            it.Prompt = prompt;
            it.MaxDistance = distance;
            return it;
        }

        // ------------------------------------------------------------------ podium, grand stamp, stars
        void BuildPodium(VictoryEncounter enc, Material gold, Material stone)
        {
            var g = Group("Podium");
            var marble = Mats.Lit(new Color(0.96f, 0.93f, 0.88f), 0.6f);
            MeshFactory.Cylinder("Step1", g, Vector3.zero, 2.6f, 0.22f, stone, 48, true);
            MeshFactory.Cylinder("Step1Trim", g, new Vector3(0, 0.22f, 0), 2.62f, 0.04f, gold, 48, false);
            MeshFactory.Cylinder("Step2", g, new Vector3(0, 0.22f, 0), 2.0f, 0.3f, marble, 48, true);
            MeshFactory.Cylinder("Step2Trim", g, new Vector3(0, 0.52f, 0), 2.02f, 0.04f, gold, 48, false);
            MeshFactory.Cylinder("Plinth", g, new Vector3(0, 0.52f, 0), 1.3f, 0.5f, marble, 48, true);
            MeshFactory.Cylinder("PlinthTop", g, new Vector3(0, 1.02f, 0), 1.36f, 0.06f, gold, 48, false);
            AddPointLight(g, new Vector3(0, 2.6f, -1.8f), new Color(1f, 0.82f, 0.55f), 1.6f, 9f);

            // The grand stamp: a gold seal standing on edge, turning slowly above the plinth.
            var stampRoot = new GameObject("GrandStamp").transform;
            stampRoot.SetParent(g, false);
            stampRoot.localPosition = new Vector3(0, 2.75f, 0);
            enc.GrandStamp = stampRoot;
            var face = new GameObject("Face").transform;
            face.SetParent(stampRoot, false);
            MeshFactory.Cylinder("Disc", face, new Vector3(0, 0, -0.09f), 1.05f, 0.18f, gold, 48, false, 1f, Layers.Default).transform.localRotation = Quaternion.Euler(90f, 0, 0);
            var inner = Mats.Lit(new Color(0.7f, 0.18f, 0.12f), 0.55f, 0.2f);
            MeshFactory.Cylinder("Enamel", face, new Vector3(0, 0, -0.1f), 0.86f, 0.2f, inner, 48, false).transform.localRotation = Quaternion.Euler(90f, 0, 0);
            foreach (var side in new[] { 0f, 180f })
            {
                var s = new GameObject("Side").transform;
                s.SetParent(face, false);
                s.localRotation = Quaternion.Euler(0, side, 0);
                Label(s, "★", 0.9f, new Color(1f, 0.86f, 0.45f), new Vector3(0, 0.12f, -0.115f), 1.4f, false);
                Label(s, "VICTORY", 0.16f, new Color(1f, 0.92f, 0.7f), new Vector3(0, -0.42f, -0.115f), 1.4f, false).characterSpacing = 10f;
            }
            var halo = MeshFactory.Quad("Halo", stampRoot, 4.2f, 4.2f, AdditiveGlow(new Color(1f, 0.75f, 0.4f, 0.55f)), Vector3.zero);
            halo.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            enc.Halo = halo.transform;
            enc.StampLight = AddPointLight(stampRoot, Vector3.zero, new Color(1f, 0.78f, 0.45f), 2.2f, 10f);

            // Three stars on the podium's face, one per mini puzzle.
            enc.Stars = new List<TextMeshPro>();
            for (int i = 0; i < VictoryEncounter.PuzzleCount; i++)
            {
                float a = (i - 1) * 0.42f;
                var holder = new GameObject("Star").transform;
                holder.SetParent(g, false);
                holder.localPosition = new Vector3(Mathf.Sin(a) * 1.33f, 0.77f, -Mathf.Cos(a) * 1.33f);
                holder.localRotation = Quaternion.LookRotation(new Vector3(Mathf.Sin(a), 0, -Mathf.Cos(a)) * -1f, Vector3.up);
                var star = Label(holder, "★", 0.42f, new Color(0.45f, 0.42f, 0.4f), Vector3.zero, 0.5f, false);
                enc.Stars.Add(star);
            }
            enc.OrbitRoot = new GameObject("Orbit").transform;
            enc.OrbitRoot.SetParent(g, false);
            enc.OrbitRoot.localPosition = new Vector3(0, 2.3f, 0);

            // Slow searchlights sweeping up from the podium's foot.
            enc.Beams = new List<Transform>();
            Color[] beamColors = { Levels.Green, new Color(1f, 0.8f, 0.3f), Levels.Red, Levels.Blue };
            for (int i = 0; i < 4; i++)
            {
                var pivot = new GameObject("Beam").transform;
                pivot.SetParent(g, false);
                float a = i * 90f + 45f;
                pivot.localPosition = Quaternion.Euler(0, a, 0) * new Vector3(0, 0.25f, 2.7f);
                var c = beamColors[i];
                var cone = MeshFactory.Frustum("Cone", pivot, Vector3.zero, 0.25f, 2.2f, 38f, Mats.Unlit(new Color(c.r, c.g, c.b, 0.07f), null, true, 1.6f), 20);
                cone.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                MeshFactory.Cylinder("Lamp", pivot, new Vector3(0, -0.25f, 0), 0.3f, 0.25f, Mats.Emissive(new Color(0.2f, 0.2f, 0.2f), c * 2f), 16, false);
                enc.Beams.Add(pivot);
            }
        }

        // ------------------------------------------------------------------ banners and festoon lights
        void BuildBanners(VictoryEncounter enc)
        {
            var g = Group("Banners");
            var pole = Mats.Lit(new Color(0.85f, 0.78f, 0.62f), 0.7f, 0.8f);
            var bulb = Mats.Emissive(new Color(1f, 0.85f, 0.6f), new Color(2.4f, 1.7f, 0.9f));
            var wire = Mats.Lit(new Color(0.15f, 0.13f, 0.12f), 0.3f);
            enc.Flags = new List<Transform>();
            var countries = CountryDatabase.All;
            int n = countries.Count;
            const float R = 14.5f, top = 5.2f;
            var tops = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                var def = countries[i];
                float a = (i + 0.5f) / n * Mathf.PI * 2f;
                var p = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * R;
                tops[i] = p + Vector3.up * top;
                MeshFactory.Cylinder("Pole", g, p, 0.07f, top + 0.4f, pole, 10, true);
                MeshFactory.Sphere("Finial", g, p + Vector3.up * (top + 0.48f), 0.13f, pole, 10, 8);
                var hinge = new GameObject("Flag_" + def.StableId).transform;
                hinge.SetParent(g, false);
                hinge.localPosition = p + Vector3.up * (top - 0.15f);
                // The banner hangs on the inner side of the pole, facing the plaza.
                hinge.localRotation = Quaternion.LookRotation(p.normalized, Vector3.up);
                var swing = new GameObject("Swing").transform;
                swing.SetParent(hinge, false);
                MeshFactory.Box("Cloth", swing, new Vector3(0f, -1.15f, -0.34f), new Vector3(1.15f, 2.2f, 0.03f), Mats.Lit(def.Tint, 0.25f), false);
                MeshFactory.Box("Hem", swing, new Vector3(0f, -2.28f, -0.34f), new Vector3(1.15f, 0.12f, 0.035f), Mats.Lit(Color.Lerp(def.Tint, Color.black, 0.35f), 0.3f), false);
                var art = Tex.Art(def.StampTexture);
                if (art != null)
                    MeshFactory.Quad("Stamp", swing, 0.85f, 0.85f, Mats.Unlit(Color.white, art, true), new Vector3(0f, -0.95f, -0.36f));
                Label(swing, def.DisplayName.ToUpperInvariant(), 0.08f, Color.white, new Vector3(0f, -1.85f, -0.36f), 1.05f, false).fontStyle = FontStyles.Bold;
                // The banner hangs from a crossbar held out from the pole, clear of it while it sways.
                MeshFactory.Box("Arm", hinge, new Vector3(0f, 0.03f, -0.17f), new Vector3(0.04f, 0.04f, 0.32f), pole, false);
                MeshFactory.Box("Bar", swing, new Vector3(0f, 0.02f, -0.34f), new Vector3(1.3f, 0.045f, 0.045f), pole, false);
                enc.Flags.Add(swing);
            }
            // Festoons: warm bulbs on a sagging wire between neighbouring poles.
            for (int i = 0; i < n; i++)
            {
                var a = tops[i] + Vector3.up * 0.25f;
                var b = tops[(i + 1) % n] + Vector3.up * 0.25f;
                const int bulbs = 9;
                for (int k = 1; k < bulbs; k++)
                {
                    float t = k / (float)bulbs;
                    var p = Vector3.Lerp(a, b, t) + Vector3.down * (Mathf.Sin(t * Mathf.PI) * 0.9f);
                    MeshFactory.Sphere("Bulb", g, p, 0.07f, bulb, 8, 6);
                }
                var mid = Vector3.Lerp(a, b, 0.5f) + Vector3.down * 0.55f;
                foreach (var (from, to) in new[] { (a, mid), (mid, b) })
                {
                    var d = to - from;
                    MeshFactory.Box("Wire", g, (from + to) * 0.5f, new Vector3(0.012f, 0.012f, d.magnitude), wire, false, 1f, Quaternion.LookRotation(d));
                }
            }
            AddPointLight(g, new Vector3(0, 4.5f, 0), new Color(1f, 0.78f, 0.5f), 0.8f, 22f);
        }

        // ------------------------------------------------------------------ puzzle 1: Bell Echo
        List<VictoryEncounter.Bell> BuildBells(VictoryEncounter enc, Transform st)
        {
            Sign(st, "Bell Echo", "Press the gold button to hear the bells.\nThen ring them back in the same order.", new Color(0.8f, 0.45f, 0.12f));
            var wood = Mats.Lit(new Color(0.42f, 0.26f, 0.16f), 0.5f, 0f, "wood_dark");
            MeshFactory.Box("PostL", st, new Vector3(-1.75f, 1.35f, 0.4f), new Vector3(0.14f, 2.7f, 0.14f), wood, true);
            MeshFactory.Box("PostR", st, new Vector3(1.75f, 1.35f, 0.4f), new Vector3(0.14f, 2.7f, 0.14f), wood, true);
            MeshFactory.Box("Beam", st, new Vector3(0f, 2.7f, 0.4f), new Vector3(3.8f, 0.18f, 0.2f), wood, false);
            Color[] colors = { new Color(0.85f, 0.2f, 0.16f), new Color(0.95f, 0.66f, 0.12f), new Color(0.25f, 0.68f, 0.3f), new Color(0.22f, 0.48f, 0.85f) };
            var list = new List<VictoryEncounter.Bell>();
            for (int i = 0; i < 4; i++)
            {
                var hinge = new GameObject("Bell" + i).transform;
                hinge.SetParent(st, false);
                hinge.localPosition = new Vector3(-1.2f + i * 0.8f, 2.6f, 0.4f);
                MeshFactory.Cylinder("Rope", hinge, new Vector3(0, -0.45f, 0), 0.015f, 0.45f, wood, 6, false);
                var mat = Glow(colors[i]);
                var body = MeshFactory.Frustum("Body", hinge, new Vector3(0, -0.95f, 0), 0.3f, 0.12f, 0.5f, mat, 24, true);
                MeshFactory.Sphere("Crown", hinge, new Vector3(0, -0.45f, 0), 0.12f, mat, 12, 8);
                MeshFactory.Sphere("Clapper", hinge, new Vector3(0, -0.98f, 0), 0.07f, Mats.Lit(new Color(0.3f, 0.26f, 0.2f), 0.6f, 0.8f), 10, 8);
                var it = Usable(body, "Ring the bell");
                int idx = i;
                it.OnUse = who => enc.RingBell(idx, who);
                list.Add(new VictoryEncounter.Bell { Hinge = hinge, Mat = mat, Color = colors[i], Use = it, Pitch = new[] { 0.8f, 1.0f, 1.2f, 1.6f }[i] });
            }
            // The gold "listen" button on a short column in front of the frame.
            var col = Mats.Lit(new Color(0.96f, 0.93f, 0.88f), 0.6f);
            MeshFactory.Cylinder("Column", st, new Vector3(0f, 0f, -1.1f), 0.22f, 0.95f, col, 20, true);
            enc.ListenMat = Glow(new Color(1f, 0.78f, 0.3f));
            var btn = MeshFactory.Cylinder("Listen", st, new Vector3(0f, 0.95f, -1.1f), 0.14f, 0.08f, enc.ListenMat, 20, true);
            enc.ListenButton = Usable(btn, "Listen to the bells");
            enc.ListenButton.OnUse = who => enc.PlayBellSequence();
            return list;
        }

        // ------------------------------------------------------------------ puzzle 2: Lantern Row
        List<VictoryEncounter.Lantern> BuildLanterns(VictoryEncounter enc, Transform st)
        {
            Sign(st, "Lantern Row", "Light every lantern.\nEach one you touch flips itself\nand its neighbours.", new Color(0.86f, 0.56f, 0.12f));
            var iron = Mats.Lit(new Color(0.12f, 0.12f, 0.13f), 0.55f, 0.7f);
            var list = new List<VictoryEncounter.Lantern>();
            for (int i = 0; i < 5; i++)
            {
                float x = -2f + i;
                var root = new GameObject("Lantern" + i).transform;
                root.SetParent(st, false);
                root.localPosition = new Vector3(x, 0f, 0.5f + Mathf.Abs(x) * 0.25f);
                MeshFactory.Cylinder("Post", root, Vector3.zero, 0.05f, 1.5f, iron, 10, true);
                MeshFactory.Box("Arm", root, new Vector3(0, 1.52f, 0), new Vector3(0.36f, 0.04f, 0.36f), iron, false);
                var mat = Glow(new Color(0.95f, 0.88f, 0.7f));
                var glass = MeshFactory.Box("Glass", root, new Vector3(0, 1.78f, 0), new Vector3(0.3f, 0.44f, 0.3f), mat, true);
                MeshFactory.Box("Cap", root, new Vector3(0, 2.03f, 0), new Vector3(0.38f, 0.06f, 0.38f), iron, false);
                var flame = MeshFactory.Sphere("Flame", root, new Vector3(0, 1.76f, 0), 0.07f, Mats.Emissive(new Color(1f, 0.7f, 0.3f), new Color(3f, 1.6f, 0.5f)), 10, 8);
                flame.SetActive(false);
                var light = AddPointLight(root, new Vector3(0, 1.8f, 0), new Color(1f, 0.72f, 0.4f), 0f, 4.5f);
                var it = Usable(glass, "Flip the lantern");
                int idx = i;
                it.OnUse = who => enc.ToggleLantern(idx);
                list.Add(new VictoryEncounter.Lantern { Root = root, Mat = mat, Light = light, Flame = flame, Use = it });
            }
            return list;
        }

        // ------------------------------------------------------------------ puzzle 3: Compass Dials
        List<VictoryEncounter.Dial> BuildDials(VictoryEncounter enc, Transform st)
        {
            Sign(st, "Compass Dials", "Turn each dial until its arrow\npoints up at the star.", new Color(0.2f, 0.46f, 0.78f));
            var stoneMat = Mats.Lit(new Color(0.88f, 0.84f, 0.76f), 0.4f);
            var brass = Mats.Lit(new Color(0.85f, 0.66f, 0.32f), 0.75f, 0.9f);
            var arrowMat = Mats.Lit(new Color(0.55f, 0.1f, 0.08f), 0.5f);
            enc.DialStar = Label(st, "★", 0.6f, new Color(0.45f, 0.42f, 0.4f), new Vector3(0f, 2.75f, 0.3f), 0.6f, false);
            var list = new List<VictoryEncounter.Dial>();
            for (int i = 0; i < 3; i++)
            {
                var root = new GameObject("Dial" + i).transform;
                root.SetParent(st, false);
                root.localPosition = new Vector3(-1.6f + i * 1.6f, 0f, 0.4f);
                MeshFactory.Box("Pillar", root, new Vector3(0, 0.6f, 0.1f), new Vector3(0.3f, 1.2f, 0.3f), stoneMat, true);
                var rimMat = Glow(new Color(0.85f, 0.66f, 0.32f));
                MeshFactory.Cylinder("Rim", root, new Vector3(0, 1.6f, 0.06f), 0.56f, 0.08f, rimMat, 32, false).transform.localRotation = Quaternion.Euler(-90f, 0, 0);
                var spinner = new GameObject("Spinner").transform;
                spinner.SetParent(root, false);
                spinner.localPosition = new Vector3(0, 1.6f, 0f);
                var disc = MeshFactory.Cylinder("Disc", spinner, Vector3.zero, 0.48f, 0.1f, stoneMat, 32, true);
                disc.transform.localRotation = Quaternion.Euler(-90f, 0, 0);
                MeshFactory.Box("Shaft", spinner, new Vector3(0, 0.08f, -0.11f), new Vector3(0.07f, 0.48f, 0.02f), arrowMat, false);
                MeshFactory.Box("HeadL", spinner, new Vector3(-0.07f, 0.26f, -0.11f), new Vector3(0.05f, 0.2f, 0.02f), arrowMat, false, 1f, Quaternion.Euler(0, 0, -35f));
                MeshFactory.Box("HeadR", spinner, new Vector3(0.07f, 0.26f, -0.11f), new Vector3(0.05f, 0.2f, 0.02f), arrowMat, false, 1f, Quaternion.Euler(0, 0, 35f));
                MeshFactory.Cylinder("Hub", spinner, new Vector3(0, 0, -0.06f), 0.07f, 0.08f, brass, 12, false).transform.localRotation = Quaternion.Euler(-90f, 0, 0);
                var it = Usable(disc, "Turn the dial");
                int idx = i;
                it.OnUse = who => enc.TurnDial(idx);
                list.Add(new VictoryEncounter.Dial { Spinner = spinner, RimMat = rimMat, Use = it });
            }
            return list;
        }

        // ------------------------------------------------------------------ results book
        void BuildResultsBook(VictoryEncounter enc)
        {
            var g = Group("ResultsBook");
            g.localPosition = new Vector3(0f, 0f, -7.2f);
            g.localRotation = Quaternion.Euler(0f, 0f, 0f);
            var wood = Mats.Lit(new Color(0.36f, 0.23f, 0.15f), 0.45f, 0f, "wood_dark");
            MeshFactory.Cylinder("Stand", g, Vector3.zero, 0.1f, 1.0f, wood, 12, true);
            MeshFactory.Box("Foot", g, new Vector3(0, 0.03f, 0), new Vector3(0.5f, 0.06f, 0.5f), wood, false);
            var top = MeshFactory.Box("Desk", g, new Vector3(0, 1.05f, 0), new Vector3(0.62f, 0.05f, 0.44f), wood, true, 1f, Quaternion.Euler(-20f, 0, 0));
            var book = MeshFactory.Box("Book", g, new Vector3(0, 1.1f, 0.01f), new Vector3(0.5f, 0.04f, 0.34f), Mats.Lit(new Color(0.55f, 0.1f, 0.08f), 0.4f), true, 1f, Quaternion.Euler(-20f, 0, 0));
            MeshFactory.Box("Pages", g, new Vector3(0, 1.125f, 0.01f), new Vector3(0.46f, 0.012f, 0.3f), Mats.Lit(new Color(0.96f, 0.93f, 0.85f), 0.2f), false, 1f, Quaternion.Euler(-20f, 0, 0));
            var label = Label(g, "RESULTS", 0.06f, new Color(0.55f, 0.1f, 0.08f), new Vector3(0, 1.45f, 0.05f), 0.8f, false);
            label.fontStyle = FontStyles.Bold;
            label.characterSpacing = 6f;
            enc.BookLabel = label.transform;
            enc.Book = Usable(book, "See your results and finish the level", 3.4f);
            enc.Book.OnUse = who => Game.Story?.ShowLevelResults();
            _ = top;
        }
    }

    /// <summary>The Victory Ground's logic: the three mini puzzles, the stars, the fireworks and every idle animation.</summary>
    public class VictoryEncounter : Encounter
    {
        public const int PuzzleCount = 3;

        public class Bell { public Transform Hinge; public Material Mat; public Color Color; public Interactable Use; public float Pitch; public float Swing, Flash; }
        public class Lantern { public Transform Root; public Material Mat; public Light Light; public GameObject Flame; public Interactable Use; public bool On; public float Level; }
        public class Dial { public Transform Spinner; public Material RimMat; public Interactable Use; public int Steps; public float Angle; }

        public List<Bell> Bells;
        public List<Lantern> Lanterns;
        public List<Dial> Dials;
        public Interactable ListenButton, Book;
        public Material ListenMat;
        public TextMeshPro DialStar;
        public List<TextMeshPro> Stars;
        public Transform GrandStamp, Halo, OrbitRoot, FireworksRoot, BookLabel;
        public Light StampLight;
        public List<Transform> Beams, Flags;

        public bool BellsSolved { get; private set; }
        public bool LanternsSolved { get; private set; }
        public bool DialsSolved { get; private set; }
        public int Solved => (BellsSolved ? 1 : 0) + (LanternsSolved ? 1 : 0) + (DialsSolved ? 1 : 0);
        public bool FinaleDone { get; private set; }

        /// <summary>The order the bells must be rung back in (tests read it).</summary>
        public int[] BellSequence { get; private set; } = { 0, 2, 1, 3 };
        public bool BellsListening => m_BellInput >= 0;

        int m_BellInput = -1;
        bool m_BellsPlaying;
        float m_NextRocket, m_T;
        readonly List<Transform> m_Orbit = new List<Transform>();
        readonly float[] m_StarPop = new float[PuzzleCount];

        public override string Objective =>
            Solved < PuzzleCount
                ? "Victory Ground: solve the mini puzzles (" + Solved + " / " + PuzzleCount + "): Bell Echo on the left, Lantern Row on the right, Compass Dials behind the podium. The results book finishes the level."
                : "Every mini puzzle solved. Read the results book to finish the level.";

        // ------------------------------------------------------------------ arrival
        public override void OnEnter(bool firstVisit)
        {
            var story = Game.Story;
            if (story != null) { story.TearsAllowed = false; story.ThreatAllowed = false; }
            Game.Passenger?.Despawn();
            Game.Passport?.Close();
            if (Game.Passport != null) Game.Passport.Available = false;
            ResetPuzzles();
            BuildOrbit();
            var def = Levels.Current;
            Hud.I?.ShowTitleCard("VICTORY GROUND", (def != null ? def.Label + " complete" : "Well travelled") + "  ·  three little puzzles wait", 4.5f);
            Hud.I?.ShowToast("Solve the three mini puzzles for the grand finale, or read the results book to finish.", 5f);
            AudioDirector.I?.SetMusic("mus_home", 2f, 0.8f);
            Game.Story?.UpdateObjective();
            StartCoroutine(Welcome());
        }

        IEnumerator Welcome()
        {
            var rig = Game.Rig;
            yield return new WaitForSeconds(0.4f);
            if (rig != null) Fireworks.Confetti(Cell.transform, rig.Feet + rig.Forward * 2f, 3.5f, 280, 5f);
            AudioDirector.I?.Play("seal_verify", null, 0.8f, 1.2f);
            Color[] c = { Levels.Green, new Color(1f, 0.8f, 0.3f), Levels.Red, Levels.Blue, new Color(1f, 0.5f, 0.8f) };
            for (int i = 0; i < 5; i++)
            {
                Launch(c[i], 1f);
                yield return new WaitForSeconds(0.35f);
            }
            m_NextRocket = Time.time + 4f;
        }

        public override void ResetLocal() => ResetPuzzles();

        void ResetPuzzles()
        {
            StopAllCoroutines();
            BellsSolved = LanternsSolved = DialsSolved = FinaleDone = false;
            m_BellInput = -1;
            m_BellsPlaying = false;
            var seqs = new[] { new[] { 0, 2, 1, 3 }, new[] { 3, 1, 0, 2 }, new[] { 1, 3, 2, 0 }, new[] { 2, 0, 3, 1 } };
            BellSequence = seqs[Random.Range(0, seqs.Length)];
            if (Lanterns != null) foreach (var l in Lanterns) l.On = false;
            if (Dials != null)
                for (int i = 0; i < Dials.Count; i++) { Dials[i].Steps = i + 1; Dials[i].Angle = DialTarget(Dials[i]); }
            for (int i = 0; i < m_StarPop.Length; i++) m_StarPop[i] = 0f;
            if (Stars != null) foreach (var s in Stars) { s.color = new Color(0.45f, 0.42f, 0.4f); s.transform.localScale = Vector3.one; }
            if (DialStar != null) DialStar.color = new Color(0.45f, 0.42f, 0.4f);
            if (Bells != null) foreach (var b in Bells) b.Use.Usable = true;
            if (Lanterns != null) foreach (var l in Lanterns) l.Use.Usable = true;
            if (Dials != null) foreach (var d in Dials) d.Use.Usable = true;
            if (ListenButton != null) ListenButton.Usable = true;
        }

        /// <summary>The level's stamps circle the grand stamp (rebuilt for each run: Easy and Medium hold different ones).</summary>
        void BuildOrbit()
        {
            foreach (var t in m_Orbit) if (t != null) Destroy(t.gameObject);
            m_Orbit.Clear();
            var ids = Game.Campaign != null && Game.Campaign.IsLevelRun ? Game.Campaign.levelCountries : new List<string> { "uae" };
            foreach (var sid in ids)
            {
                var def = CountryDatabase.ByStableId(sid);
                if (def == null) continue;
                var tex = Tex.Art(def.StampTexture);
                var root = new GameObject("Orbit_" + sid).transform;
                root.SetParent(OrbitRoot, false);
                var glow = MeshFactory.Quad("Glow", root, 1.5f, 1.5f, VictoryCell.AdditiveGlow(new Color(def.Tint.r, def.Tint.g, def.Tint.b, 0.5f)), new Vector3(0, 0, 0.02f));
                glow.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (tex != null)
                {
                    var m = Mats.Unlit(Color.white, tex, true);
                    MeshFactory.Quad("Front", root, 0.85f, 0.85f, m, Vector3.zero);
                    MeshFactory.Quad("Back", root, 0.85f, 0.85f, m, Vector3.zero, Quaternion.Euler(0, 180f, 0));
                }
                m_Orbit.Add(root);
            }
            Cell.Recache();
        }

        // ------------------------------------------------------------------ Bell Echo
        public void PlayBellSequence()
        {
            if (BellsSolved || m_BellsPlaying) return;
            StartCoroutine(BellSequenceRoutine());
        }

        IEnumerator BellSequenceRoutine()
        {
            m_BellsPlaying = true;
            m_BellInput = -1;
            ListenFlash = 1f;
            yield return new WaitForSeconds(0.5f);
            foreach (var i in BellSequence)
            {
                Strike(i, 1f);
                yield return new WaitForSeconds(0.75f);
            }
            m_BellsPlaying = false;
            m_BellInput = 0;
            Hud.I?.ShowToast("Your turn: ring the bells back in the same order.", 2.5f);
        }

        float ListenFlash;

        public void RingBell(int i, Interactor who)
        {
            if (m_BellsPlaying || i < 0 || Bells == null || i >= Bells.Count) return;
            Strike(i, 1f);
            if (who != null) AudioDirector.Haptic(who.Side, 0.4f, 0.06f);
            if (BellsSolved || m_BellInput < 0) return;
            if (BellSequence[m_BellInput] == i)
            {
                m_BellInput++;
                if (m_BellInput >= BellSequence.Length)
                {
                    m_BellInput = -1;
                    BellsSolved = true;
                    foreach (var b in Bells) b.Use.Usable = false;
                    ListenButton.Usable = false;
                    StartCoroutine(BellCelebration());
                    OnSolved(0, "Bell Echo", Bells[1].Hinge.position + Vector3.up * 2f);
                }
            }
            else
            {
                m_BellInput = -1;
                AudioDirector.I?.Play("ui_back", Bells[i].Hinge.position, 0.9f, 0.6f);
                foreach (var b in Bells) b.Flash = -1f; // a red flicker on every bell
                Hud.I?.ShowToast("Not quite. Listen again.", 2f);
                StartCoroutine(ReplayAfter(1.4f));
            }
        }

        IEnumerator ReplayAfter(float s)
        {
            yield return new WaitForSeconds(s);
            PlayBellSequence();
        }

        IEnumerator BellCelebration()
        {
            for (int k = 0; k < 2; k++)
                for (int i = 0; i < Bells.Count; i++) { Strike(i, 0.7f); yield return new WaitForSeconds(0.12f); }
        }

        void Strike(int i, float volume)
        {
            var b = Bells[i];
            b.Swing = 1f;
            b.Flash = 1f;
            AudioDirector.I?.Play("clock_chime", b.Hinge.position + Vector3.down, 0.55f * volume, b.Pitch, Bus.SFX, false, null, 4f);
            AudioDirector.I?.Play("pp_tab", b.Hinge.position + Vector3.down, 0.6f * volume, b.Pitch * 1.5f);
        }

        // ------------------------------------------------------------------ Lantern Row
        public void ToggleLantern(int i)
        {
            if (LanternsSolved || Lanterns == null) return;
            for (int k = i - 1; k <= i + 1; k++)
                if (k >= 0 && k < Lanterns.Count) Lanterns[k].On = !Lanterns[k].On;
            AudioDirector.I?.Play("lantern_place", Lanterns[i].Root.position + Vector3.up * 1.7f, 0.7f, 0.9f + i * 0.08f);
            AudioDirector.I?.Play("sunbeam_on", Lanterns[i].Root.position + Vector3.up * 1.7f, 0.35f, 1.4f);
            if (Lanterns.TrueForAll(l => l.On))
            {
                LanternsSolved = true;
                foreach (var l in Lanterns) l.Use.Usable = false;
                OnSolved(1, "Lantern Row", Lanterns[2].Root.position + Vector3.up * 3f);
            }
        }

        public bool LanternOn(int i) => Lanterns[i].On;

        // ------------------------------------------------------------------ Compass Dials
        static float DialTarget(Dial d) => -90f * d.Steps;

        public void TurnDial(int i)
        {
            if (DialsSolved || Dials == null) return;
            var d = Dials[i];
            d.Steps = (d.Steps + 1) % 4;
            AudioDirector.I?.Play("dial_click", d.Spinner.position, 0.8f, 0.9f + d.Steps * 0.06f);
            if (Dials.TrueForAll(x => x.Steps == 0))
            {
                DialsSolved = true;
                foreach (var x in Dials) x.Use.Usable = false;
                OnSolved(2, "Compass Dials", Dials[1].Spinner.position + Vector3.up * 2.5f);
            }
        }

        public int DialSteps(int i) => Dials[i].Steps;

        // ------------------------------------------------------------------ solving and the finale
        void OnSolved(int star, string name, Vector3 at)
        {
            m_StarPop[star] = 1f;
            AudioDirector.I?.Play("seal_verify", null, 0.9f, 1.1f + star * 0.1f);
            PostFx.I?.Flash(0.35f);
            Hud.I?.ShowToast("Mini puzzle solved: " + name + "   (" + Solved + " / " + PuzzleCount + ")", 3.5f);
            Fireworks.Burst(Cell.transform, at, new Color(1f, 0.82f, 0.4f), 0.55f);
            Fireworks.Confetti(Cell.transform, at - Vector3.up * 2f, 1.8f, 120, 3f);
            for (int i = 0; i < 3; i++) Launch(i == 0 ? Levels.Green : i == 1 ? Levels.Blue : Levels.Red, 1f, at);
            Game.Story?.UpdateObjective();
            if (Solved == PuzzleCount) StartCoroutine(Finale());
        }

        IEnumerator Finale()
        {
            yield return new WaitForSeconds(1.5f);
            Hud.I?.ShowTitleCard("GRAND FINALE", "Every puzzle solved", 4f);
            AudioDirector.I?.SetMusic("mus_reveal", 0.5f, 0.9f, false);
            var rig = Game.Rig;
            Color[] c = { Levels.Green, new Color(1f, 0.8f, 0.3f), Levels.Red, Levels.Blue, new Color(1f, 0.5f, 0.8f), new Color(0.7f, 0.4f, 1f) };
            for (int i = 0; i < 16; i++)
            {
                Launch(c[i % c.Length], i >= 12 ? 1.35f : 1f);
                if (i % 5 == 0 && rig != null) Fireworks.Confetti(Cell.transform, rig.Feet, 4f, 200, 6f);
                yield return new WaitForSeconds(i < 12 ? 0.42f : 0.2f);
            }
            yield return new WaitForSeconds(2.5f);
            FinaleDone = true;
            AudioDirector.I?.SetMusic("mus_home", 3f, 0.8f);
            Game.Story?.ShowLevelResults();
        }

        void Launch(Color color, float scale, Vector3? near = null)
        {
            if (Cell == null || FireworksRoot == null) return;
            float a = Random.value * Mathf.PI * 2f;
            var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            var from = Cell.transform.position + dir * Random.Range(26f, 32f);
            Vector3 at;
            if (near.HasValue) at = near.Value + new Vector3(Random.Range(-4f, 4f), Random.Range(10f, 15f), Random.Range(-4f, 4f));
            else
            {
                // Burst where the player can see it: in front of them, high over the plaza.
                var rig = Game.Rig;
                var fwd = rig != null ? rig.Forward : Vector3.forward;
                var origin = rig != null ? rig.Feet : Cell.transform.position;
                at = origin + fwd * Random.Range(14f, 24f) + Vector3.Cross(Vector3.up, fwd) * Random.Range(-12f, 12f) + Vector3.up * Random.Range(14f, 24f);
                from = new Vector3(at.x, Cell.transform.position.y, at.z) + dir * 6f;
            }
            StartCoroutine(Fireworks.Rocket(FireworksRoot, from, at, color, scale));
        }

        // ------------------------------------------------------------------ idle animation
        void Update()
        {
            if (Cell == null || Cell.Mode != CellMode.Active) return;
            float dt = Time.deltaTime;
            m_T += dt;

            if (GrandStamp != null)
            {
                GrandStamp.localRotation = Quaternion.Euler(0f, m_T * 24f, 0f);
                GrandStamp.localPosition = new Vector3(0f, 2.75f + Mathf.Sin(m_T * 1.1f) * 0.12f, 0f);
            }
            var cam = Game.Rig != null ? Game.Rig.Head : null;
            if (Halo != null && cam != null)
            {
                Halo.rotation = Quaternion.LookRotation(Halo.position - cam.position, Vector3.up);
                Halo.localScale = Vector3.one * (1f + Mathf.Sin(m_T * 2.2f) * 0.06f + Solved * 0.08f);
            }
            if (StampLight != null) StampLight.intensity = 2.2f + Solved * 0.6f + Mathf.Sin(m_T * 2.2f) * 0.3f;

            // The level's stamps circle the grand stamp, each bobbing on its own beat and facing outwards.
            for (int i = 0; i < m_Orbit.Count; i++)
            {
                if (m_Orbit[i] == null) continue;
                float a = m_T * 0.45f + i * Mathf.PI * 2f / m_Orbit.Count;
                var p = new Vector3(Mathf.Sin(a) * 3.3f, Mathf.Sin(m_T * 1.3f + i) * 0.25f, Mathf.Cos(a) * 3.3f);
                m_Orbit[i].localPosition = p;
                m_Orbit[i].localRotation = Quaternion.LookRotation(new Vector3(p.x, 0f, p.z).normalized, Vector3.up) * Quaternion.Euler(0f, 0f, Mathf.Sin(m_T + i) * 8f);
            }
            if (Beams != null)
                for (int i = 0; i < Beams.Count; i++)
                    Beams[i].localRotation = Quaternion.Euler(0f, i * 90f + 45f + m_T * 14f, 0f) * Quaternion.Euler(18f + Mathf.Sin(m_T * 0.5f + i) * 10f, 0f, 0f);
            if (Flags != null)
                for (int i = 0; i < Flags.Count; i++)
                    Flags[i].localRotation = Quaternion.Euler(Mathf.Sin(m_T * 1.6f + i * 0.7f) * 3f, Mathf.Sin(m_T * 0.9f + i) * 6f, Mathf.Sin(m_T * 1.2f + i * 1.3f) * 2f);
            if (BookLabel != null)
            {
                BookLabel.localPosition = new Vector3(0f, 1.45f + Mathf.Sin(m_T * 2f) * 0.03f, 0.05f);
                if (cam != null) BookLabel.rotation = Quaternion.LookRotation(BookLabel.position - cam.position, Vector3.up);
            }

            // Bells swing and glow after a strike; a wrong ring flickers red.
            if (Bells != null)
                foreach (var b in Bells)
                {
                    b.Swing = Mathf.MoveTowards(b.Swing, 0f, dt * 0.9f);
                    b.Hinge.localRotation = Quaternion.Euler(Mathf.Sin(m_T * 9f) * 16f * b.Swing * b.Swing, 0f, 0f);
                    Color glow;
                    if (b.Flash < 0f) { b.Flash = Mathf.MoveTowards(b.Flash, 0f, dt * 1.5f); glow = new Color(2.5f, 0.2f, 0.1f) * (Mathf.Repeat(m_T * 8f, 1f) > 0.5f ? -b.Flash : 0f); }
                    else { b.Flash = Mathf.MoveTowards(b.Flash, 0f, dt * 1.6f); glow = b.Color * (b.Flash * 3f + (BellsSolved ? 0.8f + Mathf.Sin(m_T * 3f) * 0.3f : 0f)); }
                    b.Mat.SetColor("_EmissionColor", glow);
                }
            if (ListenMat != null)
            {
                ListenFlash = Mathf.MoveTowards(ListenFlash, 0f, dt * 1.5f);
                float idle = !BellsSolved && !m_BellsPlaying && m_BellInput < 0 ? 0.6f + Mathf.Sin(m_T * 4f) * 0.4f : 0.1f;
                ListenMat.SetColor("_EmissionColor", new Color(1f, 0.7f, 0.25f) * (idle + ListenFlash * 2f));
            }

            // Lanterns ease on and off; lit ones flicker gently.
            if (Lanterns != null)
                foreach (var l in Lanterns)
                {
                    l.Level = Mathf.MoveTowards(l.Level, l.On ? 1f : 0f, dt * 3f);
                    float flicker = 1f + (Mathf.PerlinNoise(m_T * 6f, l.Root.position.x) - 0.5f) * 0.25f;
                    l.Light.intensity = 1.8f * l.Level * flicker * (LanternsSolved ? 1.25f : 1f);
                    l.Mat.SetColor("_EmissionColor", new Color(2.2f, 1.4f, 0.55f) * l.Level * flicker);
                    if (l.Flame.activeSelf != l.Level > 0.2f) l.Flame.SetActive(l.Level > 0.2f);
                }

            // Dials turn with a small overshoot; a dial on target glows on its rim.
            if (Dials != null)
            {
                foreach (var d in Dials)
                {
                    float target = DialTarget(d);
                    float diff = Mathf.DeltaAngle(d.Angle, target);
                    d.Angle += diff * Mathf.Min(1f, dt * 9f);
                    d.Spinner.localRotation = Quaternion.Euler(0f, 0f, d.Angle);
                    bool on = d.Steps == 0;
                    d.RimMat.SetColor("_EmissionColor", on ? new Color(0.3f, 1.6f, 0.6f) * (DialsSolved ? 1.2f + Mathf.Sin(m_T * 3f) * 0.3f : 0.9f) : Color.black);
                }
                if (DialStar != null)
                {
                    DialStar.color = DialsSolved ? Color.Lerp(new Color(1f, 0.8f, 0.3f), Color.white, Mathf.Sin(m_T * 3f) * 0.3f + 0.3f) : new Color(0.45f, 0.42f, 0.4f);
                    DialStar.transform.localRotation = Quaternion.Euler(0f, 0f, DialsSolved ? m_T * 40f : 0f);
                }
            }

            // Podium stars pop to gold when their puzzle is solved.
            if (Stars != null)
            {
                bool[] solved = { BellsSolved, LanternsSolved, DialsSolved };
                for (int i = 0; i < Stars.Count; i++)
                {
                    m_StarPop[i] = Mathf.MoveTowards(m_StarPop[i], 0f, dt * 1.4f);
                    float pop = m_StarPop[i];
                    Stars[i].transform.localScale = Vector3.one * (1f + Mathf.Sin(pop * Mathf.PI) * 0.8f);
                    Stars[i].color = solved[i] ? Color.Lerp(new Color(1f, 0.78f, 0.25f), Color.white, pop + Mathf.Sin(m_T * 2f + i) * 0.15f + 0.1f) : new Color(0.45f, 0.42f, 0.4f);
                }
            }

            // A firework every few seconds while the player is here.
            if (Entered && !Game.Paused && Time.time > m_NextRocket)
            {
                m_NextRocket = Time.time + Random.Range(2.8f, 5.5f);
                Color[] c = { Levels.Green, new Color(1f, 0.8f, 0.3f), Levels.Red, Levels.Blue, new Color(1f, 0.5f, 0.8f) };
                Launch(c[Random.Range(0, c.Length)], Random.Range(0.8f, 1.1f));
            }
        }
    }
}
