using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>Materials of the Rio carnival detail (shared by the street, the float and the figures).</summary>
    class CarnivalMats
    {
        public Material Gold, Chrome, DarkMetal, Skin, DrumHead, Wood;
        public Material SequinGold, SequinMagenta, SequinTurquoise;
        /// <summary>Saturated, faintly self-lit carnival colours: magenta, yellow, turquoise, white, orange, violet, green.</summary>
        public Material[] Plume;
        /// <summary>Emissive bulb colours: warm, red, green, cyan, magenta (the last two also light the gel heads).</summary>
        public Material[] Bulbs;
        public Material Sequin(int i) => i % 3 == 0 ? SequinGold : i % 3 == 1 ? SequinMagenta : SequinTurquoise;
    }

    public partial class RioCell
    {
        enum FigurePose { Samba, Wave, Drum, Cheer, Seated }

        /// <summary>
        /// A faceless store mannequin frozen mid-movement (no real person: smooth glossy limbs, an egg for a head).
        /// Local frame: feet at <paramref name="feet"/>, facing yaw. Returns world points for costume pieces.
        /// </summary>
        static void Mannequin(KitBatch k, CarnivalMats m, Vector3 feet, float yaw, FigurePose pose, float s, out Vector3 handR, out Vector3 handL, out Quaternion rot)
        {
            var r = Quaternion.Euler(0f, yaw, 0f);
            rot = r;
            Vector3 W(float x, float y, float z) => feet + r * (new Vector3(x, y, z) * s);
            var b = k.Of(m.Skin);
            Vector3 hip, chest, footR, footL, kneeR, kneeL, elbowR, elbowL;
            switch (pose)
            {
                case FigurePose.Samba:
                    hip = W(0.02f, 0.94f, 0f); chest = W(0.04f, 1.38f, 0.03f);
                    footR = W(0.13f, 0.05f, 0.2f); kneeR = W(0.14f, 0.5f, 0.14f); footL = W(-0.1f, 0.13f, -0.16f); kneeL = W(-0.1f, 0.5f, -0.02f);
                    elbowR = W(0.33f, 1.62f, 0.06f); handR = W(0.4f, 1.95f, 0.12f); elbowL = W(-0.41f, 1.36f, 0.04f); handL = W(-0.63f, 1.44f, 0.14f);
                    break;
                case FigurePose.Wave:
                    hip = W(0f, 0.95f, 0f); chest = W(0f, 1.39f, 0.02f);
                    footR = W(0.14f, 0.05f, 0.03f); kneeR = W(0.13f, 0.5f, 0.05f); footL = W(-0.14f, 0.05f, -0.02f); kneeL = W(-0.13f, 0.5f, 0f);
                    elbowR = W(0.34f, 1.7f, 0f); handR = W(0.47f, 2.0f, 0.06f); elbowL = W(-0.34f, 1.7f, 0f); handL = W(-0.47f, 2.0f, 0.06f);
                    break;
                case FigurePose.Drum:
                    hip = W(0f, 0.95f, 0f); chest = W(0f, 1.38f, -0.03f);
                    footR = W(0.17f, 0.05f, 0.05f); kneeR = W(0.15f, 0.5f, 0.08f); footL = W(-0.17f, 0.05f, 0f); kneeL = W(-0.15f, 0.5f, 0.04f);
                    elbowR = W(0.31f, 1.56f, 0.16f); handR = W(0.27f, 1.84f, 0.32f); elbowL = W(-0.28f, 1.2f, 0.18f); handL = W(-0.13f, 1.22f, 0.42f);
                    break;
                case FigurePose.Seated:
                    hip = W(0f, 0.47f, 0f); chest = W(0f, 0.9f, -0.02f);
                    footR = W(0.12f, 0.03f, 0.48f); kneeR = W(0.12f, 0.5f, 0.42f); footL = W(-0.12f, 0.03f, 0.44f); kneeL = W(-0.12f, 0.5f, 0.42f);
                    elbowR = W(0.26f, 1.18f, 0.06f); handR = W(0.3f, 1.48f, 0.1f); elbowL = W(-0.3f, 0.72f, 0.18f); handL = W(-0.16f, 0.55f, 0.32f);
                    break;
                default: // Cheer
                    hip = W(0f, 0.95f, 0f); chest = W(0f, 1.39f, 0f);
                    footR = W(0.12f, 0.05f, 0f); kneeR = W(0.12f, 0.5f, 0.02f); footL = W(-0.12f, 0.05f, 0.02f); kneeL = W(-0.12f, 0.5f, 0.04f);
                    elbowR = W(0.24f, 1.76f, 0f); handR = W(0.28f, 2.06f, 0.03f); elbowL = W(-0.3f, 1.2f, 0.1f); handL = W(-0.2f, 1.38f, 0.26f);
                    break;
            }
            var up = (chest - hip).normalized;
            var right = r * Vector3.right;
            var shoulderR = chest + up * 0.03f * s + right * 0.19f * s;
            var shoulderL = chest + up * 0.03f * s - right * 0.19f * s;
            var hipR = hip + right * 0.1f * s - up * 0.02f * s;
            var hipL = hip - right * 0.1f * s - up * 0.02f * s;
            Limb(b, hip - up * 0.04f * s, chest, 0.13f * s, 0.155f * s);
            DetailShapes.Ellipsoid(b, hip, 0.15f * s, 0.1f * s, 8, 4);
            DetailShapes.Ellipsoid(b, chest, 0.19f * s, 0.07f * s, 8, 4);
            Limb(b, chest + up * 0.03f * s, chest + up * 0.15f * s, 0.045f * s, 0.04f * s, 6);
            DetailShapes.Ellipsoid(b, chest + up * 0.26f * s + r * Vector3.forward * 0.01f * s, 0.088f * s, 0.115f * s, 9, 6);
            Limb(b, hipR, kneeR, 0.075f * s, 0.055f * s); Limb(b, kneeR, footR, 0.055f * s, 0.04f * s);
            Limb(b, hipL, kneeL, 0.075f * s, 0.055f * s); Limb(b, kneeL, footL, 0.055f * s, 0.04f * s);
            Limb(b, shoulderR, elbowR, 0.048f * s, 0.038f * s); Limb(b, elbowR, handR, 0.038f * s, 0.03f * s);
            Limb(b, shoulderL, elbowL, 0.048f * s, 0.038f * s); Limb(b, elbowL, handL, 0.038f * s, 0.03f * s);
            foreach (var j in new[] { kneeR, kneeL, elbowR, elbowL, shoulderR, shoulderL })
                b.AddSphere(j, 0.05f * s, 6, 4);
            foreach (var h in new[] { handR, handL }) b.AddSphere(h, 0.04f * s, 6, 4);
            var fwd = r * Vector3.forward;
            foreach (var f in new[] { footR, footL })
                b.AddBox(f + fwd * 0.05f * s - Vector3.up * 0.015f * s, new Vector3(0.08f, 0.05f, 0.2f) * s, r);
        }

        static void Limb(MeshBuilder b, Vector3 a, Vector3 c, float ra, float rc, int seg = 7)
        {
            var d = c - a;
            if (d.sqrMagnitude < 1e-6f) return;
            b.AddFrustum(a, ra, rc, d.magnitude, seg, true, true, 1f, Quaternion.FromToRotation(Vector3.up, d));
        }

        /// <summary>
        /// A passista frozen mid-samba: sequinned bodice and flared belt, a feather headdress, and a great fan of
        /// plumes on a gold frame behind the shoulders.
        /// </summary>
        static void Dancer(KitBatch k, CarnivalMats m, Vector3 feet, float yaw, FigurePose pose, int colour, float s = 1f)
        {
            Mannequin(k, m, feet, yaw, pose, s, out var handR, out var handL, out var r);
            Vector3 W(float x, float y, float z) => feet + r * (new Vector3(x, y, z) * s);
            var sequin = m.Sequin(colour);
            k.Of(sequin).AddFrustum(W(0f, 1.06f, 0.01f), 0.15f * s, 0.168f * s, 0.34f * s, 10);
            k.Of(sequin).AddFrustum(W(0f, 0.78f, 0f), 0.33f * s, 0.15f * s, 0.2f * s, 12);
            k.Of(m.Gold).AddTorus(W(0f, 0.985f, 0f), 0.155f * s, 0.018f * s, 12, 4, Quaternion.identity);
            foreach (var h in new[] { handR, handL }) k.Of(m.Gold).AddTorus(h - Vector3.up * 0.06f * s, 0.04f * s, 0.012f * s, 8, 3, Quaternion.identity);
            // Headdress: a gold band and a crown of plumes.
            var head = W(0f, 1.66f, 0.01f);
            k.Of(m.Gold).AddTorus(head + Vector3.up * 0.04f * s, 0.095f * s, 0.016f * s, 12, 4, Quaternion.identity);
            var fwd = r * Vector3.forward; var right = r * Vector3.right;
            for (int i = 0; i < 9; i++)
            {
                float a = Mathf.Lerp(-55f, 55f, i / 8f) * Mathf.Deg2Rad;
                var dir = (Vector3.up * Mathf.Cos(a) + right * Mathf.Sin(a)).normalized;
                DetailShapes.Feather(k.Of(m.Plume[(colour + i) % 3 == 0 ? 3 : colour % m.Plume.Length]), head + Vector3.up * 0.08f * s - fwd * 0.03f * s, dir, fwd, (0.6f - Mathf.Abs(a) * 0.12f) * s, 0.12f * s, -fwd + dir * 0.3f, 0.18f, 5);
            }
            // Back fan on a gold arc.
            var root = W(0f, 1.26f, -0.15f);
            for (int i = 0; i < 15; i++)
            {
                float a = Mathf.Lerp(-82f, 82f, i / 14f) * Mathf.Deg2Rad;
                var dir = (Vector3.up * Mathf.Cos(a) + right * Mathf.Sin(a)).normalized;
                float len = (1.35f - Mathf.Abs(a) * 0.22f) * s;
                var mat = m.Plume[i % 2 == 0 ? colour % m.Plume.Length : 3];
                DetailShapes.Feather(k.Of(mat), root + dir * 0.32f * s, dir, fwd, len, 0.21f * s, -fwd - Vector3.up * 0.25f, 0.16f, 6);
            }
            for (int i = 0; i < 12; i++)
            {
                float a0 = Mathf.Lerp(-88f, 88f, i / 12f) * Mathf.Deg2Rad, a1 = Mathf.Lerp(-88f, 88f, (i + 1) / 12f) * Mathf.Deg2Rad;
                var p0 = root + (Vector3.up * Mathf.Cos(a0) + right * Mathf.Sin(a0)) * 0.36f * s;
                var p1 = root + (Vector3.up * Mathf.Cos(a1) + right * Mathf.Sin(a1)) * 0.36f * s;
                DetailShapes.PlaneStrip(k, m.Gold, p0, p1, fwd, 0.05f * s, 0.03f * s, 0.02f);
            }
        }

        /// <summary>A drummer of the frozen bateria: white hat, sequinned shirt, a surdo at the hip, the mallet raised.</summary>
        static void Drummer(KitBatch k, CarnivalMats m, Vector3 feet, float yaw, int colour)
        {
            Mannequin(k, m, feet, yaw, FigurePose.Drum, 1f, out var handR, out _, out var r);
            Vector3 W(float x, float y, float z) => feet + r * new Vector3(x, y, z);
            k.Of(m.Sequin(colour)).AddFrustum(W(0f, 1.0f, -0.01f), 0.155f, 0.17f, 0.42f, 10);
            k.Of(m.DrumHead).AddCylinder(W(0f, 1.73f, 0.01f), 0.17f, 0.015f, 14);
            k.Of(m.DrumHead).AddCylinder(W(0f, 1.74f, 0.01f), 0.095f, 0.11f, 12);
            k.Of(m.Plume[colour % m.Plume.Length]).AddCylinder(W(0f, 1.745f, 0.01f), 0.098f, 0.03f, 12);
            Surdo(k, m, W(0f, 0.96f, 0.43f), r * Quaternion.Euler(-14f, 0f, 0f), 0.27f, 0.42f, colour);
            var fwd = r * Vector3.forward;
            var tip = handR + fwd * 0.3f + Vector3.up * 0.05f;
            Limb(k.Of(m.Wood), handR - fwd * 0.04f, tip, 0.012f, 0.012f, 5);
            k.Of(m.DrumHead).AddSphere(tip, 0.045f, 7, 5);
            Limb(k.Of(m.DarkMetal), W(-0.17f, 1.42f, 0.02f), W(0.16f, 0.98f, 0.12f), 0.02f, 0.02f, 4);
        }

        /// <summary>A surdo: chrome shell, white heads, rims, a coloured band and tension rods. Centre and tilt given.</summary>
        static void Surdo(KitBatch k, CarnivalMats m, Vector3 centre, Quaternion tilt, float radius, float height, int colour)
        {
            var axis = tilt * Vector3.up;
            var b0 = centre - axis * (height * 0.5f);
            k.Of(m.Chrome).AddCylinder(b0, radius, height, 16, 1f, tilt);
            k.Of(m.DrumHead).AddCylinder(b0 + axis * height, radius * 0.97f, 0.008f, 16, 1f, tilt);
            k.Of(m.Plume[colour % m.Plume.Length]).AddCylinder(centre - axis * 0.05f, radius * 1.012f, 0.1f, 16, 1f, tilt);
            foreach (float t in new[] { 0f, 1f })
                k.Of(m.Chrome).AddTorus(b0 + axis * (height * t), radius * 1.01f, 0.018f, 16, 4, tilt);
            for (int i = 0; i < 8; i++)
            {
                var d = tilt * (Quaternion.Euler(0f, i * 45f, 0f) * Vector3.right);
                Limb(k.Of(m.Chrome), b0 + d * (radius * 1.05f) + axis * 0.02f, b0 + d * (radius * 1.05f) + axis * (height - 0.02f), 0.007f, 0.007f, 4);
            }
        }
    }
}
