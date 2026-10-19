using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// A piece of a remembered place that paints itself in as the player walks towards it: it rises out of the ground and
    /// grows into place, and any light on it fades up (the Marrakech memory's opening "wow"). World motion only: the
    /// camera never moves. Outside a memory (see <see cref="Begin"/> / <see cref="End"/>) everything simply stands there.
    /// </summary>
    public class MemoryReveal : MonoBehaviour
    {
        /// <summary>Starts revealing when the player is this close (flat distance, metres).</summary>
        public float Radius = 9f;
        public float Seconds = 1.1f;
        /// <summary>Extra wait after the player comes in range (stagger the parts of one stall).</summary>
        public float Delay;
        /// <summary>How far below its place it starts (metres).</summary>
        public float Sink = 1.4f;

        Vector3 m_Home, m_Scale;
        Light[] m_Lights;
        float[] m_LightBase;
        static float s_LastSound = -10f;
        float m_T = 1f, m_Wait, m_FoldDelay;
        bool m_Hidden, m_Init, m_Folding;

        public bool Revealed => !m_Hidden && m_T >= 1f;

        /// <summary>Adds (or returns) the reveal on a group root. Call after the group is fully built.</summary>
        public static MemoryReveal Attach(GameObject go, float radius = 9f, float delay = 0f, float sink = 1.4f)
        {
            var r = go.GetComponent<MemoryReveal>() ?? go.AddComponent<MemoryReveal>();
            r.Radius = radius; r.Delay = delay; r.Sink = sink;
            return r;
        }

        void Init()
        {
            if (m_Init) return;
            m_Init = true;
            m_Home = transform.localPosition;
            m_Scale = transform.localScale;
            m_Lights = GetComponentsInChildren<Light>(true);
            m_LightBase = new float[m_Lights.Length];
            for (int i = 0; i < m_Lights.Length; i++) m_LightBase[i] = m_Lights[i].intensity;
        }

        /// <summary>Hide every reveal under <paramref name="root"/>; each paints in as the player approaches.</summary>
        public static void Begin(Transform root)
        {
            foreach (var r in root.GetComponentsInChildren<MemoryReveal>(true))
            {
                r.Init();
                r.m_Hidden = true; r.m_T = 0f; r.m_Wait = 0f;
                r.Apply(0f);
            }
        }

        /// <summary>Show everything under <paramref name="root"/> at once (memory over, skipped, or a revisit).</summary>
        public static void End(Transform root)
        {
            foreach (var r in root.GetComponentsInChildren<MemoryReveal>(true))
            {
                r.Init();
                r.m_Hidden = false; r.m_Folding = false; r.m_T = 1f;
                r.Apply(1f);
            }
        }

        /// <summary>
        /// The memory unmakes itself: everything under <paramref name="root"/> sinks back into the ground, nearest to
        /// <paramref name="from"/> last, over about <paramref name="seconds"/>.
        /// </summary>
        public static void FoldAway(Transform root, Vector3 from, float seconds)
        {
            var list = root.GetComponentsInChildren<MemoryReveal>(true);
            float far = 0.01f;
            foreach (var r in list) { var d = r.transform.position - from; d.y = 0f; far = Mathf.Max(far, d.magnitude); }
            foreach (var r in list)
            {
                r.Init();
                var d = r.transform.position - from; d.y = 0f;
                r.m_Hidden = false; r.m_T = 1f; r.m_Folding = true;
                r.m_FoldDelay = (1f - d.magnitude / far) * seconds * 0.75f;
            }
        }

        /// <summary>Fraction of the reveals under <paramref name="root"/> that have finished painting in (tests, pacing).</summary>
        public static float Progress(Transform root)
        {
            int n = 0, done = 0;
            foreach (var r in root.GetComponentsInChildren<MemoryReveal>(true)) { n++; if (r.Revealed) done++; }
            return n == 0 ? 1f : done / (float)n;
        }

        void Update()
        {
            if (m_Folding)
            {
                if (m_FoldDelay > 0f) { m_FoldDelay -= Time.deltaTime; return; }
                m_T = Mathf.Max(0f, m_T - Time.deltaTime / Mathf.Max(0.05f, Seconds));
                Apply(m_T);
                if (m_T <= 0f) m_Folding = false;
                return;
            }
            if (!m_Hidden) return;
            var rig = Game.Rig;
            if (rig == null) return;
            var d = transform.position - rig.Feet; d.y = 0f;
            if (m_T <= 0f && d.magnitude > Radius) return;
            if (m_T <= 0f && m_Wait < Delay) { m_Wait += Time.deltaTime; return; }
            // A soft shimmer as a piece paints in, at most every 0.2 s (a whole square of stalls can rise together).
            if (m_T <= 0f && Time.time - s_LastSound > 0.2f)
            {
                s_LastSound = Time.time;
                AudioDirector.I?.Play("pp_seam_shimmer", transform.position + Vector3.up, 0.38f, Random.Range(1.3f, 1.6f)); // re-levelled clip is 7.6 dB quieter
            }
            m_T = Mathf.Min(1f, m_T + Time.deltaTime / Mathf.Max(0.05f, Seconds));
            Apply(m_T);
            if (m_T >= 1f) m_Hidden = false;
        }

        void Apply(float t)
        {
            float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
            transform.localPosition = m_Home - Vector3.up * Sink * (1f - e);
            transform.localScale = m_Scale * Mathf.Lerp(0.55f, 1f, e);
            for (int i = 0; i < m_Lights.Length; i++) if (m_Lights[i] != null) m_Lights[i].intensity = m_LightBase[i] * e * e;
        }
    }
}
