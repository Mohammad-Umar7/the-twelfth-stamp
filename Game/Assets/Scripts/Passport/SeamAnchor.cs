using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// A thin place in the Margin where a tear can be opened: a faint stitched scar on a wall or in the air.
    /// transform.position = centre of the tear (about 1.25 m above the floor); transform.forward points out into the
    /// current world (toward where the player stands). Light borrowed through a tear shines along forward.
    /// </summary>
    public class SeamAnchor : MonoBehaviour
    {
        public string Id = "seam";
        public float CenterHeight = 1.25f;
        public bool AllowTraverse = true;
        public bool Revealed = true;
        /// <summary>The true return in Berlin: only the genuine home tear opens here.</summary>
        public bool HomeSeam;
        /// <summary>0..1 extra glow in warm daylight, for story moments (the seam waking when the glasses go on).</summary>
        public float Flare;

        Material m_Mat;
        GameObject m_Visual;
        bool m_Targeted;
        float m_Glow;

        public Vector3 Center => transform.position;
        public Vector3 Forward => transform.forward;
        public Vector3 FloorPoint => transform.position - Vector3.up * CenterHeight;

        public static SeamAnchor Create(Transform parent, string id, Vector3 floorPos, float yawDegrees, float centerHeight = 1.25f)
        {
            var go = new GameObject("Seam_" + id);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = floorPos + Vector3.up * centerHeight;
            go.transform.localRotation = Quaternion.Euler(0, yawDegrees, 0);
            var s = go.AddComponent<SeamAnchor>();
            s.Id = id;
            s.CenterHeight = centerHeight;
            s.BuildVisual();
            return s;
        }

        void BuildVisual()
        {
            m_Mat = Mats.UnlitInstance(new Color(0.45f, 0.95f, 0.82f, 0.0f), Tex.Fibre, true, 1.4f);
            m_Mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m_Mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            var b = new MeshBuilder();
            // Vertical scar with cross stitches, slightly in front of the surface.
            const float h = 0.9f;
            int stitches = 9;
            for (int i = 0; i < stitches; i++)
            {
                float y = -h / 2 + h * (i + 0.5f) / stitches;
                float w = 0.05f + 0.03f * Mathf.Sin(i * 1.7f);
                float tilt = (i % 2 == 0 ? 1 : -1) * 0.012f;
                b.AddQuad(new Vector3(w, y - 0.006f + tilt, 0), new Vector3(-w, y - 0.006f - tilt, 0), new Vector3(-w, y + 0.006f - tilt, 0), new Vector3(w, y + 0.006f + tilt, 0),
                    new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1));
            }
            b.AddQuad(new Vector3(0.007f, -h / 2, 0), new Vector3(-0.007f, -h / 2, 0), new Vector3(-0.007f, h / 2, 0), new Vector3(0.007f, h / 2, 0),
                new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1));
            m_Visual = MeshFactory.Make("Scar", b.ToMesh("Scar"), m_Mat, transform, Vector3.forward * 0.01f, Quaternion.Euler(0, 180, 0));
            m_Visual.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var mat = m_Visual.GetComponent<Renderer>().sharedMaterial;
            mat.SetFloat("_Cull", 0f);
        }

        public void SetTargeted(bool on) => m_Targeted = on;

        void Update()
        {
            if (m_Mat == null) return;
            bool show = Revealed && Game.Campaign != null && (Game.Campaign.HasFlag(StoryFlags.SeamRevealed) || Game.IsDemo);
            bool tearHere = Game.Tears != null && Game.Tears.Active != null && Game.Tears.Active.Anchor == this;
            float target = !show || tearHere ? 0f : m_Targeted ? 1f : (Game.Passport != null && Game.Passport.IsOpen ? 0.55f : 0.18f);
            if (show && !tearHere) target = Mathf.Max(target, Flare);
            m_Glow = Mathf.MoveTowards(m_Glow, target, Time.deltaTime * 2.5f);
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 2.3f + transform.position.x);
            var c = m_Targeted ? new Color(0.65f, 1f, 0.9f) : new Color(0.45f, 0.95f, 0.82f);
            c = Color.Lerp(c, new Color(1f, 0.82f, 0.55f), Flare);
            m_Mat.SetColor("_Color", new Color(c.r, c.g, c.b, m_Glow * pulse));
            m_Visual.SetActive(m_Glow > 0.01f);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.5f);
        }
    }
}
