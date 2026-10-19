using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// A country's stamp hanging in the air: its artwork on a turning disc, a soft glow and a tall beam of light that
    /// can be seen from far off. Walk (or run) into it to collect it: it chimes, flies into the player and calls
    /// <see cref="Collected"/>. A portal is the large version, a doorway-sized seal that opens the way on; it can wait,
    /// dimmed, until <see cref="Ready"/> is set (for example, until the smaller stamps are collected).
    /// No collider: collection is by distance, so it works the same with desktop walking, VR teleport or smooth movement.
    /// </summary>
    public class StampPortal : MonoBehaviour
    {
        public CountryId Country;
        public bool IsPortal;
        public float Radius = 1.4f;
        public event System.Action<StampPortal> Collected;
        public bool IsCollected { get; private set; }

        bool m_Ready = true;
        Transform m_Disc;
        Material m_Face, m_Glow, m_Beam;
        Color m_Tint;
        float m_Fly = -1f;
        Vector3 m_FlyFrom;
        float m_BaseY;

        public bool Ready
        {
            get => m_Ready;
            set { m_Ready = value; ApplyReady(); }
        }

        /// <summary>
        /// Places a stamp (or a portal) at <paramref name="worldPos"/> (its centre) under <paramref name="parent"/>.
        /// <paramref name="onCollected"/> runs once, when the player reaches it while it is ready.
        /// </summary>
        public static StampPortal Spawn(Transform parent, Vector3 worldPos, CountryId country, System.Action<StampPortal> onCollected, bool portal = false)
        {
            var go = new GameObject(portal ? "StampPortal_" + country : "Stamp_" + country);
            go.transform.SetParent(parent, true);
            go.transform.position = worldPos;
            var s = go.AddComponent<StampPortal>();
            s.Country = country;
            s.IsPortal = portal;
            s.Radius = portal ? 2.2f : 1.4f;
            if (onCollected != null) s.Collected += onCollected;
            s.Build();
            return s;
        }

        void Build()
        {
            var def = CountryDatabase.Get(Country);
            m_Tint = def != null ? def.Tint : new Color(1f, 0.8f, 0.5f);
            var art = def != null ? Tex.Art(def.StampTexture) : null;
            float r = IsPortal ? 1.5f : 0.42f;
            m_Disc = new GameObject("Disc").transform;
            m_Disc.SetParent(transform, false);
            m_BaseY = 0f;

            // Paper-coloured disc with the stamp art on both faces, a gold rim.
            var b = new MeshBuilder();
            b.AddCylinder(new Vector3(0f, 0f, -0.02f), r, 0.04f, 32, 1f, Quaternion.Euler(90f, 0f, 0f));
            var rim = MeshFactory.FromBuilder("Rim", b, Mats.Emissive(new Color(0.8f, 0.62f, 0.3f), new Color(0.45f, 0.32f, 0.12f)), m_Disc);
            rim.transform.localRotation = Quaternion.identity;
            m_Face = Mats.UnlitInstance(Color.white, art != null ? art : Tex.SoftDot, true, IsPortal ? 1.5f : 1.3f);
            MeshFactory.Quad("FaceFront", m_Disc, r * 1.86f, r * 1.86f, m_Face, new Vector3(0f, 0f, -0.045f), Quaternion.identity);
            MeshFactory.Quad("FaceBack", m_Disc, r * 1.86f, r * 1.86f, m_Face, new Vector3(0f, 0f, 0.045f), Quaternion.Euler(0f, 180f, 0f));

            // Glow round the disc and a beam that rises from it (additive, no lights needed).
            m_Glow = new Material(Mats.Particle(Tex.SoftDot, true, m_Tint, IsPortal ? 1.6f : 1.4f)) { name = "StampGlow" };
            MeshFactory.Quad("Glow", m_Disc, r * 4.2f, r * 4.2f, m_Glow, new Vector3(0f, 0f, 0.06f), Quaternion.identity);
            m_Beam = new Material(Mats.Particle(Tex.Streak, true, m_Tint, IsPortal ? 1.2f : 0.9f)) { name = "StampBeam" };
            float beamH = IsPortal ? 40f : 24f, beamW = IsPortal ? 2.4f : 1.1f;
            for (int i = 0; i < 2; i++)
                MeshFactory.Quad("Beam", transform, beamW, beamH, m_Beam, Vector3.up * (beamH * 0.5f - r), Quaternion.Euler(0f, i * 90f, 0f));
            foreach (var rend in GetComponentsInChildren<Renderer>()) rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (IsPortal)
            {
                var l = new GameObject("PortalLight").AddComponent<Light>();
                l.transform.SetParent(transform, false);
                l.type = LightType.Point; l.color = m_Tint; l.range = 9f; l.intensity = 2.2f; l.shadows = LightShadows.None;
            }
            ApplyReady();
        }

        void ApplyReady()
        {
            if (m_Glow == null) return;
            float a = m_Ready ? 1f : 0.25f;
            m_Glow.SetColor("_TintColor", m_Tint * a);
            m_Beam.SetColor("_TintColor", m_Tint * (m_Ready ? 1f : 0.15f));
            m_Face.SetColor("_Color", new Color(1f, 1f, 1f, m_Ready ? 1f : 0.4f));
        }

        void Update()
        {
            if (m_Disc == null) return;
            float t = Time.time + transform.position.x * 0.37f;
            if (m_Fly >= 0f)
            {
                // Collected: it flies into the player's chest and shrinks away.
                m_Fly += Time.deltaTime / 0.45f;
                var to = Game.Rig != null ? Game.Rig.Head.position + Vector3.down * 0.45f : m_FlyFrom;
                transform.position = Vector3.Lerp(m_FlyFrom, to, Mathf.SmoothStep(0f, 1f, m_Fly));
                transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.05f, m_Fly);
                if (m_Fly >= 1f) Destroy(gameObject);
                return;
            }
            // Turn slowly to show both faces, and bob.
            m_Disc.localRotation = Quaternion.Euler(0f, t * (IsPortal ? 20f : 55f), 0f);
            m_Disc.localPosition = Vector3.up * (m_BaseY + Mathf.Sin(t * 1.6f) * (IsPortal ? 0.12f : 0.09f));
            if (!m_Ready || IsCollected || Game.Rig == null) return;
            var head = Game.Rig.Head.position;
            var feet = Game.Rig.Feet;
            var p = transform.position;
            bool near = new Vector2(p.x - head.x, p.z - head.z).magnitude < Radius && p.y > feet.y - 0.5f && p.y < head.y + Radius;
            if (near) Collect();
        }

        /// <summary>Collects it now (also used by the autotest).</summary>
        public void Collect()
        {
            if (IsCollected) return;
            IsCollected = true;
            AudioDirector.I?.Play("stamp_hit", transform.position, IsPortal ? 1f : 0.8f);
            AudioDirector.I?.Play("seal_verify", null, IsPortal ? 0.8f : 0.45f, IsPortal ? 0.9f : 1.15f, Bus.SFX);
            AudioDirector.Haptic(HandSide.Left, 0.4f, 0.12f);
            AudioDirector.Haptic(HandSide.Right, 0.4f, 0.12f);
            m_FlyFrom = transform.position;
            m_Fly = 0f;
            Collected?.Invoke(this);
        }
    }
}
