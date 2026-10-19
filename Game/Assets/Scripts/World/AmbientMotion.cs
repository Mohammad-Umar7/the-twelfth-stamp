using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// Small motion that keeps a place from looking like a photograph: hanging things sway in the wind
    /// (<see cref="WindSway"/>), flags and cloth ripple (<see cref="ClothFlag"/>), fans and wheels turn (<see cref="Rotor"/>),
    /// boats and lanterns on water bob (<see cref="Bob"/>), flames and old bulbs flicker (<see cref="Flicker"/>). All are
    /// world motion only and cheap; a shared slow wind (<see cref="Wind"/>) gusts so neighbours move together.
    /// </summary>
    public static class Wind
    {
        /// <summary>0..1 gust strength now (a slow, irregular swell).</summary>
        public static float Gust => 0.55f + 0.45f * (Mathf.PerlinNoise(Time.time * 0.13f, 0.7f) * 2f - 0.5f);
    }

    /// <summary>A pendulum sway about the object's own pivot (hang the pivot at the top: lanterns, signs, laundry).</summary>
    public class WindSway : MonoBehaviour
    {
        public float Degrees = 6f, Period = 2.4f;
        public Vector3 Axis = Vector3.forward;
        Quaternion m_Rest;
        float m_Phase;

        public static WindSway Add(Transform t, float degrees = 6f, float period = 2.4f, Vector3? axis = null)
        {
            var s = t.gameObject.AddComponent<WindSway>();
            s.Degrees = degrees;
            s.Period = Mathf.Max(0.3f, period);
            s.Axis = axis ?? Vector3.forward;
            return s;
        }

        void Awake()
        {
            m_Rest = transform.localRotation;
            m_Phase = Random.value * 10f;
        }

        void Update()
        {
            float t = Time.time + m_Phase;
            float a = Mathf.Sin(t * Mathf.PI * 2f / Period) * Degrees * Wind.Gust
                      + Mathf.Sin(t * 5.3f) * Degrees * 0.12f;
            transform.localRotation = m_Rest * Quaternion.AngleAxis(a, Axis) * Quaternion.AngleAxis(Mathf.Sin(t * 1.7f) * Degrees * 0.3f, Vector3.right);
        }
    }

    /// <summary>A constant turn (ceiling fans, windmills, a carousel).</summary>
    public class Rotor : MonoBehaviour
    {
        public float DegreesPerSecond = 90f;
        public Vector3 Axis = Vector3.up;

        public static Rotor Add(Transform t, float degreesPerSecond, Vector3? axis = null)
        {
            var s = t.gameObject.AddComponent<Rotor>();
            s.DegreesPerSecond = degreesPerSecond;
            s.Axis = axis ?? Vector3.up;
            return s;
        }

        void Update() => transform.localRotation *= Quaternion.AngleAxis(DegreesPerSecond * Time.deltaTime, Axis);
    }

    /// <summary>A gentle float (boats at a quay, lanterns on water, a balloon on a string).</summary>
    public class Bob : MonoBehaviour
    {
        public float Amplitude = 0.05f, Period = 3f, Roll = 2f;
        Vector3 m_Rest;
        Quaternion m_RestRot;
        float m_Phase;

        public static Bob Add(Transform t, float amplitude = 0.05f, float period = 3f, float rollDegrees = 2f)
        {
            var b = t.gameObject.AddComponent<Bob>();
            b.Amplitude = amplitude;
            b.Period = Mathf.Max(0.3f, period);
            b.Roll = rollDegrees;
            return b;
        }

        void Awake()
        {
            m_Rest = transform.localPosition;
            m_RestRot = transform.localRotation;
            m_Phase = Random.value * 10f;
        }

        void Update()
        {
            float t = (Time.time + m_Phase) * Mathf.PI * 2f / Period;
            transform.localPosition = m_Rest + Vector3.up * Mathf.Sin(t) * Amplitude;
            transform.localRotation = m_RestRot * Quaternion.Euler(Mathf.Sin(t * 0.8f) * Roll, 0f, Mathf.Cos(t * 0.6f) * Roll);
        }
    }

    /// <summary>A flame or failing bulb: the light's intensity wavers (and gutters now and then).</summary>
    public class Flicker : MonoBehaviour
    {
        public float Amount = 0.25f, Speed = 9f;
        Light m_Light;
        float m_Base, m_Seed;

        public static Flicker Add(Light light, float amount = 0.25f, float speed = 9f)
        {
            var f = light.gameObject.AddComponent<Flicker>();
            f.Amount = amount;
            f.Speed = speed;
            return f;
        }

        void Awake()
        {
            m_Light = GetComponent<Light>();
            m_Base = m_Light != null ? m_Light.intensity : 1f;
            m_Seed = Random.value * 100f;
        }

        void Update()
        {
            if (m_Light == null || !m_Light.enabled) return;
            float n = Mathf.PerlinNoise(Time.time * Speed, m_Seed);
            float gutter = Mathf.PerlinNoise(Time.time * 0.7f, m_Seed + 3f) > 0.82f ? 0.55f : 1f;
            m_Light.intensity = m_Base * (1f - Amount + Amount * 2f * n) * gutter;
        }
    }

    /// <summary>
    /// A flag or hanging cloth: a grid that ripples with a travelling wave, stronger at the free edge, gusting with
    /// <see cref="Wind"/>. Its origin is the top corner at the pole; it extends along local +X and down local -Y.
    /// </summary>
    public class ClothFlag : MonoBehaviour
    {
        const int Cols = 12, Rows = 6;
        public float Width = 1.2f, Height = 0.7f, Wave = 0.12f;
        Mesh m_Mesh;
        Vector3[] m_Rest, m_Verts;
        float m_Phase;

        public static ClothFlag Create(Transform parent, string name, Vector3 localTopCorner, float width, float height, Material mat, float yaw = 0f, float wave = 0.12f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localTopCorner;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var f = go.AddComponent<ClothFlag>();
            f.Width = width;
            f.Height = height;
            f.Wave = wave;
            f.Build(mat);
            return f;
        }

        void Build(Material mat)
        {
            // Two faces with their own vertices (front 0..n-1, back n..2n-1): shared vertices under opposite windings
            // would average their normals to zero and render black.
            int n = (Cols + 1) * (Rows + 1);
            m_Rest = new Vector3[n];
            var uv = new Vector2[n * 2];
            for (int r = 0; r <= Rows; r++)
                for (int c = 0; c <= Cols; c++)
                {
                    int i = r * (Cols + 1) + c;
                    m_Rest[i] = new Vector3(Width * c / Cols, -Height * r / Rows, 0f);
                    uv[i] = uv[i + n] = new Vector2((float)c / Cols, 1f - (float)r / Rows);
                }
            var tris = new int[Cols * Rows * 12];
            int k = 0;
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                {
                    int a = r * (Cols + 1) + c, b = a + 1, d = a + Cols + 1, e = d + 1;
                    tris[k++] = a; tris[k++] = b; tris[k++] = d;
                    tris[k++] = b; tris[k++] = e; tris[k++] = d;
                    tris[k++] = a + n; tris[k++] = d + n; tris[k++] = b + n;
                    tris[k++] = b + n; tris[k++] = d + n; tris[k++] = e + n;
                }
            m_Verts = new Vector3[n * 2];
            for (int i = 0; i < n; i++) m_Verts[i] = m_Verts[i + n] = m_Rest[i];
            m_Mesh = new Mesh { name = name };
            m_Mesh.MarkDynamic();
            m_Mesh.vertices = m_Verts;
            m_Mesh.uv = uv;
            m_Mesh.triangles = tris;
            m_Mesh.RecalculateNormals();
            m_Mesh.RecalculateBounds();
            gameObject.AddComponent<MeshFilter>().sharedMesh = m_Mesh;
            var mr = gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            m_Phase = Random.value * 10f;
        }

        void Update()
        {
            if (m_Mesh == null) return;
            float t = Time.time + m_Phase;
            float gust = Wind.Gust;
            int n = m_Rest.Length;
            for (int i = 0; i < n; i++)
            {
                var p = m_Rest[i];
                float along = p.x / Mathf.Max(0.01f, Width);              // 0 at the pole, 1 at the free edge
                float w = Mathf.Sin(along * 7f - t * 5.5f) * Wave * along * (0.6f + gust);
                float sag = -along * along * Height * 0.08f * (1.2f - gust);
                m_Verts[i] = m_Verts[i + n] = new Vector3(p.x * (1f - 0.04f * Mathf.Abs(w)), p.y + sag, w);
            }
            m_Mesh.vertices = m_Verts;
            m_Mesh.RecalculateNormals();
            m_Mesh.RecalculateBounds();
        }
    }
}
