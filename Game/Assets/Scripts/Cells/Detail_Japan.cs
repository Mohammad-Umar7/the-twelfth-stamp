using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    // Place detail for Japan (playtest: "the places look cheap and bare"). A wet night street of Kyoto-style machiya, every
    // front different: koshi lattices over lit paper, noren in the doorways, pent roofs with rafters and round eave tiles,
    // plastered upper floors with slatted windows, ridged main roofs with end tiles; chochin, signboards, poles and sagging
    // wires, vending machines, a bicycle and pots (Detail_JapanProps.cs). Parts bake per material in four groups (street side
    // x near/far half) so the forward renderer's eight lights per object stay local. No shrine gates or sacred objects; shop
    // words are neutral. The far vista and the pagoda precinct beyond the end gate are built in Landmarks.cs / Vista.cs.
    public partial class JapanCell
    {
        enum HouseKind { Modern, Koshi, KoshiBlind, Soba, Teahouse, Shoji, Shuttered, ShutterShop }

        /// <summary>
        /// The houses: side, centre z, kind, main eave height, noren colour (navy, crimson, cream), red chochin, lit. The first
        /// eight line the playable street (Build gives them colliders); the last two carry it on into the dark behind the arrival.
        /// </summary>
        static readonly (int side, float z, HouseKind kind, float eave, int noren, bool red, bool lit)[] s_Houses =
        {
            (-1, -5.0f, HouseKind.Modern, 5.05f, 0, false, true),
            (-1, -0.4f, HouseKind.Koshi, 5.35f, 0, false, true),
            (-1, 4.2f, HouseKind.Soba, 5.2f, 0, true, true),
            (-1, 8.8f, HouseKind.Shuttered, 5.5f, 2, false, true),
            (1, -5.0f, HouseKind.ShutterShop, 5.15f, 0, false, true),
            (1, -0.4f, HouseKind.Shoji, 5.45f, 2, false, true),
            (1, 4.2f, HouseKind.Teahouse, 5.3f, 2, true, true),
            (1, 8.8f, HouseKind.KoshiBlind, 5.15f, 1, false, true),
            (-1, -9.6f, HouseKind.Koshi, 5.3f, 1, false, false),
            (1, -9.6f, HouseKind.Shuttered, 5.2f, 0, false, false),
        };

        /// <summary>
        /// The street's materials. The project renders in linear space, so a textured material shows its texture times the
        /// tint: tints stay near white and the texture carries the colour (dark tints on dark textures went black).
        /// </summary>
        sealed class JapanMats
        {
            public Material Timber, Weathered, Bengara, Board, Plaster, Earth, Tile, PaperDim, Bamboo, Stone, Strip,
                Metal, Black, Glass, Pane, Frosted, Pot, PotBlue, Leaf, RedPaint, Plastic;
            public Material[] Cloth;                                                            // navy, crimson, cream
            public Material PaperGlow, PaperFaint, Interior, LanternCream, LanternRed, Bulb, Display, Buttons; // glow batch
            public Material[] Goods;                                                            // softly self-lit stock
            public Material Shadow;                                                             // shadows on lit paper
        }

        static JapanMats StreetMats() => new JapanMats
        {
            Timber = Mats.Lit(new Color(0.8f, 0.78f, 0.76f), 0.35f, 0f, "wood_dark"),
            Weathered = Mats.Lit(new Color(0.7f, 0.84f, 1f), 0.25f, 0f, "wood_dark"),
            Bengara = Mats.Lit(new Color(1f, 0.55f, 0.48f), 0.3f, 0f, "wood_dark"),
            Board = Mats.Lit(new Color(0.72f, 0.7f, 0.7f), 0.35f, 0f, "wood_oak_floor"),
            Plaster = Mats.Lit(new Color(0.95f, 0.94f, 0.92f), 0.15f, 0f, "plaster_warm"),
            Earth = Mats.Lit(new Color(0.74f, 0.62f, 0.5f), 0.15f, 0f, "plaster_warm"),
            Tile = Mats.Lit(new Color(0.82f, 0.84f, 0.9f), 0.55f, 0.3f, "stone_dark_slate"),
            PaperDim = Mats.Lit(new Color(0.7f, 0.67f, 0.62f), 0.1f, 0f, "paper_shoji", false),
            Bamboo = Mats.Lit(new Color(0.82f, 0.84f, 0.8f), 0.45f, 0f, "wood_oak_floor"),
            Stone = Mats.Lit(new Color(0.95f, 0.95f, 0.95f), 0.45f, 0f, "granite_pavers"),
            Strip = Mats.Lit(new Color(0.95f, 0.95f, 0.95f), 0.62f, 0f, "cobble_wet"),
            Metal = Mats.Lit(new Color(0.9f, 0.9f, 0.92f), 0.55f, 0.6f, "metal_brushed"),
            Black = Mats.Lit(new Color(0.03f, 0.03f, 0.035f), 0.4f),
            Glass = Mats.Lit(new Color(0.05f, 0.06f, 0.07f), 0.92f, 0.3f),
            Pane = Mats.Glass(new Color(0.65f, 0.75f, 0.8f, 0.1f)),
            Frosted = Mats.Lit(new Color(0.66f, 0.68f, 0.7f), 0.6f),
            Pot = Mats.Lit(new Color(0.32f, 0.18f, 0.12f), 0.55f),
            PotBlue = Mats.Lit(new Color(0.14f, 0.22f, 0.38f), 0.75f),
            Leaf = Mats.Lit(new Color(0.8f, 0.85f, 0.8f), 0.2f, 0f, "grass_short"),
            RedPaint = Mats.Lit(new Color(0.62f, 0.09f, 0.07f), 0.5f),
            Plastic = Mats.Lit(new Color(0.15f, 0.32f, 0.62f), 0.45f),
            Cloth = new[]
            {
                Mats.Lit(new Color(0.19f, 0.27f, 0.58f), 0.2f, 0f, "fabric_linen"),
                Mats.Lit(new Color(0.66f, 0.15f, 0.15f), 0.2f, 0f, "fabric_linen"),
                Mats.Lit(new Color(1f, 1f, 0.98f), 0.2f, 0f, "fabric_linen"),
            },
            PaperGlow = Mats.Lit(new Color(0.95f, 0.9f, 0.8f), 0.1f, 0f, "paper_shoji", false, new Color(0.62f, 0.5f, 0.34f)),
            PaperFaint = Mats.Lit(new Color(0.75f, 0.7f, 0.62f), 0.1f, 0f, "paper_shoji", false, new Color(0.36f, 0.28f, 0.18f)),
            Interior = Mats.Emissive(new Color(0.42f, 0.32f, 0.22f), new Color(0.45f, 0.34f, 0.22f)),
            LanternCream = Mats.Lit(new Color(0.95f, 0.88f, 0.72f), 0.1f, 0f, "paper_shoji", false, new Color(1.1f, 0.85f, 0.6f)),
            LanternRed = Mats.Lit(new Color(0.8f, 0.2f, 0.12f), 0.1f, 0f, "paper_shoji", false, new Color(1.05f, 0.38f, 0.22f)),
            Bulb = Mats.Emissive(new Color(1f, 0.92f, 0.75f), new Color(1.2f, 1f, 0.75f)),
            Display = Mats.Emissive(new Color(0.8f, 0.84f, 0.88f), new Color(0.8f, 0.86f, 0.9f)),
            Buttons = Mats.Emissive(new Color(0.4f, 0.9f, 0.6f), new Color(0.3f, 0.75f, 0.45f)),
            Goods = new[]
            {
                Mats.Emissive(new Color(0.72f, 0.12f, 0.1f), new Color(0.4f, 0.1f, 0.08f)),
                Mats.Emissive(new Color(0.16f, 0.32f, 0.7f), new Color(0.12f, 0.22f, 0.45f)),
                Mats.Emissive(new Color(0.3f, 0.6f, 0.28f), new Color(0.2f, 0.4f, 0.18f)),
                Mats.Emissive(new Color(0.88f, 0.62f, 0.2f), new Color(0.5f, 0.36f, 0.12f)),
            },
            Shadow = Mats.Unlit(new Color(0.05f, 0.025f, 0.015f, 0.68f), null, true),
        };

        /// <summary>Batches for one build: four lit groups, one glow batch (emission-led, no shadows) and the paper shadows.</summary>
        sealed class StreetCtx
        {
            public JapanMats M;
            public Transform Root;
            public readonly KitBatch[] Groups = { new KitBatch(), new KitBatch(), new KitBatch(), new KitBatch() };
            public readonly KitBatch Glow = new KitBatch();
            public readonly MeshBuilder Shade = new MeshBuilder();
            /// <summary>The batch for a spot: street side x near (z &lt; 1.9) or far half, so each mesh's lights stay local.</summary>
            public KitBatch At(float x, float z) => Groups[(x < 0f ? 0 : 2) + (z < 1.9f ? 0 : 1)];
        }

        /// <summary>A house front's frame: w = metres out from the facade (|x| = 3.8) toward the street, v = height, u = along the street.</summary>
        readonly struct Facade
        {
            public readonly float Side, Zc;
            public Facade(float side, float zc) { Side = side; Zc = zc; }
            public Vector3 P(float w, float v, float u) => new Vector3(Side * (3.8f - w), v, Zc + u);
            /// <summary>World direction of a step (dw, dv) in the front's plane; Dir(1, 0) faces the street.</summary>
            public Vector3 Dir(float dw, float dv) => new Vector3(-Side * dw, dv, 0f);
            /// <summary>Turns a box about the street axis so its length follows a slope rising toward the house.</summary>
            public Quaternion Tilt(float degrees) => Quaternion.Euler(0f, 0f, Side * degrees);
            /// <summary>An axis-aligned box between two corners given in front coordinates.</summary>
            public void Box(KitBatch k, Material m, float w0, float w1, float v0, float v1, float u0, float u1, float uv = 1f)
                => k.Box(m, P((w0 + w1) * 0.5f, (v0 + v1) * 0.5f, (u0 + u1) * 0.5f), new Vector3(Mathf.Abs(w1 - w0), Mathf.Abs(v1 - v0), Mathf.Abs(u1 - u0)), uv);
        }

        /// <summary>The street's architecture and set dressing around the puzzle (see the file header).</summary>
        void BuildStreetDetail(Transform g, Material postWood)
        {
            var c = new StreetCtx { M = StreetMats(), Root = g };
            BuildPaving(c);
            foreach (var h in s_Houses) BuildHouse(c, h);
            BuildStreetEnds(c);
            BuildRoute(c);
            BuildPoles(c);
            BuildStreetProps(c, postWood);
            string[] names = { "StreetLeftNear", "StreetLeftFar", "StreetRightNear", "StreetRightFar" };
            for (int i = 0; i < c.Groups.Length; i++) c.Groups[i].Bake(g, names[i]);
            c.Glow.Bake(g, "StreetGlow", false);
            var shade = MeshFactory.FromBuilder("PaperShadows", c.Shade, c.M.Shadow, g).GetComponent<MeshRenderer>();
            shade.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            shade.receiveShadows = false;
        }

        /// <summary>
        /// Wet granite slabs laid across the street in five pieces, one per lantern pool (the collider slab keeps its footprint
        /// but no longer draws), a gutter with iron grates each side, a kerb, and a cobbled strip along the houses; a few puddles.
        /// </summary>
        void BuildPaving(StreetCtx c)
        {
            var m = c.M;
            var paving = transform.Find("Street/Paving");
            if (paving != null && paving.GetComponent<MeshRenderer>() != null) paving.GetComponent<MeshRenderer>().enabled = false;
            var slabs = Mats.Lit(new Color(0.64f, 0.64f, 0.68f), 0.72f, 0f, "granite_pavers");
            float[] cuts = { -12.2f, -7.3f, -2.7f, 1.9f, 6.5f, 13.7f };
            for (int i = 0; i + 1 < cuts.Length; i++)
            {
                float z0 = cuts[i], z1 = cuts[i + 1], zm = (z0 + z1) * 0.5f;
                MeshFactory.Box("Paving" + i, c.Root, new Vector3(0f, -0.15f, zm), new Vector3(5.9f, 0.3f, z1 - z0), slabs, false, 2.4f);
                foreach (float s in new[] { -1f, 1f })
                {
                    var k = c.At(s, zm);
                    k.Box(m.Strip, new Vector3(s * 3.05f, -0.19f, zm), new Vector3(0.2f, 0.22f, z1 - z0), 0.6f);    // channel, wet cobbles at -0.08
                    k.Box(m.Stone, new Vector3(s * 3.2f, -0.13f, zm), new Vector3(0.1f, 0.34f, z1 - z0));            // kerb, top +0.04
                    k.Box(m.Strip, new Vector3(s * 3.535f, -0.135f, zm), new Vector3(0.57f, 0.33f, z1 - z0), 0.9f);  // cobbles, top +0.03
                    for (float gz = z0 + 1.1f; gz < z1 - 0.3f; gz += 2.3f) Grate(k, m.Black, new Vector3(s * 3.05f, 0f, gz));
                }
            }
            UrbanKit.Puddles(c.Root, new Vector3(0.2f, 0f, 1.2f), new Vector2(4.6f, 18f), 10, 64, Mats.Lit(new Color(0.02f, 0.025f, 0.03f), 0.96f));
        }

        /// <summary>An iron gutter grate, its top flush with the street.</summary>
        static void Grate(KitBatch k, Material iron, Vector3 c)
        {
            for (int i = 0; i < 8; i++) k.Box(iron, c + new Vector3(0f, -0.012f, -0.21f + i * 0.06f), new Vector3(0.2f, 0.024f, 0.014f));
            foreach (float x in new[] { -0.095f, 0.095f }) k.Box(iron, c + new Vector3(x, -0.012f, 0f), new Vector3(0.016f, 0.024f, 0.46f));
        }

        void BuildHouse(StreetCtx c, (int side, float z, HouseKind kind, float eave, int noren, bool red, bool lit) h)
        {
            var m = c.M;
            var f = new Facade(h.side, h.z);
            var k = c.At(h.side, h.z);
            const float L = 2.3f;
            var paper = h.lit ? m.PaperGlow : m.PaperDim;
            var pb = h.lit ? c.Glow : k;
            // The frame every front shares: a stone plinth, corner posts and the head beam under the pent roof.
            f.Box(k, m.Stone, -0.02f, 0.07f, 0f, 0.16f, -L, L, 1.2f);
            Post(k, m.Timber, f, -L + 0.07f, 3.24f);
            Post(k, m.Timber, f, L - 0.07f, 3.24f);
            f.Box(k, m.Timber, -0.02f, 0.15f, 2.98f, 3.24f, -L, L);
            switch (h.kind)
            {
                case HouseKind.Koshi:
                case HouseKind.KoshiBlind: KoshiFront(c, k, f, m.Cloth[h.noren], paper, pb, h.lit && h.kind == HouseKind.Koshi); break;
                case HouseKind.Teahouse: TeahouseFront(c, k, f, paper, pb); break;
                case HouseKind.Soba: SobaFront(c, k, f); break;
                case HouseKind.Shoji: ShojiFront(c, k, f, paper, pb); break;
                case HouseKind.Shuttered: ShutteredFront(c, k, f, h.lit); break;
                case HouseKind.ShutterShop: ShutterShopFront(c, k, f); break;
                default: ModernFront(c, k, f); break;
            }
            PentRoof(c, k, f, L);
            UpperFloor(c, k, f, L, h.kind, h.eave, h.lit);
            MainRoof(c, k, f, L, h.eave);
            // The house chochin hangs from the pent roof's middle arm; BuildStreetProps lights the lit ones.
            Chochin(c, k, f.P(0.88f, 2.48f, 0f), h.lit ? (h.red ? m.LanternRed : m.LanternCream) : m.PaperDim, h.lit, 1f, 2.86f);
        }

        static void Post(KitBatch k, Material m, Facade f, float u, float top) => f.Box(k, m, -0.02f, 0.13f, 0.16f, top, u - 0.07f, u + 0.07f);

        // ------------------------------------------------------------------ ground-floor fronts
        /// <summary>The classic front: a lattice door with noren at the south end, then senbon-goshi over the paper and the
        /// curved bamboo inuyarai along its foot (shorter where a plant stand sits).</summary>
        static void KoshiFront(StreetCtx c, KitBatch k, Facade f, Material noren, Material paper, KitBatch pb, bool plants)
        {
            var m = c.M;
            Post(k, m.Timber, f, -1.16f, 2.98f);
            pb.Box(paper, f.P(0.012f, 1.17f, -1.7f), new Vector3(0.012f, 1.94f, 0.92f));
            Koshi(k, m.Timber, f, -2.16f, -1.23f, 0.2f, 2.15f, 0.04f, 0.075f);
            pb.Box(paper, f.P(0.012f, 2.58f, -1.7f), new Vector3(0.012f, 0.74f, 0.92f));
            Kumiko(k, m.Timber, f, -2.13f, -1.26f, 2.24f, 2.92f, 0.03f, 4, 2);
            Noren(k, noren, m.Timber, f, -2.13f, -1.27f, 2.14f, 0.8f, 2, 0.14f);
            f.Box(k, m.Board, 0.13f, 0.145f, 1.32f, 1.62f, -1.2f, -1.12f); // nameplate on the door post
            Post(k, m.Timber, f, 0.55f, 2.98f);
            f.Box(k, m.Timber, 0f, 0.1f, 0.16f, 0.4f, -1.09f, 2.23f);
            pb.Box(paper, f.P(0.01f, 1.6f, 0.57f), new Vector3(0.012f, 2.4f, 3.32f));
            if (plants) ShadowShape(c.Shade, f, 0.0165f, new Vector2(1.45f, 0.42f), s_Standing, 0.95f);
            Koshi(k, m.Timber, f, -1.09f, 2.23f, 0.4f, 2.8f, 0.045f, 0.07f);
            f.Box(k, m.Plaster, 0f, 0.025f, 2.8f, 2.98f, -1.09f, 2.23f, 2f);
            Inuyarai(k, m.Bamboo, m.Black, f, plants ? 0.3f : -1.0f, 2.14f);
        }

        /// <summary>A teahouse in red-ochre bengara: kirikoshi lattice over lit paper, the door at the north end under a pale
        /// noren with a red disc, small red chochin either side of it, and a hanging board.</summary>
        static void TeahouseFront(StreetCtx c, KitBatch k, Facade f, Material paper, KitBatch pb)
        {
            var m = c.M;
            Post(k, m.Bengara, f, 1.16f, 2.98f);
            pb.Box(paper, f.P(0.012f, 1.17f, 1.7f), new Vector3(0.012f, 1.94f, 0.92f));
            Koshi(k, m.Bengara, f, 1.23f, 2.16f, 0.2f, 2.15f, 0.04f, 0.075f);
            pb.Box(paper, f.P(0.012f, 2.58f, 1.7f), new Vector3(0.012f, 0.74f, 0.92f));
            Kumiko(k, m.Bengara, f, 1.26f, 2.13f, 2.24f, 2.92f, 0.03f, 4, 2);
            Noren(k, m.Cloth[2], m.Timber, f, 1.27f, 2.13f, 2.14f, 0.8f, 2, 0.14f);
            k.Of(m.Cloth[1]).AddDisc(f.P(0.153f, 1.86f, 1.7f), f.Dir(1f, 0f), 0.12f, 18);
            f.Box(k, m.Bengara, 0f, 0.1f, 0.16f, 0.4f, -2.23f, 1.09f);
            pb.Box(paper, f.P(0.01f, 1.6f, -0.57f), new Vector3(0.012f, 2.4f, 3.32f));
            ShadowShape(c.Shade, f, 0.0165f, new Vector2(-0.9f, 0.42f), s_Seated, 1.05f);
            Koshi(k, m.Bengara, f, -2.23f, 1.09f, 0.4f, 2.8f, 0.045f, 0.065f, 4, 1.35f);
            f.Box(k, m.Plaster, 0f, 0.025f, 2.8f, 2.98f, -2.23f, 1.09f, 2f);
            foreach (float u in new[] { 1.0f, 2.26f })
            {
                f.Box(k, m.Timber, 0.12f, 0.4f, 2.84f, 2.9f, u - 0.025f, u + 0.025f);
                Chochin(c, k, f.P(0.34f, 2.42f, u), m.LanternRed, true, 0.6f, 2.84f);
            }
            HangingSign(c, k, f, -1.95f, "TEA", m.Board, new Color(0.12f, 0.08f, 0.06f));
        }

        /// <summary>An open soba shop: five navy noren across a lit room with a counter, stools, shelves of bowls, menu tags
        /// and pendant lamps; its own light spills out under the noren onto the street.</summary>
        void SobaFront(StreetCtx c, KitBatch k, Facade f)
        {
            var m = c.M;
            foreach (float u in new[] { -1.8f, 1.8f }) Post(k, m.Timber, f, u, 2.98f);
            foreach (float s in new[] { -1f, 1f })
            {
                f.Box(k, m.Plaster, 0f, 0.03f, 0.4f, 2.98f, s * 1.86f, s * 2.16f, 2f);
                f.Box(k, m.Timber, 0f, 0.06f, 0.16f, 0.4f, s * 1.86f, s * 2.16f);
            }
            f.Box(k, m.Timber, -0.02f, 0.12f, 2.58f, 2.98f, -1.74f, 1.74f);
            // The room: warm back wall, side walls, boarded ceiling, stone floor.
            c.Glow.Box(m.Interior, f.P(-1.74f, 1.29f, 0f), new Vector3(0.04f, 2.58f, 3.5f));
            foreach (float s in new[] { -1f, 1f }) f.Box(k, m.Plaster, -1.74f, 0f, 0f, 2.58f, s * 1.74f, s * 1.8f, 2f);
            f.Box(k, m.Board, -1.74f, 0f, 2.58f, 2.64f, -1.8f, 1.8f);
            f.Box(k, m.Stone, -1.74f, 0f, -0.05f, 0.025f, -1.8f, 1.8f, 0.8f);
            // Counter and stools.
            f.Box(k, m.Timber, -1.1f, -0.64f, 0f, 0.92f, -1.45f, 1.25f);
            f.Box(k, m.Board, -1.13f, -0.58f, 0.92f, 0.97f, -1.5f, 1.3f);
            foreach (float u in new[] { -0.95f, 0f, 0.95f })
            {
                k.Of(m.Timber).AddCylinder(f.P(-0.4f, 0f, u), 0.03f, 0.55f, 8);
                k.Of(m.Board).AddCylinder(f.P(-0.4f, 0.55f, u), 0.16f, 0.05f, 14);
            }
            // Shelves of bowls and bottles; menu tags along the top of the back wall; two pendant lamps.
            var bowl = new[] { new Vector2(0f, 0f), new Vector2(0.035f, 0f), new Vector2(0.06f, 0.035f), new Vector2(0.07f, 0.06f) };
            foreach (float sv in new[] { 1.3f, 1.72f })
            {
                f.Box(k, m.Board, -1.72f, -1.48f, sv - 0.025f, sv, -1.5f, 1.5f);
                int i = sv > 1.5f ? 1 : 0;
                for (float u = -1.4f; u < 1.42f; u += 0.17f, i++)
                {
                    if (i % 3 == 2)
                    {
                        k.Of(m.Glass).AddCylinder(f.P(-1.6f, sv, u), 0.035f, 0.22f, 8);
                        k.Of(m.Glass).AddCylinder(f.P(-1.6f, sv + 0.22f, u), 0.012f, 0.06f, 6);
                    }
                    else k.Of(m.Pot).AddLathe(f.P(-1.6f, sv, u), bowl, 10);
                }
            }
            for (int i = 0; i < 9; i++) f.Box(k, m.PaperDim, -1.72f, -1.71f, 2.12f, 2.4f, -1.41f + i * 0.34f, -1.31f + i * 0.34f);
            foreach (float u in new[] { -0.6f, 0.6f })
            {
                Rod(k, m.Black, f.P(-0.85f, 2.58f, u), f.P(-0.85f, 2.32f, u), 0.01f);
                k.Of(m.Black).AddFrustum(f.P(-0.85f, 2.24f, u), 0.15f, 0.04f, 0.09f, 12, false, true);
                c.Glow.Of(m.Bulb).AddSphere(f.P(-0.85f, 2.22f, u), 0.055f, 10, 6);
            }
            Noren(k, m.Cloth[0], m.Timber, f, -1.7f, 1.7f, 2.52f, 0.95f, 5, 0.14f);
            k.Of(m.Cloth[2]).AddDisc(f.P(0.153f, 2.1f, 0f), f.Dir(1f, 0f), 0.13f, 18);
            AddPointLight(c.Root, f.P(-0.85f, 2.1f, 0f), new Color(1f, 0.7f, 0.42f), 1.15f, 3.5f);
            HangingSign(c, k, f, -2.0f, "SOBA", m.Timber, new Color(0.93f, 0.88f, 0.76f));
        }

        /// <summary>Four paper doors with kumiko grids over kick boards; people's shadows on the paper, and one of them walks.</summary>
        static void ShojiFront(StreetCtx c, KitBatch k, Facade f, Material paper, KitBatch pb)
        {
            var m = c.M;
            for (int i = 0; i < 4; i++)
            {
                float u0 = -2.16f + i * 1.08f, u1 = u0 + 1.08f;
                f.Box(k, m.Timber, 0f, 0.045f, 0.18f, 0.66f, u0, u1);
                pb.Box(paper, f.P(0.008f, 1.79f, (u0 + u1) * 0.5f), new Vector3(0.008f, 2.26f, u1 - u0 - 0.06f));
                Kumiko(k, m.Timber, f, u0 + 0.045f, u1 - 0.045f, 0.7f, 2.9f, 0.014f, 3, 7);
            }
            ShadowShape(c.Shade, f, 0.0125f, new Vector2(-1.62f, 0.7f), s_Seated, 0.95f);
            ShadowShape(c.Shade, f, 0.0125f, new Vector2(0.54f, 0.7f), s_Standing, 0.98f, true);
            PaperShadowFigure(c, f, 0.0125f, -0.75f, 0.7f, s_Standing, 0.96f, -1.85f, 1.85f, false);
        }

        /// <summary>Amado storm shutters closed over the whole front and a folded bench; one small window lit, and someone may
        /// be at it when you look back.</summary>
        static void ShutteredFront(StreetCtx c, KitBatch k, Facade f, bool lit)
        {
            var m = c.M;
            f.Box(k, m.Weathered, 0f, 0.04f, 0.16f, 2.98f, -2.16f, 2.16f, 1.5f);
            for (float u = -1.98f; u < 2.1f; u += 0.18f) f.Box(k, m.Black, 0.04f, 0.045f, 0.16f, 2.98f, u - 0.006f, u + 0.006f);
            foreach (float v in new[] { 0.62f, 1.55f, 2.5f }) f.Box(k, m.Weathered, 0.04f, 0.075f, v - 0.04f, v + 0.04f, -2.16f, 2.16f);
            (lit ? c.Glow : k).Box(lit ? m.PaperGlow : m.PaperDim, f.P(0.082f, 1.68f, 1.35f), new Vector3(0.008f, 0.72f, 0.76f));
            Kumiko(k, m.Timber, f, 0.97f, 1.73f, 1.32f, 2.04f, 0.088f, 3, 4);
            f.Box(k, m.Weathered, 0.075f, 0.105f, 0.48f, 0.92f, -1.7f, -0.3f);
            foreach (float u in new[] { -1.55f, -0.45f }) f.Box(k, m.Black, 0.075f, 0.13f, 0.86f, 0.9f, u - 0.02f, u + 0.02f);
            if (lit) PaperShadowFigure(c, f, 0.0865f, 1.35f, 1.33f, s_Bust, 1f, 0f, 0f, true);
        }

        /// <summary>A closed shop: a corrugated roll-down shutter under its box, a faded fascia, a padlock, a notice taped on.</summary>
        static void ShutterShopFront(StreetCtx c, KitBatch k, Facade f)
        {
            var m = c.M;
            f.Box(k, m.Metal, 0f, 0.22f, 2.4f, 2.62f, -2.16f, 2.16f);
            f.Box(k, m.Board, 0f, 0.09f, 2.62f, 2.98f, -2.16f, 2.16f);
            f.Box(k, m.Metal, 0.02f, 0.05f, 0.03f, 2.4f, -2.02f, 2.02f, 0.8f);
            for (int i = 0; i < 30; i++) { float v = 0.13f + i * 0.076f; f.Box(k, m.Metal, 0.05f, 0.062f, v - 0.011f, v + 0.011f, -2.02f, 2.02f); }
            foreach (float u in new[] { -2.07f, 2.07f }) f.Box(k, m.Metal, 0f, 0.09f, 0.03f, 2.4f, u - 0.05f, u + 0.05f);
            f.Box(k, m.Metal, 0.04f, 0.085f, 0.03f, 0.13f, -2.02f, 2.02f);
            f.Box(k, m.Black, 0.085f, 0.11f, 0.06f, 0.16f, -0.05f, 0.05f);
            f.Box(k, m.PaperDim, 0.062f, 0.066f, 1.22f, 1.52f, 0.5f, 0.71f);
            FasciaSign(c, f, "TOFU", 2.8f, 0.09f, new Color(0.62f, 0.55f, 0.42f));
        }

        /// <summary>A renovated front: tiled dado, plaster, aluminium doors with frosted glass under a little canopy, an
        /// entrance light, nameplate and intercom, a barred window and a mailbox.</summary>
        static void ModernFront(StreetCtx c, KitBatch k, Facade f)
        {
            var m = c.M;
            f.Box(k, m.Stone, 0f, 0.05f, 0.16f, 0.95f, -2.16f, 2.16f, 0.6f);
            f.Box(k, m.Plaster, 0f, 0.03f, 0.95f, 2.98f, -2.16f, 2.16f, 2f);
            f.Box(k, m.Black, 0.03f, 0.055f, 0.16f, 2.16f, -0.99f, 0.39f);
            foreach (var (a, b, w) in new[] { (-0.95f, -0.29f, 0.055f), (-0.33f, 0.35f, 0.08f) })
            {
                f.Box(k, m.Frosted, w, w + 0.01f, 0.25f, 2.06f, a + 0.04f, b - 0.04f);
                f.Box(k, m.Metal, w, w + 0.03f, 0.18f, 0.25f, a, b);
                f.Box(k, m.Metal, w, w + 0.03f, 2.06f, 2.13f, a, b);
                f.Box(k, m.Metal, w, w + 0.03f, 0.18f, 2.13f, a, a + 0.04f);
                f.Box(k, m.Metal, w, w + 0.03f, 0.18f, 2.13f, b - 0.04f, b);
                f.Box(k, m.Metal, w, w + 0.03f, 1.02f, 1.05f, a, b);
            }
            f.Box(k, m.Metal, 0.03f, 0.42f, 2.2f, 2.25f, -1.05f, 0.45f);
            c.Glow.Box(m.Bulb, f.P(0.1f, 2.4f, 0.62f), new Vector3(0.1f, 0.13f, 0.1f));
            f.Box(k, m.Metal, 0.03f, 0.05f, 2.34f, 2.46f, 0.58f, 0.66f);
            f.Box(k, m.Board, 0.03f, 0.05f, 1.45f, 1.6f, 0.52f, 0.68f);
            f.Box(k, m.Metal, 0.03f, 0.075f, 1.22f, 1.34f, 0.55f, 0.65f);
            f.Box(k, m.Frosted, 0.03f, 0.04f, 1.3f, 2.2f, -2.0f, -1.32f);
            for (int i = 0; i < 7; i++) f.Box(k, m.Metal, 0.06f, 0.075f, 1.28f, 2.22f, -1.975f + i * 0.1f, -1.96f + i * 0.1f);
            f.Box(k, m.Metal, 0.03f, 0.19f, 1.05f, 1.42f, -1.27f, -1.05f);
        }

        // ------------------------------------------------------------------ roofs and upper floor
        /// <summary>The pent roof over the ground floor: tiled slab with round tile courses and eave tiles, boards and rafters
        /// underneath (lit from below by the chochin), the eave beam on three cantilevered arms, flashing against the wall.</summary>
        static void PentRoof(StreetCtx c, KitBatch k, Facade f, float L)
        {
            var m = c.M;
            const float we = 0.95f, ve = 3.12f, wt = -0.14f, vt = 3.5f;
            Slab(k, m.Tile, f, we, ve, wt, vt, -L, L, 0.05f, 1.5f);
            TileRows(k, m.Tile, f, we, ve, wt, vt, -L + 0.05f, L - 0.05f, 0.25f, 0.042f);
            Soffit(k, m.Timber, m.Board, f, we, ve, wt, vt, 0.13f, -L, L, 0.05f, 0.3f);
            f.Box(k, m.Timber, 0.8f, 0.9f, 2.95f, 3.06f, -L, L);
            foreach (float u in new[] { -L + 0.07f, 0f, L - 0.07f }) f.Box(k, m.Timber, 0.13f, 0.92f, 2.86f, 2.98f, u - 0.05f, u + 0.05f);
            f.Box(k, m.Tile, -0.17f, 0.03f, 3.44f, 3.57f, -L, L);
        }

        /// <summary>The low upper floor: plaster with corner posts and a wall plate, and per kind slatted mushiko windows,
        /// sudare blinds over faint paper, a paper-screened balcony, or an aluminium window with an air conditioner.</summary>
        static void UpperFloor(StreetCtx c, KitBatch k, Facade f, float L, HouseKind kind, float eave, bool lit)
        {
            var m = c.M;
            float top = eave + 0.34f, wTop = Mathf.Min(4.7f, eave - 0.3f);
            f.Box(k, m.Plaster, -2.3f, -0.12f, 3.44f, top, -L, L, 2.2f);
            f.Box(k, m.Timber, -0.13f, -0.05f, 3.44f, eave, -L, -L + 0.12f);
            f.Box(k, m.Timber, -0.13f, -0.05f, 3.44f, eave, L - 0.12f, L);
            f.Box(k, m.Timber, -0.14f, -0.04f, eave - 0.14f, eave, -L, L);
            var faint = lit ? m.PaperFaint : m.Black;
            var fb = lit ? c.Glow : k;
            switch (kind)
            {
                case HouseKind.Koshi:
                case HouseKind.Soba:
                case HouseKind.Shuttered:
                    Mushiko(k, m.Plaster, fb, kind == HouseKind.Shuttered ? m.Black : faint, f, -1.5f, 1.5f, 3.95f, wTop);
                    break;
                case HouseKind.KoshiBlind:
                case HouseKind.Teahouse:
                    fb.Box(faint, f.P(-0.114f, (3.82f + wTop) * 0.5f, 0f), new Vector3(0.008f, wTop - 3.82f, 3.2f));
                    Kumiko(k, kind == HouseKind.Teahouse ? m.Bengara : m.Timber, f, -1.6f, 1.6f, 3.82f, wTop, -0.105f, 8, 3);
                    Sudare(k, m.Bamboo, m.Black, f, -1.66f, 1.66f, 3.78f, wTop + 0.05f, -0.045f);
                    break;
                case HouseKind.Shoji:
                    fb.Box(lit ? m.PaperGlow : m.PaperDim, f.P(-0.112f, 4.2f, 0f), new Vector3(0.008f, 1.16f, 3.3f));
                    Kumiko(k, m.Timber, f, -1.65f, 1.65f, 3.62f, 4.78f, -0.105f, 12, 4);
                    if (lit) ShadowShape(c.Shade, f, -0.1075f, new Vector2(-0.55f, 3.62f), s_Standing, 0.7f);
                    f.Box(k, m.Timber, -0.12f, 0.26f, 3.5f, 3.56f, -1.8f, 1.8f);
                    for (float u = -1.75f; u <= 1.76f; u += 0.1f) f.Box(k, m.Timber, 0.19f, 0.21f, 3.56f, 4.12f, u - 0.01f, u + 0.01f);
                    f.Box(k, m.Timber, 0.17f, 0.24f, 4.12f, 4.18f, -1.82f, 1.82f);
                    foreach (float u in new[] { -1.8f, 1.8f }) f.Box(k, m.Timber, 0.17f, 0.24f, 3.56f, 4.18f, u - 0.035f, u + 0.035f);
                    break;
                default:
                    f.Box(k, m.Glass, -0.125f, -0.115f, 3.95f, 4.7f, -1.3f, 0.5f);
                    f.Box(k, m.Metal, -0.13f, -0.09f, 3.92f, 3.97f, -1.33f, 0.53f);
                    f.Box(k, m.Metal, -0.13f, -0.09f, 4.68f, 4.73f, -1.33f, 0.53f);
                    foreach (float u in new[] { -1.31f, -0.4f, 0.51f }) f.Box(k, m.Metal, -0.13f, -0.09f, 3.95f, 4.7f, u - 0.02f, u + 0.02f);
                    // Air conditioner on brackets, its fan facing the street, the pipe into the wall.
                    var face = Quaternion.FromToRotation(Vector3.up, f.Dir(1f, 0f));
                    f.Box(k, m.Frosted, -0.12f, 0.16f, 3.72f, 4.26f, 0.9f, 1.72f);
                    k.Of(m.Black).AddDisc(f.P(0.162f, 3.99f, 1.38f), f.Dir(1f, 0f), 0.2f, 18);
                    k.Of(m.Metal).AddTorus(f.P(0.165f, 3.99f, 1.38f), 0.2f, 0.01f, 18, 3, face);
                    k.Of(m.Metal).AddTorus(f.P(0.165f, 3.99f, 1.38f), 0.12f, 0.008f, 14, 3, face);
                    foreach (float u in new[] { 0.97f, 1.65f }) f.Box(k, m.Metal, -0.12f, 0.2f, 3.66f, 3.7f, u - 0.02f, u + 0.02f);
                    Rod(k, m.Frosted, f.P(0.0f, 3.85f, 1.74f), f.P(-0.11f, 3.85f, 1.98f), 0.05f);
                    break;
            }
        }

        /// <summary>The main roof: tiled slab with round courses and eave tiles, rafters under the eave, the ridge with end
        /// tiles, and gable ends wherever this roof stands above a neighbour's.</summary>
        static void MainRoof(StreetCtx c, KitBatch k, Facade f, float L, float eave)
        {
            var m = c.M;
            float we = 0.62f, ve = eave + 0.04f, wt = -2.2f, vt = eave + 1.34f, top = eave + 0.34f;
            Slab(k, m.Tile, f, we, ve, wt, vt, -L, L, 0.06f, 1.5f);
            TileRows(k, m.Tile, f, we, ve, wt, vt, -L + 0.06f, L - 0.06f, 0.27f, 0.05f);
            Soffit(k, m.Timber, m.Board, f, we, ve, wt, vt, -0.12f, -L, L, 0.06f, 0.32f);
            Ridge(k, m.Tile, f, wt, vt, -L, L);
            foreach (float u in new[] { -L, L }) Tri2(k.Of(m.Plaster), f.P(-0.12f, top, u), f.P(wt, vt - 0.04f, u), f.P(wt, top, u));
        }

        /// <summary>A box lying along the roof line (we, ve)-(wt, vt) with its top `drop` below the line, centred at u.</summary>
        static void SlopeBox(KitBatch k, Material mat, Facade f, float we, float ve, float wt, float vt, float u, float width, float thick, float drop, float uv = 1f)
        {
            float run = we - wt, rise = vt - ve, len = Mathf.Sqrt(run * run + rise * rise);
            float nw = rise / len, nv = run / len; // the surface normal in (w, v): up, and out toward the street
            float cw = (we + wt) * 0.5f - nw * (drop + thick * 0.5f), cv = (ve + vt) * 0.5f - nv * (drop + thick * 0.5f);
            k.Box(mat, f.P(cw, cv, u), new Vector3(len, thick, width), f.Tilt(Mathf.Atan2(rise, run) * Mathf.Rad2Deg), uv);
        }

        static void Slab(KitBatch k, Material mat, Facade f, float we, float ve, float wt, float vt, float u0, float u1, float thick, float uv = 1f)
            => SlopeBox(k, mat, f, we, ve, wt, vt, (u0 + u1) * 0.5f, u1 - u0, thick, 0f, uv);

        /// <summary>Round tile courses down a roof slab, each ending in a round eave tile.</summary>
        static void TileRows(KitBatch k, Material tile, Facade f, float we, float ve, float wt, float vt, float u0, float u1, float pitch, float r)
        {
            float run = we - wt, rise = vt - ve, len = Mathf.Sqrt(run * run + rise * rise);
            float nw = rise / len, nv = run / len;
            var up = f.Dir(-run, rise).normalized;
            var rot = Quaternion.FromToRotation(Vector3.up, up);
            int n = Mathf.Max(1, Mathf.RoundToInt((u1 - u0) / pitch));
            var b = k.Of(tile);
            for (int i = 0; i < n; i++)
            {
                float u = u0 + (i + 0.5f) * (u1 - u0) / n;
                var foot = f.P(we + nw * r * 0.3f, ve + nv * r * 0.3f, u);
                b.AddFrustum(foot, r, r, len, 6, true, false, 1f, rot);
                b.AddCylinder(foot - up * 0.025f, r * 1.3f, 0.03f, 8, 1f, rot);
            }
        }

        /// <summary>The underside of an eave between the wall (wWall) and the edge: boards under the slab, rafters under them.</summary>
        static void Soffit(KitBatch k, Material rafter, Material board, Facade f, float we, float ve, float wt, float vt, float wWall, float u0, float u1, float slab, float pitch)
        {
            float slope = (vt - ve) / (we - wt), vw = ve + (we - wWall) * slope;
            SlopeBox(k, board, f, we, ve, wWall, vw, (u0 + u1) * 0.5f, u1 - u0, 0.018f, slab);
            int n = Mathf.Max(1, Mathf.RoundToInt((u1 - u0) / pitch));
            for (int i = 0; i <= n; i++)
                SlopeBox(k, rafter, f, we - 0.02f, ve + 0.02f * slope, wWall, vw, Mathf.Lerp(u0 + 0.04f, u1 - 0.04f, i / (float)n), 0.045f, 0.055f, slab + 0.018f);
        }

        /// <summary>The ridge: stacked ridge tiles under a round cap, with raised end tiles at both ends.</summary>
        static void Ridge(KitBatch k, Material tile, Facade f, float w, float v, float u0, float u1)
        {
            f.Box(k, tile, w - 0.2f, w + 0.2f, v - 0.08f, v + 0.1f, u0, u1);
            f.Box(k, tile, w - 0.14f, w + 0.14f, v + 0.1f, v + 0.22f, u0 + 0.06f, u1 - 0.06f);
            k.Of(tile).AddCylinder(f.P(w, v + 0.26f, u0 + 0.06f), 0.07f, u1 - u0 - 0.12f, 8, 1f, Quaternion.Euler(90f, 0f, 0f));
            foreach (float u in new[] { u0 + 0.1f, u1 - 0.1f }) f.Box(k, tile, w - 0.22f, w + 0.22f, v - 0.08f, v + 0.44f, u - 0.08f, u + 0.08f);
        }

        // ------------------------------------------------------------------ joinery and fittings
        /// <summary>Koshi: vertical slats between rails in front of the paper. longEvery &gt; 0 makes kirikoshi: one long slat,
        /// then short ones hanging from a rail at shortFrom.</summary>
        static void Koshi(KitBatch k, Material mat, Facade f, float u0, float u1, float v0, float v1, float w, float pitch, int longEvery = 0, float shortFrom = 0f)
        {
            int n = Mathf.Max(2, Mathf.RoundToInt((u1 - u0) / pitch));
            for (int i = 0; i <= n; i++)
            {
                float u = Mathf.Lerp(u0 + 0.016f, u1 - 0.016f, i / (float)n);
                float vb = longEvery > 0 && i % longEvery != 0 ? shortFrom : v0;
                f.Box(k, mat, w, w + 0.03f, vb, v1, u - 0.012f, u + 0.012f);
            }
            f.Box(k, mat, w - 0.006f, w + 0.04f, v1 - 0.06f, v1, u0, u1);
            f.Box(k, mat, w - 0.006f, w + 0.04f, v0, v0 + 0.06f, u0, u1);
            if (longEvery > 0) f.Box(k, mat, w - 0.006f, w + 0.04f, shortFrom - 0.05f, shortFrom, u0, u1);
        }

        /// <summary>A kumiko grid of thin bars in a frame, laid over a paper panel (bars start at w).</summary>
        static void Kumiko(KitBatch k, Material mat, Facade f, float u0, float u1, float v0, float v1, float w, int cols, int rows)
        {
            for (int i = 1; i < cols; i++) { float u = Mathf.Lerp(u0, u1, i / (float)cols); f.Box(k, mat, w, w + 0.02f, v0, v1, u - 0.009f, u + 0.009f); }
            for (int j = 1; j < rows; j++) { float v = Mathf.Lerp(v0, v1, j / (float)rows); f.Box(k, mat, w, w + 0.02f, v - 0.009f, v + 0.009f, u0, u1); }
            f.Box(k, mat, w, w + 0.035f, v0 - 0.035f, v0, u0 - 0.035f, u1 + 0.035f);
            f.Box(k, mat, w, w + 0.035f, v1, v1 + 0.035f, u0 - 0.035f, u1 + 0.035f);
            f.Box(k, mat, w, w + 0.035f, v0, v1, u0 - 0.035f, u0);
            f.Box(k, mat, w, w + 0.035f, v0, v1, u1, u1 + 0.035f);
        }

        /// <summary>A noren: split cloth panels hanging (a little unevenly) from a rod across a doorway.</summary>
        static void Noren(KitBatch k, Material cloth, Material rod, Facade f, float u0, float u1, float top, float len, int panels, float w)
        {
            f.Box(k, rod, w - 0.016f, w + 0.016f, top, top + 0.032f, u0 - 0.05f, u1 + 0.05f);
            float pw = (u1 - u0) / panels;
            for (int i = 0; i < panels; i++)
            {
                float a = u0 + i * pw + 0.012f, b = u0 + (i + 1) * pw - 0.012f, dw = i % 2 == 0 ? 0.004f : -0.004f;
                f.Box(k, cloth, w - 0.006f + dw, w + 0.006f + dw, top - len - (i % 3) * 0.015f, top, a, b);
            }
        }

        /// <summary>Inuyarai: the curved bamboo fence skirting the foot of a machiya front (it falls from the wall and flares
        /// out over the cobbles), with three ties.</summary>
        static void Inuyarai(KitBatch k, Material bamboo, Material tie, Facade f, float u0, float u1)
        {
            const float W = 0.42f, H = 0.95f;
            Vector3 C(float t, float u) { float a = t * Mathf.PI * 0.5f; return f.P(W - W * Mathf.Cos(a), H - H * Mathf.Sin(a) + 0.03f, u); }
            int n = Mathf.Max(2, Mathf.RoundToInt((u1 - u0) / 0.075f));
            for (int i = 0; i <= n; i++)
            {
                float u = Mathf.Lerp(u0, u1, i / (float)n);
                for (int s = 0; s < 4; s++) Rod(k, bamboo, C(s / 4f, u), C((s + 1) / 4f, u), 0.024f);
            }
            foreach (float t in new[] { 0.12f, 0.45f, 0.8f }) Rod(k, tie, C(t, u0 - 0.03f), C(t, u1 + 0.03f), 0.016f);
        }

        /// <summary>Mushiko-mado: the slatted plaster window of the low upper floor, a dark or faintly lit room behind.</summary>
        static void Mushiko(KitBatch k, Material plaster, KitBatch backBatch, Material back, Facade f, float u0, float u1, float v0, float v1)
        {
            backBatch.Box(back, f.P(-0.114f, (v0 + v1) * 0.5f, (u0 + u1) * 0.5f), new Vector3(0.008f, v1 - v0, u1 - u0));
            int n = Mathf.Max(2, Mathf.RoundToInt((u1 - u0) / 0.11f));
            for (int i = 0; i <= n; i++)
            {
                float u = Mathf.Lerp(u0 + 0.03f, u1 - 0.03f, i / (float)n);
                f.Box(k, plaster, -0.12f, -0.04f, v0, v1, u - 0.028f, u + 0.028f, 2f);
            }
            f.Box(k, plaster, -0.12f, -0.02f, v1, v1 + 0.1f, u0 - 0.1f, u1 + 0.1f, 2f);
            f.Box(k, plaster, -0.12f, 0f, v0 - 0.1f, v0, u0 - 0.1f, u1 + 0.1f, 2f);
            f.Box(k, plaster, -0.12f, -0.04f, v0, v1, u0 - 0.1f, u0, 2f);
            f.Box(k, plaster, -0.12f, -0.04f, v0, v1, u1, u1 + 0.1f, 2f);
        }

        /// <summary>A sudare: a lowered bamboo blind, slats with gaps for the light behind, a cloth binding and two cords.</summary>
        static void Sudare(KitBatch k, Material bamboo, Material binding, Facade f, float u0, float u1, float v0, float v1, float w)
        {
            int n = Mathf.Max(2, Mathf.RoundToInt((v1 - v0) / 0.05f));
            for (int j = 0; j < n; j++) { float v = v0 + (j + 0.5f) * (v1 - v0) / n; f.Box(k, bamboo, w, w + 0.008f, v - 0.016f, v + 0.016f, u0, u1); }
            f.Box(k, binding, w - 0.004f, w + 0.014f, v1, v1 + 0.07f, u0 - 0.02f, u1 + 0.02f);
            foreach (float u in new[] { u0 + 0.3f, u1 - 0.3f }) f.Box(k, binding, w + 0.008f, w + 0.014f, v0 + 0.12f, v1, u - 0.008f, u + 0.008f);
        }

        // ------------------------------------------------------------------ small geometry helpers
        /// <summary>A square-section rod between two points.</summary>
        static void Rod(KitBatch k, Material m, Vector3 a, Vector3 b, float thick) => Rod(k, m, a, b, thick, thick);

        /// <summary>A rod between two points: width across (horizontal side), height in the vertical plane through it.</summary>
        static void Rod(KitBatch k, Material m, Vector3 a, Vector3 b, float width, float height)
        {
            var d = b - a;
            float len = d.magnitude;
            if (len < 1e-4f) return;
            d /= len;
            var up = Mathf.Abs(d.y) > 0.98f ? Vector3.forward : Vector3.up;
            k.Box(m, (a + b) * 0.5f, new Vector3(width, height, len), Quaternion.LookRotation(d, up));
        }

        /// <summary>A flat triangle visible from both sides.</summary>
        static void Tri2(MeshBuilder b, Vector3 a, Vector3 b1, Vector3 c)
        {
            b.AddTriangle(a, b1, c, Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 1f));
            b.AddTriangle(a, c, b1, Vector2.zero, new Vector2(0f, 1f), new Vector2(1f, 0f));
        }

        /// <summary>A flattened ellipsoid (radius r, half-height h): foliage pads, cushions of moss.</summary>
        static void Blob(MeshBuilder b, Vector3 c, float r, float h, int seg)
        {
            var prof = new List<Vector2>();
            for (int i = 0; i <= 6; i++) { float a = Mathf.Lerp(-90f, 90f, i / 6f) * Mathf.Deg2Rad; prof.Add(new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * h)); }
            b.AddLathe(c, prof, seg);
        }
    }
}
