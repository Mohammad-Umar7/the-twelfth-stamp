using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    // Place detail for Berlin (playtest: "make all the countries detailed"). Builders add architecture, set dressing
    // and practical lights around the puzzle objects, batched per material; far vistas are built in Landmarks.cs / Vista.cs.
    public partial class BerlinCell
    {
        /// <summary>
        /// A festival departure pavilion rather than a grey box: steel trusses and purlins with glazed skylights, pendant
        /// lights, festival banners, a departures board under a clock stopped at a minute to twelve, wayfinding signs and
        /// painted lines that all lead to GATE 12 (the three false returns, warm, timber-clad and lamp-lit), an empty
        /// information desk, a registration totem and an inlaid ring of eleven seals around the plinth, waiting-area
        /// seating (one row faces the blank wall), luggage trolleys, planted trees and flags on the square. The fourth
        /// (west) wall stays deliberately blank, and nothing in the hall points at it.
        /// </summary>
        void BuildPavilionDetail(Transform g, Material steel, Material concrete)
        {
            var k = new KitBatch();
            // Skylights glow moderately and are broken into panes (playtest: flat white strips). Glowing materials keep a
            // dark albedo so the lamps beside them (1/d^2) cannot add a blow-out on top of the emission.
            var sky = Mats.Emissive(new Color(0.12f, 0.13f, 0.14f), new Color(0.62f, 0.68f, 0.75f));
            var black = Mats.Lit(new Color(0.03f, 0.03f, 0.035f), 0.5f);
            var oak = Mats.Lit(new Color(0.7f, 0.52f, 0.34f), 0.4f, 0f, "wood_oak_floor");
            var panel = Mats.Lit(new Color(0.86f, 0.85f, 0.82f), 0.35f);
            var brass = Mats.Lit(new Color(0.78f, 0.6f, 0.32f), 0.65f, 0.9f, "metal_brushed");
            var chrome = Mats.Lit(new Color(0.75f, 0.76f, 0.78f), 0.8f, 1f);
            var paint = Mats.Lit(new Color(0.9f, 0.7f, 0.16f), 0.3f);
            var red = Mats.Lit(new Color(0.55f, 0.12f, 0.12f), 0.3f, 0f, "fabric_linen");
            var navy = Mats.Lit(new Color(0.12f, 0.2f, 0.38f), 0.3f, 0f, "fabric_linen");
            var gold = Mats.Lit(new Color(0.78f, 0.63f, 0.3f), 0.3f, 0f, "fabric_linen");
            var signBlue = Mats.Lit(new Color(0.08f, 0.14f, 0.3f), 0.4f);
            var seat = Mats.Lit(new Color(0.12f, 0.2f, 0.32f), 0.45f, 0.1f);
            var leaf = Mats.Lit(new Color(0.17f, 0.29f, 0.14f), 0.35f);
            var soil = Mats.Lit(new Color(0.1f, 0.08f, 0.06f), 0.2f);
            var carpet = Mats.Lit(new Color(1f, 0.75f, 0.65f), 0.1f, 0f, "carpet_rug");
            var glow = Mats.Emissive(new Color(0.25f, 0.24f, 0.22f), new Color(0.85f, 0.8f, 0.7f));
            var warmGlow = Mats.Emissive(new Color(0.3f, 0.24f, 0.17f), new Color(1.1f, 0.72f, 0.38f));
            var screen = Mats.Emissive(new Color(0.15f, 0.2f, 0.25f), new Color(0.2f, 0.28f, 0.36f));
            var textGold = new Color(0.93f, 0.78f, 0.45f);

            // ---- roof: trusses, glazed skylights, purlins, pendant lights over the four lamps, a ring over the plinth
            for (int i = 0; i < 6; i++)
            {
                float z = -5.6f + i * 2.9f;
                k.Box(steel, new Vector3(0, 5.75f, z), new Vector3(18f, 0.22f, 0.14f));
                k.Box(steel, new Vector3(0, 5.42f, z), new Vector3(18f, 0.08f, 0.08f));
                for (int d = 0; d < 9; d++) k.Box(steel, new Vector3(-8f + d * 2f, 5.58f, z), new Vector3(0.06f, 0.36f, 0.06f), Quaternion.Euler(0, 0, d % 2 == 0 ? 35f : -35f));
                if (i == 5) continue;
                float sz = z + 1.45f;
                for (int p = 0; p < 8; p++) k.Box(sky, new Vector3(-7f + p * 2f, 5.935f, sz), new Vector3(1.84f, 0.015f, 0.62f));
                foreach (float e in new[] { -0.33f, 0.33f }) k.Box(steel, new Vector3(0f, 5.915f, sz + e), new Vector3(16.2f, 0.04f, 0.05f));
            }
            foreach (float x in new[] { -6f, -3f, 0f, 3f, 6f }) k.Box(steel, new Vector3(x, 5.88f, 1f), new Vector3(0.08f, 0.1f, 15.9f));
            foreach (float x in new[] { -6f, -2f, 2f, 6f })
            {
                var p = new Vector3(x, 5.5f, 1f);
                k.Box(steel, new Vector3(x, 5.84f, 1f), new Vector3(0.012f, 0.22f, 0.012f));
                k.Of(steel).AddLathe(p, new List<Vector2> { new Vector2(0.42f, 0f), new Vector2(0.41f, 0.05f), new Vector2(0.3f, 0.14f), new Vector2(0.12f, 0.2f), new Vector2(0.05f, 0.23f) }, 24);
                k.Of(glow).AddDisc(p + Vector3.up * 0.01f, Vector3.down, 0.39f, 24);
            }
            var plinthTop = new Vector3(0f, 0f, 1.5f);
            k.Of(glow).AddTorus(plinthTop + Vector3.up * 4.6f, 1.0f, 0.03f, 48, 6, Quaternion.identity);
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI * 2f / 3f + 0.5f;
                k.Box(steel, plinthTop + new Vector3(Mathf.Cos(a), 5.28f, Mathf.Sin(a)), new Vector3(0.01f, 1.34f, 0.01f));
            }
            AddPointLight(g, plinthTop + Vector3.up * 3.4f, new Color(1f, 0.93f, 0.82f), 1.0f, 4.2f);

            // ---- the south wall: the departures board under a stopped clock, banners flanking it over planters
            var board = new Vector3(0f, 3.6f, -6.86f);
            k.Box(black, board, new Vector3(7.2f, 2.4f, 0.12f));
            k.Box(steel, board + new Vector3(0, 1.26f, 0), new Vector3(7.4f, 0.1f, 0.16f));
            k.Box(steel, board + new Vector3(0, -1.25f, 0), new Vector3(7.4f, 0.06f, 0.16f));
            foreach (float bx in new[] { -3.66f, 3.66f }) k.Box(steel, board + new Vector3(bx, 0f, 0f), new Vector3(0.08f, 2.56f, 0.16f));
            string rows = "ABFLUG  ·  DEPARTURES                     GATE\n" +
                          "CAIRO              EGYPT          B1\nPARIS              FRANCE         B2\nNEW YORK           USA            B3\n" +
                          "KYOTO              JAPAN          B4\nRIYADH             SAUDI ARABIA   B5\nAGRA               INDIA          B6\n" +
                          "<color=#E6B54A>HOME               ENTRY GRANTED  B12  BOARDING</color>";
            var t = TextFactory.World(g, rows, 0.016f, new Color(0.93f, 0.92f, 0.86f), TMPro.TextAlignmentOptions.TopLeft, false, 6.8f);
            t.transform.localPosition = board + new Vector3(0f, 0f, 0.07f);
            t.transform.localRotation = Quaternion.Euler(0, 180f, 0); // the board faces into the hall (+Z)
            t.rectTransform.sizeDelta = new Vector2(6.8f, 2.2f);
            // A station clock at a minute to twelve (the hall faces +Z: a clockwise angle a from twelve is (-sin a, cos a)).
            var clock = new Vector3(0f, 5.28f, -6.9f);
            k.Of(panel).AddDisc(clock + new Vector3(0f, 0f, 0.035f), Vector3.forward, 0.3f, 36);
            k.Of(black).AddTorus(clock + new Vector3(0f, 0f, 0.03f), 0.31f, 0.025f, 36, 6, Quaternion.Euler(90f, 0f, 0f));
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f * Mathf.Deg2Rad;
                k.Box(black, clock + new Vector3(-Mathf.Sin(a) * 0.24f, Mathf.Cos(a) * 0.24f, 0.04f), new Vector3(0.025f, i % 3 == 0 ? 0.08f : 0.05f, 0.005f), Quaternion.Euler(0f, 0f, i * 30f));
            }
            foreach (var (deg, len, wid) in new[] { (359.5f, 0.15f, 0.03f), (354f, 0.23f, 0.02f) })
            {
                float a = deg * Mathf.Deg2Rad;
                k.Box(black, clock + new Vector3(-Mathf.Sin(a) * len * 0.5f, Mathf.Cos(a) * len * 0.5f, 0.048f), new Vector3(wid, len, 0.006f), Quaternion.Euler(0f, 0f, deg));
            }
            k.Of(red).AddDisc(clock + new Vector3(0f, 0f, 0.052f), Vector3.forward, 0.02f, 12);
            // Two tall banners on the wall: the twelfth pavilion's numeral over the festival's name.
            foreach (var (bx, cloth) in new[] { (-5.2f, red), (5.2f, navy) })
            {
                var c = new Vector3(bx, 3.0f, -6.93f);
                k.Box(cloth, c, new Vector3(1.2f, 3.2f, 0.02f));
                k.Box(gold, c + new Vector3(0f, 1.52f, 0.012f), new Vector3(1.2f, 0.06f, 0.004f));
                k.Box(gold, c + new Vector3(0f, -1.52f, 0.012f), new Vector3(1.2f, 0.06f, 0.004f));
                k.Box(steel, c + new Vector3(0f, 1.63f, 0.01f), new Vector3(1.36f, 0.04f, 0.04f));
                k.Of(gold).AddTorus(c + new Vector3(0f, 0.55f, 0.014f), 0.42f, 0.018f, 40, 5, Quaternion.Euler(90f, 0f, 0f));
                var numeral = TextFactory.World(g, "XII\n<size=20%>FESTIVAL DER PÄSSE\nBERLIN</size>", 0.26f, textGold, TMPro.TextAlignmentOptions.Center, true, 1.1f);
                numeral.transform.localPosition = c + new Vector3(0f, 0.5f, 0.016f); // the numeral and its name sit inside the ring
                numeral.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                numeral.rectTransform.sizeDelta = new Vector2(1.1f, 1.8f);
                Planter(k, concrete, soil, leaf, new Vector3(bx, 0f, -6.45f), 0.75f, 0.5f, false, new System.Random((int)(bx * 10f) + 50));
            }

            // ---- hanging festival banners (overhead, at the sides, facing the arrival)
            int bi = 0;
            foreach (float bz in new[] { 0.2f, 3.1f })
                foreach (float bx in new[] { -4.6f, 4.6f })
                {
                    var cloth = (bi++ % 3 == 1) ? navy : red;
                    var c = new Vector3(bx, 4.15f, bz);
                    k.Box(steel, new Vector3(bx, 5.31f, bz), new Vector3(1.26f, 0.035f, 0.035f));
                    foreach (float sx in new[] { -0.55f, 0.55f }) k.Box(steel, new Vector3(bx + sx, 5.36f, bz), new Vector3(0.01f, 0.09f, 0.01f));
                    k.Box(cloth, c, new Vector3(1.1f, 2.3f, 0.02f));
                    k.Box(gold, c + new Vector3(0f, 1.05f, -0.012f), new Vector3(1.1f, 0.05f, 0.004f));
                    k.Box(gold, c + new Vector3(0f, -1.05f, -0.012f), new Vector3(1.1f, 0.05f, 0.004f));
                    k.Of(gold).AddTorus(c + new Vector3(0f, 0.5f, -0.014f), 0.24f, 0.014f, 32, 5, Quaternion.Euler(90f, 0f, 0f));
                    var bt = TextFactory.World(g, "FESTIVAL\nDER PÄSSE\n<size=55%>BERLIN</size>", 0.085f, textGold, TMPro.TextAlignmentOptions.Center, true, 1.0f);
                    bt.transform.localPosition = c + new Vector3(0f, -0.38f, -0.016f);
                    bt.rectTransform.sizeDelta = new Vector2(1.0f, 0.9f);
                }

            // ---- GATE 12: the three false returns. Timber cladding, sconces, a warm carpet; signs and lines lead here.
            for (float z = -3.7f; z <= 3.71f; z += 0.09f) k.Box(oak, new Vector3(8.935f, 2.1f, z), new Vector3(0.03f, 4.2f, 0.045f));
            k.Box(steel, new Vector3(8.92f, 4.22f, 0f), new Vector3(0.06f, 0.04f, 7.5f));
            for (int i = 0; i < 3; i++)
            {
                var d = new Vector3(8.84f, 0f, -2.4f + i * 2.4f);
                k.Box(steel, d + new Vector3(-0.02f, 2.62f, 0), new Vector3(0.1f, 0.12f, 1.6f));
                foreach (var dz in new[] { -0.78f, 0.78f }) k.Box(steel, d + new Vector3(-0.02f, 1.3f, dz), new Vector3(0.1f, 2.6f, 0.08f));
                k.Box(signBlue, d + new Vector3(-0.05f, 3.05f, 0), new Vector3(0.06f, 0.34f, 1.1f));
                var gl = TextFactory.World(g, "GATE 12", 0.018f, new Color(0.95f, 0.95f, 0.92f), TMPro.TextAlignmentOptions.Center, false, 1.0f);
                gl.transform.localPosition = d + new Vector3(-0.09f, 3.05f, 0);
                gl.transform.localRotation = Quaternion.Euler(0, 90f, 0); // reads from inside the hall
            }
            foreach (float sz in new[] { -3.6f, -1.2f, 1.2f, 3.6f })
            {
                k.Box(brass, new Vector3(8.9f, 2.05f, sz), new Vector3(0.03f, 0.3f, 0.14f));
                k.Box(warmGlow, new Vector3(8.84f, 2.08f, sz), new Vector3(0.1f, 0.2f, 0.1f));
                k.Box(brass, new Vector3(8.84f, 2.2f, sz), new Vector3(0.12f, 0.02f, 0.12f));
            }
            k.Box(carpet, new Vector3(8.07f, 0.004f, 0f), new Vector3(1.56f, 0.008f, 6.8f));
            foreach (float lz in new[] { -1.2f, 1.2f }) AddPointLight(g, new Vector3(7.9f, 2.4f, lz), new Color(1f, 0.72f, 0.45f), 1.1f, 4.5f);

            // ---- wayfinding: a painted line from the arrival to the gates with chevrons; an overhead sign; nothing west
            void Paint(Vector3 a, Vector3 b2)
            {
                var c = (a + b2) * 0.5f; var dv = b2 - a;
                k.Box(paint, new Vector3(c.x, 0.0015f, c.z), new Vector3(Mathf.Abs(dv.x) + 0.12f, 0.003f, Mathf.Abs(dv.z) + 0.12f));
            }
            Paint(new Vector3(1.0f, 0f, -5.0f), new Vector3(1.0f, 0f, -3.2f));
            Paint(new Vector3(1.0f, 0f, -3.2f), new Vector3(7.1f, 0f, -3.2f));
            Paint(new Vector3(7.1f, 0f, -3.2f), new Vector3(7.1f, 0f, 2.4f));
            foreach (float gz in new[] { -2.4f, 0f, 2.4f }) Paint(new Vector3(7.1f, 0f, gz), new Vector3(7.3f, 0f, gz));
            void Chevron(Vector3 p, float yaw)
            {
                var q = Quaternion.Euler(0f, yaw, 0f);
                foreach (float s in new[] { -1f, 1f })
                    k.Box(paint, p + q * new Vector3(s * 0.09f, 0.0015f, -0.07f), new Vector3(0.07f, 0.003f, 0.26f), q * Quaternion.Euler(0f, -s * 40f, 0f));
            }
            for (float x = 2.2f; x < 6.6f; x += 1.5f) Chevron(new Vector3(x, 0f, -3.55f), 90f);
            for (float z = -2.0f; z < 2.0f; z += 1.5f) Chevron(new Vector3(6.75f, 0f, z), 0f);
            var signA = new Vector3(3.0f, 3.95f, -2.7f);
            k.Box(black, signA, new Vector3(2.8f, 0.5f, 0.06f));
            k.Box(paint, signA + new Vector3(0f, -0.235f, 0f), new Vector3(2.8f, 0.03f, 0.065f));
            foreach (float sx in new[] { -1.2f, 1.2f }) k.Box(steel, signA + new Vector3(sx, 0.71f, 0f), new Vector3(0.015f, 0.92f, 0.015f));
            var sf = TextFactory.World(g, "GATE 12  →\n<size=48%><color=#E8E6DE>Abflug  ·  Departures</color></size>", 0.11f, new Color(0.95f, 0.76f, 0.2f), TMPro.TextAlignmentOptions.Center, false, 2.7f);
            sf.transform.localPosition = signA + new Vector3(0f, 0.01f, -0.035f);
            sf.rectTransform.sizeDelta = new Vector2(2.7f, 0.48f);
            var sb = TextFactory.World(g, "←  GATE 12", 0.11f, new Color(0.95f, 0.76f, 0.2f), TMPro.TextAlignmentOptions.Center, false, 2.7f);
            sb.transform.localPosition = signA + new Vector3(0f, 0.02f, 0.035f);
            sb.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            sb.rectTransform.sizeDelta = new Vector2(2.7f, 0.48f);

            // ---- the empty information desk (south-east), its sign, a desk lamp
            var desk = new Vector3(5.3f, 0f, -4.3f);
            k.Box(panel, desk + new Vector3(0f, 0.55f, 0.28f), new Vector3(2.4f, 1.1f, 0.06f));
            k.Box(oak, desk + new Vector3(0f, 1.12f, 0.14f), new Vector3(2.5f, 0.04f, 0.44f));
            foreach (float ex in new[] { -1.2f, 1.2f }) k.Box(panel, desk + new Vector3(ex, 0.55f, 0f), new Vector3(0.06f, 1.1f, 0.6f));
            k.Box(oak, desk + new Vector3(0f, 0.76f, -0.12f), new Vector3(2.34f, 0.03f, 0.5f));
            k.Box(red, desk + new Vector3(0f, 0.27f, 0.315f), new Vector3(2.4f, 0.07f, 0.008f));
            k.Box(navy, desk + new Vector3(0f, 0.19f, 0.315f), new Vector3(2.4f, 0.07f, 0.008f));
            k.Box(gold, desk + new Vector3(0f, 0.11f, 0.315f), new Vector3(2.4f, 0.07f, 0.008f));
            k.Of(signBlue).AddDisc(desk + new Vector3(-0.6f, 0.72f, 0.316f), Vector3.forward, 0.17f, 28);
            var info = TextFactory.World(g, "i", 0.22f, new Color(0.95f, 0.95f, 0.92f), TMPro.TextAlignmentOptions.Center, true, 0.3f);
            info.transform.localPosition = desk + new Vector3(-0.6f, 0.72f, 0.32f);
            info.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            k.Box(black, desk + new Vector3(0.45f, 1.36f, 0.06f), new Vector3(0.5f, 0.32f, 0.03f));
            k.Box(screen, desk + new Vector3(0.45f, 1.365f, 0.077f), new Vector3(0.46f, 0.27f, 0.004f));
            k.Box(black, desk + new Vector3(0.45f, 1.17f, 0.04f), new Vector3(0.05f, 0.08f, 0.05f));
            k.Of(brass).AddSphere(desk + new Vector3(-0.1f, 1.14f, 0.22f), 0.035f, 12, 8, 1f, 0f, 90f);
            k.Of(brass).AddCylinder(desk + new Vector3(-0.1f, 1.14f, 0.22f), 0.045f, 0.006f, 14);
            var lamp = desk + new Vector3(-0.95f, 1.14f, -0.05f);
            k.Of(brass).AddCylinder(lamp, 0.07f, 0.015f, 14);
            k.Of(brass).AddTube(new List<Vector3> { lamp, lamp + new Vector3(0.05f, 0.3f, 0f), lamp + new Vector3(0.18f, 0.38f, 0f) }, 0.008f, 6, false);
            k.Of(warmGlow).AddFrustum(lamp + new Vector3(0.18f, 0.3f, 0f), 0.075f, 0.03f, 0.1f, 14);
            // A rack of leaflets at the desk's east end, the stool nobody sits on.
            var rack = desk + new Vector3(1.55f, 0f, 0.05f);
            k.Box(steel, rack + new Vector3(0f, 0.6f, 0.08f), new Vector3(0.4f, 1.2f, 0.03f));
            var leaflets = new[] { red, navy, gold, panel };
            for (int r = 0; r < 4; r++)
            {
                k.Box(steel, rack + new Vector3(0f, 0.35f + r * 0.25f, 0f), new Vector3(0.4f, 0.015f, 0.14f));
                for (int c = 0; c < 2; c++) k.Box(leaflets[(r + c) % 4], rack + new Vector3(-0.1f + c * 0.2f, 0.45f + r * 0.25f, 0.03f), new Vector3(0.14f, 0.2f, 0.02f), Quaternion.Euler(-12f, 0f, 0f));
            }
            k.Of(steel).AddCylinder(desk + new Vector3(0.3f, 0f, -0.45f), 0.02f, 0.65f, 8);
            k.Of(black).AddCylinder(desk + new Vector3(0.3f, 0.65f, -0.45f), 0.18f, 0.05f, 18);
            k.Of(steel).AddCylinder(desk + new Vector3(0.3f, 0f, -0.45f), 0.2f, 0.02f, 18);
            var infoSign = new Vector3(5.3f, 4.1f, -5.6f);
            k.Box(black, infoSign, new Vector3(2.3f, 0.42f, 0.06f));
            foreach (float sx in new[] { -1f, 1f }) k.Box(steel, infoSign + new Vector3(sx, 0.75f, 0f), new Vector3(0.015f, 1.08f, 0.015f));
            var it = TextFactory.World(g, "<b>i</b>   Information  ·  Auskunft", 0.085f, new Color(0.92f, 0.92f, 0.88f), TMPro.TextAlignmentOptions.Center, false, 2.2f);
            it.transform.localPosition = infoSign + new Vector3(0f, 0f, 0.035f);
            it.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            it.rectTransform.sizeDelta = new Vector2(2.2f, 0.4f);
            AddPointLight(g, desk + new Vector3(-0.3f, 2.1f, 0.1f), new Color(1f, 0.8f, 0.55f), 0.8f, 3.5f);

            // ---- registration: the queue line, a totem, the inlaid ring of eleven seals around the plinth
            for (int i = 0; i < 5; i++)
            {
                var p = new Vector3(-1.6f + i * 0.8f, 0f, -2.2f);
                k.Box(chrome, p + Vector3.up * 0.48f, new Vector3(0.06f, 0.96f, 0.06f));
                k.Box(chrome, p + Vector3.up * 0.02f, new Vector3(0.3f, 0.04f, 0.3f));
                if (i < 4) k.Box(red, p + new Vector3(0.4f, 0.88f, 0), new Vector3(0.76f, 0.05f, 0.012f));
            }
            var totem = new Vector3(-2.4f, 0f, -2.6f);
            k.Box(black, totem + new Vector3(0f, 0.95f, 0f), new Vector3(0.55f, 1.9f, 0.22f));
            k.Box(screen, totem + new Vector3(0f, 1.25f, -0.112f), new Vector3(0.46f, 0.82f, 0.004f));
            k.Box(steel, totem + new Vector3(0f, 0.02f, 0f), new Vector3(0.7f, 0.04f, 0.4f));
            var tt = TextFactory.World(g, "REGISTRIERUNG\n<size=72%>Registration</size>\n\n<size=60%>11 Siegel  ·  11 seals</size>", 0.03f, new Color(0.9f, 0.93f, 0.95f), TMPro.TextAlignmentOptions.Center, false, 0.44f);
            tt.transform.localPosition = totem + new Vector3(0f, 1.3f, -0.118f);
            tt.rectTransform.sizeDelta = new Vector2(0.44f, 0.7f);
            foreach (float r in new[] { 1.45f, 1.62f }) k.Of(brass).AddTorus(plinthTop + Vector3.up * 0.002f, r, 0.012f, 64, 4, Quaternion.identity);
            for (int i = 0; i < 11; i++)
            {
                float a = i / 11f * Mathf.PI * 2f;
                k.Of(brass).AddDisc(plinthTop + new Vector3(Mathf.Cos(a) * 1.535f, 0.004f, Mathf.Sin(a) * 1.535f), Vector3.up, 0.055f, 16);
            }

            // ---- waiting area: beam seating (one row faces the blank wall), a paper cup and a newspaper left behind
            Seats(k, steel, chrome, seat, new Vector3(-5.2f, 0f, -1.2f), 0f);
            Seats(k, steel, chrome, seat, new Vector3(-5.4f, 0f, 2.8f), 90f);
            Seats(k, steel, chrome, seat, new Vector3(4.6f, 0f, 5.6f), 0f);
            Seats(k, steel, chrome, seat, new Vector3(4.6f, 0f, 3.0f), 0f);
            k.Of(panel).AddFrustum(new Vector3(-4.6f, 0.478f, -1.15f), 0.03f, 0.042f, 0.11f, 12);
            k.Box(panel, new Vector3(5.2f, 0.487f, 3.05f), new Vector3(0.34f, 0.02f, 0.27f), Quaternion.Euler(0f, 14f, 0f));
            k.Box(panel, new Vector3(0.9f, 0.002f, -2.75f), new Vector3(0.09f, 0.002f, 0.2f), Quaternion.Euler(0f, 33f, 0f));

            // ---- luggage trolleys: a nested row by the gates, two left with bags
            for (int i = 0; i < 5; i++) Trolley(k, chrome, black, new Vector3(8.15f, 0f, 4.3f + i * 0.32f), 0f, null, null);
            Trolley(k, chrome, black, new Vector3(2.6f, 0f, -5.9f), 20f, red, navy);
            Trolley(k, chrome, black, new Vector3(-2.8f, 0f, 5.0f), -35f, navy, null);

            // ---- trees in the corners by the glass
            Planter(k, concrete, soil, leaf, new Vector3(-5.6f, 0f, 7.7f), 1.0f, 0.55f, true, new System.Random(3));
            Planter(k, concrete, soil, leaf, new Vector3(6.9f, 0f, 7.7f), 1.0f, 0.55f, true, new System.Random(4));

            // ---- the square: festival flags and lamps either side of the view to the gate (the axis stays clear)
            var flags = new[] { red, navy, gold };
            int fi = 0;
            foreach (float fz in new[] { 16f, 27f })
                foreach (float fx in new[] { -16f, -10f, 10f, 16f })
                {
                    var foot = new Vector3(fx, -0.05f, fz);
                    k.Box(concrete, foot + Vector3.up * 0.15f, new Vector3(0.5f, 0.3f, 0.5f));
                    k.Box(steel, foot + Vector3.up * 4.6f, new Vector3(0.09f, 9f, 0.09f));
                    k.Box(flags[fi++ % 3], foot + new Vector3(fx < 0 ? 0.75f : -0.75f, 8.4f, 0f), new Vector3(1.4f, 0.9f, 0.02f));
                }
            foreach (float lz in new[] { 14f, 32f })
                foreach (float lx in new[] { -7f, 7f })
                {
                    var foot = new Vector3(lx, -0.05f, lz);
                    k.Box(steel, foot + Vector3.up * 2.2f, new Vector3(0.1f, 4.4f, 0.1f));
                    k.Box(warmGlow, foot + Vector3.up * 4.45f, new Vector3(0.36f, 0.22f, 0.36f));
                    k.Box(steel, foot + Vector3.up * 4.6f, new Vector3(0.44f, 0.04f, 0.44f));
                }
            k.Bake(g, "PavilionDetail");
        }

        /// <summary>Five beam seats facing local -Z: a steel beam on T-legs, seat pans and backs, chrome armrests between.</summary>
        static void Seats(KitBatch k, Material steel, Material chrome, Material seat, Vector3 c, float yaw)
        {
            var q = Quaternion.Euler(0f, yaw, 0f);
            void B(Material m, Vector3 o, Vector3 s, Quaternion r) => k.Box(m, c + q * o, s, q * r);
            B(steel, new Vector3(0f, 0.36f, 0.08f), new Vector3(3.05f, 0.08f, 0.1f), Quaternion.identity);
            foreach (float lx in new[] { -1.35f, 1.35f })
            {
                B(steel, new Vector3(lx, 0.18f, 0.08f), new Vector3(0.07f, 0.36f, 0.07f), Quaternion.identity);
                B(steel, new Vector3(lx, 0.015f, 0.08f), new Vector3(0.08f, 0.03f, 0.62f), Quaternion.identity);
            }
            for (int s = 0; s < 5; s++)
            {
                float x = -1.2f + s * 0.6f;
                B(seat, new Vector3(x, 0.45f, 0.02f), new Vector3(0.5f, 0.05f, 0.46f), Quaternion.Euler(4f, 0f, 0f));
                B(seat, new Vector3(x, 0.76f, 0.27f), new Vector3(0.5f, 0.5f, 0.04f), Quaternion.Euler(9f, 0f, 0f));
                B(steel, new Vector3(x, 0.42f, 0.2f), new Vector3(0.04f, 0.1f, 0.06f), Quaternion.identity);
                if (s < 4)
                {
                    B(chrome, new Vector3(x + 0.3f, 0.62f, 0.02f), new Vector3(0.04f, 0.03f, 0.4f), Quaternion.identity);
                    B(chrome, new Vector3(x + 0.3f, 0.52f, 0.18f), new Vector3(0.03f, 0.2f, 0.03f), Quaternion.identity);
                }
            }
        }

        /// <summary>A luggage trolley facing local -Z (handle at the back), optionally loaded with a case and a bag.</summary>
        static void Trolley(KitBatch k, Material chrome, Material black, Vector3 c, float yaw, Material caseA, Material caseB)
        {
            var q = Quaternion.Euler(0f, yaw, 0f);
            void B(Material m, Vector3 o, Vector3 s, Quaternion r) => k.Box(m, c + q * o, s, q * r);
            B(chrome, new Vector3(0f, 0.2f, 0f), new Vector3(0.56f, 0.03f, 0.82f), Quaternion.identity);
            B(chrome, new Vector3(0f, 0.32f, -0.42f), new Vector3(0.56f, 0.22f, 0.025f), Quaternion.identity);
            foreach (float sx in new[] { -0.27f, 0.27f })
            {
                B(chrome, new Vector3(sx, 0.62f, 0.36f), new Vector3(0.03f, 0.88f, 0.03f), Quaternion.Euler(8f, 0f, 0f));
                B(chrome, new Vector3(sx, 0.62f, 0.1f), new Vector3(0.025f, 0.025f, 0.55f), Quaternion.Euler(-30f, 0f, 0f));
            }
            B(black, new Vector3(0f, 1.06f, 0.43f), new Vector3(0.62f, 0.04f, 0.04f), Quaternion.identity);
            foreach (float wx in new[] { -0.22f, 0.22f })
                foreach (float wz in new[] { -0.33f, 0.33f })
                {
                    B(chrome, new Vector3(wx, 0.15f, wz), new Vector3(0.02f, 0.08f, 0.02f), Quaternion.identity);
                    k.Of(black).AddCylinder(c + q * new Vector3(wx + 0.02f, 0.06f, wz), 0.06f, 0.04f, 10, 1f, q * Quaternion.Euler(0f, 0f, 90f)); // caster
                }
            if (caseA != null)
            {
                k.Of(caseA).AddRoundedPlate(c + q * new Vector3(0f, 0.53f, -0.02f), 0.48f, 0.62f, 0.05f, 0.24f, q * Quaternion.Euler(0f, 0f, 0f));
                B(black, new Vector3(0f, 0.87f, -0.02f), new Vector3(0.16f, 0.03f, 0.03f), Quaternion.identity);
            }
            if (caseB != null) k.Of(caseB).AddCylinder(c + q * new Vector3(-0.25f, 0.97f, 0.02f), 0.13f, 0.5f, 12, 1f, q * Quaternion.Euler(0f, 0f, -90f)); // a duffel on top
        }

        /// <summary>A concrete planter with soil and either a small tree (a trunk under a cloud of foliage) or a low shrub.</summary>
        static void Planter(KitBatch k, Material concrete, Material soil, Material leaf, Vector3 foot, float size, float height, bool tree, System.Random rnd)
        {
            k.Box(concrete, foot + Vector3.up * height * 0.5f, new Vector3(size, height, size));
            k.Box(soil, foot + Vector3.up * (height - 0.02f), new Vector3(size - 0.08f, 0.02f, size - 0.08f));
            var top = foot + Vector3.up * height;
            if (tree)
            {
                k.Of(soil).AddTube(new List<Vector3> { top, top + new Vector3(0.05f, 0.9f, -0.03f), top + new Vector3(-0.04f, 1.6f, 0.02f) }, 0.05f, 6, false);
                for (int i = 0; i < 10; i++)
                {
                    float a = i * 2.2f, r = 0.15f + (i % 3) * 0.2f;
                    k.Of(leaf).AddSphere(top + new Vector3(Mathf.Cos(a) * r, 1.45f + (i % 4) * 0.22f + (float)rnd.NextDouble() * 0.1f, Mathf.Sin(a) * r), 0.32f + (float)rnd.NextDouble() * 0.16f, 10, 8);
                }
            }
            else
                for (int i = 0; i < 4; i++)
                {
                    float a = i * 1.7f;
                    k.Of(leaf).AddSphere(top + new Vector3(Mathf.Cos(a) * 0.14f, 0.18f + (i % 2) * 0.1f, Mathf.Sin(a) * 0.14f), 0.24f, 10, 8);
                }
        }

        /// <summary>
        /// Each false return dressed as a front door from home, the lure the finale asks you to refuse: an architrave, a
        /// panelled honey-oak leaf, a brass lever, kick plate and threshold, a coir mat. Built under the door (local -Z faces
        /// the hall, the wall face is at z = +0.05), so all of it goes when the forgery is voided.
        /// </summary>
        static void BuildDecoyDoor(Transform d)
        {
            var k = new KitBatch();
            var leaf = Mats.Lit(new Color(0.55f, 0.34f, 0.2f), 0.45f, 0f, "wood_oak_floor");
            var brass = Mats.Lit(new Color(0.8f, 0.62f, 0.33f), 0.65f, 0.9f, "metal_brushed");
            var coir = Mats.Lit(new Color(0.42f, 0.3f, 0.18f), 0.1f, 0f, "fabric_linen");
            k.Box(leaf, new Vector3(0f, 1.05f, 0.02f), new Vector3(0.92f, 2.1f, 0.04f));
            foreach (float px in new[] { -0.2f, 0.2f })
                foreach (var (py, ph) in new[] { (0.47f, 0.62f), (1.17f, 0.62f), (1.76f, 0.4f) })
                    k.Box(leaf, new Vector3(px, py, -0.006f), new Vector3(0.3f, ph, 0.012f));
            foreach (float ax in new[] { -0.53f, 0.53f }) k.Box(leaf, new Vector3(ax, 1.1f, -0.005f), new Vector3(0.1f, 2.2f, 0.11f));
            k.Box(leaf, new Vector3(0f, 2.15f, -0.005f), new Vector3(1.16f, 0.1f, 0.11f));
            k.Box(brass, new Vector3(0.36f, 1.0f, -0.012f), new Vector3(0.05f, 0.12f, 0.012f));
            k.Box(brass, new Vector3(0.36f, 1.02f, -0.03f), new Vector3(0.018f, 0.018f, 0.05f));
            k.Box(brass, new Vector3(0.31f, 1.02f, -0.05f), new Vector3(0.12f, 0.018f, 0.018f));
            k.Box(brass, new Vector3(0f, 0.1f, -0.003f), new Vector3(0.86f, 0.16f, 0.006f));
            k.Box(brass, new Vector3(0f, 0.008f, -0.08f), new Vector3(1.06f, 0.016f, 0.16f));
            k.Of(brass).AddCylinder(new Vector3(0f, 1.55f, 0f), 0.012f, 0.012f, 10, 1f, Quaternion.Euler(-90f, 0f, 0f));
            k.Box(coir, new Vector3(0f, 0.008f, -0.45f), new Vector3(0.85f, 0.016f, 0.5f));
            k.Bake(d, "HomeDoor");
        }
    }
}
