using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>Texture access: generated PNGs from Resources, with procedural fallbacks so nothing ever goes missing.</summary>
    public static class Tex
    {
        static readonly Dictionary<string, Texture2D> s_Cache = new Dictionary<string, Texture2D>();
        static readonly HashSet<string> s_Missing = new HashSet<string>();

        public static Texture2D Load(string resourcePath)
        {
            if (s_Cache.TryGetValue(resourcePath, out var t)) return t;
            if (s_Missing.Contains(resourcePath)) return null;
            t = Resources.Load<Texture2D>(resourcePath);
            if (t == null) { s_Missing.Add(resourcePath); return null; }
            s_Cache[resourcePath] = t;
            return t;
        }

        public static Texture2D Albedo(string name) => Load("Textures/" + name + "_a") ?? Noise(name);
        public static Texture2D Normal(string name) => Load("Textures/" + name + "_n");
        public static Texture2D Art(string name) => Load("Art/" + name);

        static Texture2D s_White, s_Dot, s_Streak, s_TearMask, s_Fibre, s_Ring;

        public static Texture2D White
        {
            get
            {
                if (s_White != null) return s_White;
                s_White = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "TS_White" };
                var px = new Color32[16];
                for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
                s_White.SetPixels32(px); s_White.Apply();
                return s_White;
            }
        }

        /// <summary>Soft radial dot for motes, sand, petals and glints.</summary>
        public static Texture2D SoftDot
        {
            get
            {
                if (s_Dot != null) return s_Dot;
                const int n = 64;
                s_Dot = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "TS_SoftDot", wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x + 0.5f) / n * 2 - 1, dy = (y + 0.5f) / n * 2 - 1;
                        float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                        a = a * a * (3 - 2 * a);
                        s_Dot.SetPixel(x, y, new Color(1, 1, 1, a));
                    }
                s_Dot.Apply();
                return s_Dot;
            }
        }

        /// <summary>Thin vertical streak for rain and suspended water.</summary>
        public static Texture2D Streak
        {
            get
            {
                if (s_Streak != null) return s_Streak;
                const int w = 16, h = 64;
                s_Streak = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "TS_Streak", wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float dx = Mathf.Abs((x + 0.5f) / w * 2 - 1);
                        float fy = (y + 0.5f) / h;
                        float a = Mathf.Clamp01(1 - dx * 2.2f) * Mathf.Sin(fy * Mathf.PI);
                        s_Streak.SetPixel(x, y, new Color(1, 1, 1, a));
                    }
                s_Streak.Apply();
                return s_Streak;
            }
        }

        /// <summary>Short ragged fibre used along tear edges.</summary>
        public static Texture2D Fibre
        {
            get
            {
                if (s_Fibre != null) return s_Fibre;
                const int w = 32, h = 32;
                s_Fibre = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "TS_Fibre", wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float fx = (x + 0.5f) / w, fy = (y + 0.5f) / h;
                        float line = Mathf.Clamp01(1f - Mathf.Abs(fy - 0.5f - Mathf.Sin(fx * 9f) * 0.08f) * 14f);
                        float a = line * Mathf.Clamp01(Mathf.Sin(fx * Mathf.PI) * 1.4f);
                        s_Fibre.SetPixel(x, y, new Color(1, 1, 1, a));
                    }
                s_Fibre.Apply();
                return s_Fibre;
            }
        }

        public static Texture2D Ring
        {
            get
            {
                if (s_Ring != null) return s_Ring;
                const int n = 128;
                s_Ring = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "TS_Ring", wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x + 0.5f) / n * 2 - 1, dy = (y + 0.5f) / n * 2 - 1;
                        float r = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(1f - Mathf.Abs(r - 0.82f) * 12f);
                        s_Ring.SetPixel(x, y, new Color(1, 1, 1, a));
                    }
                s_Ring.Apply();
                return s_Ring;
            }
        }

        /// <summary>Ragged almond-shaped opening (alpha) for the tear stencil; fibrous edge noise.</summary>
        public static Texture2D TearMask
        {
            get
            {
                if (s_TearMask != null) return s_TearMask;
                const int w = 128, h = 256;
                s_TearMask = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "TS_TearMask", wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < h; y++)
                {
                    float fy = (y + 0.5f) / h;              // 0..1 bottom to top
                    float profile = Mathf.Pow(Mathf.Sin(fy * Mathf.PI), 0.8f); // almond
                    for (int x = 0; x < w; x++)
                    {
                        float fx = (x + 0.5f) / w * 2f - 1f;  // -1..1
                        float jag = (Mathf.PerlinNoise(fy * 18f, x < w / 2 ? 3.1f : 7.7f) - 0.5f) * 0.22f
                                  + (Mathf.PerlinNoise(fy * 61f, x < w / 2 ? 1.3f : 5.2f) - 0.5f) * 0.1f;
                        float edge = profile * (0.92f + jag);
                        float a = Mathf.Abs(fx) < edge ? 1f : 0f;
                        s_TearMask.SetPixel(x, y, new Color(1, 1, 1, a));
                    }
                }
                s_TearMask.Apply();
                return s_TearMask;
            }
        }

        /// <summary>Procedural neutral texture when a generated texture is missing (logs once).</summary>
        static Texture2D Noise(string name)
        {
            var key = "noise:" + name;
            if (s_Cache.TryGetValue(key, out var t)) return t;
            Debug.LogWarning("[TS-Tex] Missing texture '" + name + "', using procedural fallback");
            const int n = 128;
            t = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "TS_Fallback_" + name, wrapMode = TextureWrapMode.Repeat };
            float seed = (name.GetHashCode() & 0xffff) * 0.01f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float v = 0.82f + (Mathf.PerlinNoise(x * 0.08f + seed, y * 0.08f + seed) - 0.5f) * 0.18f
                                    + (Mathf.PerlinNoise(x * 0.35f + seed, y * 0.35f) - 0.5f) * 0.06f;
                    t.SetPixel(x, y, new Color(v, v, v, 1));
                }
            t.Apply();
            s_Cache[key] = t;
            return t;
        }
    }
}
