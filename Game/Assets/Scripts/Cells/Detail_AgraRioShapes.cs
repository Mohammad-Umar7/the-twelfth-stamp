using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// Shape helpers shared by the Agra and Rio detail passes: vertical ellipsoids (foliage lobes), curved double-sided
    /// feathers and leaves, twisted ribbons (frozen streamers), strips between two points (inlay lines, arch outlines)
    /// and arch / polygon outlines. Everything goes into a MeshBuilder so the caller can batch it per material.
    /// </summary>
    static class DetailShapes
    {
        /// <summary>Ellipsoid with a vertical axis: <paramref name="rx"/> radius, <paramref name="ry"/> half-height. Smooth.</summary>
        public static void Ellipsoid(MeshBuilder b, Vector3 c, float rx, float ry, int seg = 8, int rows = 5, float uv = 1f)
        {
            var prof = new List<Vector2>(rows + 1);
            for (int i = 0; i <= rows; i++)
            {
                float t = Mathf.Lerp(-90f, 90f, i / (float)rows) * Mathf.Deg2Rad;
                prof.Add(new Vector2(Mathf.Cos(t) * rx, Mathf.Sin(t) * ry));
            }
            prof[0] = new Vector2(0.0005f, -ry);
            prof[rows] = new Vector2(0.0005f, ry);
            b.AddLathe(c, prof, seg, uv);
        }

        /// <summary>
        /// A curved, tapered blade (feather, plume, leaf), double-sided. It leaves <paramref name="root"/> along
        /// <paramref name="dir"/> and bends toward <paramref name="bend"/> (droop); <paramref name="normal"/> is the face.
        /// Width swells to <paramref name="width"/> a third of the way up and closes to a point.
        /// </summary>
        public static void Feather(MeshBuilder b, Vector3 root, Vector3 dir, Vector3 normal, float length, float width, Vector3 bend, float droop, int segs = 6, float rootWidth = 0.12f)
        {
            dir.Normalize();
            Vector3 P(float t) => root + dir * (length * t) + bend * (length * droop * t * t);
            Vector3 prevL = Vector3.zero, prevR = Vector3.zero;
            for (int i = 0; i <= segs; i++)
            {
                float t = i / (float)segs;
                var p = P(t);
                var tan = (dir * length + bend * (2f * length * droop * t)).normalized;
                var n = (normal - tan * Vector3.Dot(normal, tan)).normalized;
                var side = Vector3.Cross(n, tan).normalized;
                float w = width * Mathf.Lerp(rootWidth, 1f, Mathf.Clamp01(t / 0.35f)) * (t > 0.35f ? Mathf.Sqrt(Mathf.Clamp01((1f - t) / 0.65f)) : 1f);
                var l = p - side * (w * 0.5f);
                var r = p + side * (w * 0.5f);
                if (i > 0)
                {
                    b.AddQuad(prevL, l, r, prevR, new Vector2(0, t), new Vector2(0, t), new Vector2(1, t), new Vector2(1, t));
                    b.AddQuad(prevR, r, l, prevL, new Vector2(1, t), new Vector2(1, t), new Vector2(0, t), new Vector2(0, t));
                }
                prevL = l; prevR = r;
            }
        }

        /// <summary>A flat ribbon along a path, double-sided, twisting <paramref name="turns"/> times along its length.</summary>
        public static void Ribbon(MeshBuilder b, IList<Vector3> path, float width, Vector3 refUp, float turns)
        {
            int n = path.Count;
            if (n < 2) return;
            Vector3 T(int i) => (i == 0 ? path[1] - path[0] : i == n - 1 ? path[i] - path[i - 1] : path[i + 1] - path[i - 1]).normalized;
            var frame = Vector3.Cross(refUp, T(0));
            if (frame.sqrMagnitude < 1e-6f) frame = Vector3.Cross(Vector3.right, T(0));
            frame.Normalize();
            Vector3 prevL = Vector3.zero, prevR = Vector3.zero;
            for (int i = 0; i < n; i++)
            {
                var t = T(i);
                frame = (frame - t * Vector3.Dot(frame, t)).normalized;
                var side = Quaternion.AngleAxis(turns * 360f * i / (n - 1), t) * frame;
                var l = path[i] - side * (width * 0.5f);
                var r = path[i] + side * (width * 0.5f);
                if (i > 0)
                {
                    float v = i / (float)(n - 1);
                    b.AddQuad(prevL, l, r, prevR, new Vector2(0, v), new Vector2(0, v), new Vector2(1, v), new Vector2(1, v));
                    b.AddQuad(prevR, r, l, prevL, new Vector2(1, v), new Vector2(1, v), new Vector2(0, v), new Vector2(0, v));
                }
                prevL = l; prevR = r;
            }
        }

        /// <summary>A box from a to b lying flat on the ground (rotated about Y): inlay lines, kerbs, borders.</summary>
        public static void GroundStrip(KitBatch k, Material m, Vector3 a, Vector3 b, float width, float height)
        {
            var d = b - a; d.y = 0f;
            float len = d.magnitude;
            if (len < 1e-4f) return;
            k.Box(m, (a + b) * 0.5f + Vector3.up * (height * 0.5f), new Vector3(width, height, len), Quaternion.LookRotation(d / len, Vector3.up));
        }

        /// <summary>A box from a to b in the plane facing <paramref name="normal"/>: arch outlines and mouldings on a wall.</summary>
        public static void PlaneStrip(KitBatch k, Material m, Vector3 a, Vector3 b, Vector3 normal, float width, float depth, float overlap = 0f)
        {
            var d = b - a;
            float len = d.magnitude;
            if (len < 1e-4f) return;
            var rot = Quaternion.LookRotation(d / len, normal);
            k.Box(m, (a + b) * 0.5f, new Vector3(width, depth, len + overlap), rot);
        }

        /// <summary>
        /// A pointed (two-centred) arch outline in XY, counter-clockwise, base on y = 0: straight jambs to
        /// <paramref name="spring"/>, then two arcs meeting at <paramref name="apex"/>. <paramref name="sharp"/> = how far
        /// each arc centre sits from the middle, as a fraction of the half-width (0 = round arch).
        /// </summary>
        public static List<Vector2> PointedArch(float width, float spring, float sharp = 0.3f, int segs = 8)
        {
            float hw = width * 0.5f, e = hw * sharp, r = hw + e;
            float apexAngle = Mathf.Acos(e / r); // angle (from the +X axis around the left centre) where the arcs meet
            var pts = new List<Vector2> { new Vector2(hw, 0f), new Vector2(hw, spring) };
            // Right arc: centre (-e, spring), from angle 0 to apexAngle.
            for (int i = 1; i <= segs; i++)
            {
                float a = Mathf.Lerp(0f, apexAngle, i / (float)segs);
                pts.Add(new Vector2(-e + Mathf.Cos(a) * r, spring + Mathf.Sin(a) * r));
            }
            // Left arc: centre (e, spring), from the apex down to angle 180.
            for (int i = 1; i <= segs; i++)
            {
                float a = Mathf.Lerp(Mathf.PI - apexAngle, Mathf.PI, i / (float)segs);
                pts.Add(new Vector2(e + Mathf.Cos(a) * r, spring + Mathf.Sin(a) * r));
            }
            pts.Add(new Vector2(-hw, 0f));
            return pts;
        }

        /// <summary>Apex height of <see cref="PointedArch"/> above its base.</summary>
        public static float ArchApex(float width, float spring, float sharp = 0.3f)
        {
            float hw = width * 0.5f, e = hw * sharp, r = hw + e;
            return spring + Mathf.Sqrt(r * r - e * e);
        }

        /// <summary>Outline of a polygon (in a wall's XY plane) as strips: <paramref name="toWorld"/> maps 2D to the cell.</summary>
        public static void Outline(KitBatch k, Material m, IList<Vector2> poly, System.Func<Vector2, Vector3> toWorld, Vector3 normal, float width, float depth, bool closed = false)
        {
            int n = poly.Count;
            for (int i = 0; i < (closed ? n : n - 1); i++)
                PlaneStrip(k, m, toWorld(poly[i]), toWorld(poly[(i + 1) % n]), normal, width, depth, width * 0.6f);
        }

        /// <summary>Regular polygon (counter-clockwise from above when used in XZ).</summary>
        public static List<Vector2> Regular(float radius, int sides, float phaseDeg = 0f)
        {
            var pts = new List<Vector2>(sides);
            for (int i = 0; i < sides; i++)
            {
                float a = (phaseDeg + i * 360f / sides) * Mathf.Deg2Rad;
                pts.Add(new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius));
            }
            return pts;
        }

        /// <summary>A ring of strips (regular polygon outline) lying on the ground.</summary>
        public static void GroundRing(KitBatch k, Material m, Vector3 c, float radius, int sides, float width, float height, float phaseDeg = 0f)
        {
            var p = Regular(radius, sides, phaseDeg);
            for (int i = 0; i < sides; i++)
            {
                var a = c + new Vector3(p[i].x, 0f, p[i].y);
                var b = c + new Vector3(p[(i + 1) % sides].x, 0f, p[(i + 1) % sides].y);
                var d = (b - a).normalized;
                GroundStrip(k, m, a - d * width * 0.5f, b + d * width * 0.5f, width, height);
            }
        }

        public static float Range(System.Random rnd, float a, float b) => a + (float)rnd.NextDouble() * (b - a);
    }
}
